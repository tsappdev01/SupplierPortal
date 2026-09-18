using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.Reporting;

public record ReportTable(string Schema, string Name, bool IsView)
{
    public string FullName => $"{Schema}.{Name}";
}

public record ReportColumn(string Name, string DataType);

/// <summary>A single-column foreign-key relationship from the base table to a referenced table. <see cref="Label"/>
/// is a friendly name for the relationship (the FK column with a trailing "Id" removed).</summary>
public record ReportRelation(string FkColumn, string RefSchema, string RefTable, string RefColumn, string Label);

public record ReportData(List<string> Headers, List<string[]> Rows, bool Truncated);

/// <summary>A report source loaded for the builder: the base table's columns plus each FK relationship
/// with the related table's columns.</summary>
public record ReportSource(List<ReportColumn> Columns, List<(ReportRelation Rel, List<ReportColumn> Cols)> Related);

/// <summary>The whole database's report metadata, loaded once and cached: tables/views, selectable columns per
/// table, and single-column FK relations per table. Keyed by "schema|table" (case-insensitive).</summary>
public record ReportCatalog(
    List<ReportTable> Tables,
    Dictionary<string, List<ReportColumn>> ColumnsByTable,
    Dictionary<string, List<ReportRelation>> RelationsByTable);

/// <summary>Read-only database metadata browser and safe query executor for the Report Builder. Administrators
/// pick a table/view plus (optionally) columns reached through its foreign keys; every table, view, column and
/// relationship is re-validated against live metadata, identifiers are taken only from that metadata and
/// bracket-quoted, related tables are LEFT-joined only along real FK relationships, and all filter values are
/// parameterised — so a report definition can never inject SQL or reach an object the database does not expose.
/// Sensitive columns (passwords, secrets, hashes, tokens) and binary/blob columns are hidden.</summary>
public class ReportSchemaService(IDbContextFactory<ApplicationDbContext> dbFactory, IMemoryCache cache)
{
    /// <summary>Separator in an encoded related-field key: "{FkColumn}→{RefColumn}".</summary>
    public const string RelSep = "→"; // →

    private const int MaxColumns = 60;
    private const int DefaultMaxRows = 5000;
    private const int HardMaxRows = 20000;
    private const int MetaTimeoutSeconds = 90;  // metadata queries can be slow on a loaded/serverless DB
    private const string CatalogCacheKey = "report:catalog:v1";
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim CatalogLock = new(1, 1);

    private static string Key(string schema, string table) => $"{schema}|{table}";

    /// <summary>Loads the whole-database report metadata once (3 set-based queries) and caches it, so table and
    /// field loading in the builder is instant after the first prime. Call <see cref="InvalidateCatalog"/> if the
    /// schema changes.</summary>
    public async Task<ReportCatalog> GetCatalogAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CatalogCacheKey, out ReportCatalog? cached) && cached is not null) { return cached; }
        await CatalogLock.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(CatalogCacheKey, out cached) && cached is not null) { return cached; }
            var built = await BuildCatalogAsync(ct);
            cache.Set(CatalogCacheKey, built, CatalogTtl);
            return built;
        }
        finally { CatalogLock.Release(); }
    }

    public void InvalidateCatalog() => cache.Remove(CatalogCacheKey);

    private async Task<ReportCatalog> BuildCatalogAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);

        // 1) Tables & views
        var tables = new List<ReportTable>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandTimeout = MetaTimeoutSeconds;
            cmd.CommandText =
                "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_TYPE IN ('BASE TABLE','VIEW') ORDER BY TABLE_SCHEMA, TABLE_NAME";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var s = r.GetString(0); var n = r.GetString(1);
                if (BlockedSchemas.Contains(s) || BlockedTables.Contains(n)) { continue; }
                tables.Add(new ReportTable(s, n, string.Equals(r.GetString(2), "VIEW", StringComparison.OrdinalIgnoreCase)));
            }
        }

        // 2) All selectable columns (one query for the whole DB)
        var columnsByTable = new Dictionary<string, List<ReportColumn>>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandTimeout = MetaTimeoutSeconds;
            cmd.CommandText =
                "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                "ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var s = r.GetString(0); var t = r.GetString(1); var c = r.GetString(2); var dt = r.GetString(3);
                if (BlockedSchemas.Contains(s) || BlockedTables.Contains(t)) { continue; }
                if (!IsSelectable(c, dt)) { continue; }
                var k = Key(s, t);
                (columnsByTable.TryGetValue(k, out var l) ? l : columnsByTable[k] = []).Add(new ReportColumn(c, dt));
            }
        }

        // 3) All single-column FK relations (one query for the whole DB)
        var relationsByTable = new Dictionary<string, List<ReportRelation>>(StringComparer.OrdinalIgnoreCase);
        var byConstraint = new Dictionary<string, List<(string ParentKey, ReportRelation Rel)>>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandTimeout = MetaTimeoutSeconds;
            cmd.CommandText = @"
SELECT fk.name AS FkName, ps.name AS ParentSchema, pt.name AS ParentTable, pc.name AS FkColumn,
       rs.name AS RefSchema, rt.name AS RefTable, rc.name AS RefColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var fkName = r.GetString(0);
                var pSchema = r.GetString(1); var pTable = r.GetString(2); var fkCol = r.GetString(3);
                var rSchema = r.GetString(4); var rTable = r.GetString(5); var rCol = r.GetString(6);
                if (BlockedSchemas.Contains(pSchema) || BlockedTables.Contains(pTable)) { continue; }
                if (BlockedSchemas.Contains(rSchema) || BlockedTables.Contains(rTable)) { continue; }
                var rel = new ReportRelation(fkCol, rSchema, rTable, rCol, Label(fkCol));
                (byConstraint.TryGetValue(fkName, out var l) ? l : byConstraint[fkName] = []).Add((Key(pSchema, pTable), rel));
            }
        }
        // Keep only single-column FKs; group by parent table, de-dupe by FK column.
        foreach (var group in byConstraint.Values.Where(g => g.Count == 1))
        {
            var (parentKey, rel) = group[0];
            var list = relationsByTable.TryGetValue(parentKey, out var l) ? l : relationsByTable[parentKey] = [];
            if (!list.Any(x => string.Equals(x.FkColumn, rel.FkColumn, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(rel);
            }
        }
        foreach (var l in relationsByTable.Values) { l.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase)); }

        return new ReportCatalog(tables, columnsByTable, relationsByTable);
    }

    private static readonly HashSet<string> BlockedSchemas =
        new(StringComparer.OrdinalIgnoreCase) { "sys", "INFORMATION_SCHEMA", "guest" };

    private static readonly HashSet<string> BlockedTables =
        new(StringComparer.OrdinalIgnoreCase) { "__EFMigrationsHistory" };

    private static readonly string[] SensitiveColumnMarkers =
        ["password", "pwd", "secret", "salt", "hash", "token", "apikey", "api_key", "connectionstring"];

    private static readonly HashSet<string> BinaryTypes =
        new(StringComparer.OrdinalIgnoreCase) { "varbinary", "binary", "image", "timestamp", "rowversion", "geography", "geometry", "hierarchyid" };

    // ---- Field key encoding (a selectable field is either a base column, or "{Fk}→{RefColumn}") ----
    public static string EncodeRelated(string fkColumn, string refColumn) => $"{fkColumn}{RelSep}{refColumn}";

    public static bool TryDecodeRelated(string field, out string fkColumn, out string refColumn)
    {
        var i = field.IndexOf(RelSep, StringComparison.Ordinal);
        if (i < 0) { fkColumn = refColumn = ""; return false; }
        fkColumn = field[..i];
        refColumn = field[(i + RelSep.Length)..];
        return true;
    }

    private static bool IsSelectable(string columnName, string dataType) =>
        !BinaryTypes.Contains(dataType)
        && !SensitiveColumnMarkers.Any(m => columnName.Contains(m, StringComparison.OrdinalIgnoreCase));

    // ---- All lookups below are served from the cached catalog (no per-table DB round-trips) ----

    public async Task<List<ReportTable>> GetTablesAsync(CancellationToken ct = default) =>
        (await GetCatalogAsync(ct)).Tables;

    /// <summary>Selectable columns for a table/view, from the cached catalog.</summary>
    public async Task<List<ReportColumn>> GetColumnsAsync(string schema, string table, CancellationToken ct = default)
    {
        var cat = await GetCatalogAsync(ct);
        return cat.ColumnsByTable.TryGetValue(Key(schema, table), out var c) ? c : [];
    }

    /// <summary>The single-column FK relationships from a table (from the cached catalog; empty for views).</summary>
    public async Task<List<ReportRelation>> GetRelationsAsync(string schema, string table, CancellationToken ct = default)
    {
        var cat = await GetCatalogAsync(ct);
        return cat.RelationsByTable.TryGetValue(Key(schema, table), out var r) ? r : [];
    }

    /// <summary>Everything the builder needs for a source (base columns + relations + related columns), from cache.</summary>
    public async Task<ReportSource> GetSourceAsync(string schema, string table, CancellationToken ct = default)
    {
        var cat = await GetCatalogAsync(ct);
        var baseCols = cat.ColumnsByTable.TryGetValue(Key(schema, table), out var bc) ? bc : [];
        var relations = cat.RelationsByTable.TryGetValue(Key(schema, table), out var rels) ? rels : [];
        var related = relations
            .Select(rel => (Rel: rel, Cols: cat.ColumnsByTable.TryGetValue(Key(rel.RefSchema, rel.RefTable), out var rc) ? rc : []))
            .Where(x => x.Cols.Count > 0)
            .ToList();
        return new ReportSource(baseCols, related);
    }

    private static string Label(string fkColumn)
    {
        foreach (var suffix in new[] { "Id", "ID", "_id" })
        {
            if (fkColumn.EndsWith(suffix, StringComparison.Ordinal) && fkColumn.Length > suffix.Length)
            {
                return fkColumn[..^suffix.Length];
            }
        }
        return fkColumn;
    }

    /// <summary>Validates a definition's source and fields (base or related) against live metadata; returns an
    /// error message when the table/view is missing or any field is unknown or unavailable.</summary>
    public async Task<string?> ValidateAsync(string schema, string table, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(table)) { return "Select a table or view."; }
        if (columns.Count == 0) { return "Select at least one column."; }
        if (columns.Count > MaxColumns) { return $"Select at most {MaxColumns} columns."; }

        var resolver = BuildResolver(await GetCatalogAsync(ct), schema, table);
        if (resolver.BaseColumns.Count == 0) { return "The selected table or view is not available."; }

        var unknown = columns.Where(c => resolver.Resolve(c) is null).ToList();
        return unknown.Count > 0 ? $"Unknown or unavailable field(s): {string.Join(", ", unknown)}." : null;
    }

    public async Task<ReportData> RunAsync(ReportDefinition def, int maxRows = DefaultMaxRows, CancellationToken ct = default)
    {
        maxRows = Math.Clamp(maxRows, 1, HardMaxRows);
        var columns = def.GetColumns();
        if (columns.Count == 0) { throw new InvalidOperationException("The report has no columns."); }

        // Metadata comes from the cached catalog (no round-trips); only the data query below hits the DB.
        var resolver = BuildResolver(await GetCatalogAsync(ct), def.SchemaName, def.TableName);
        if (resolver.BaseColumns.Count == 0) { throw new InvalidOperationException("The selected table or view is not available."); }

        var selected = new List<ResolvedField>();
        foreach (var c in columns)
        {
            var rf = resolver.Resolve(c) ?? throw new InvalidOperationException($"Unavailable field '{c}'.");
            selected.Add(rf);
        }

        // Filters (unknown fields are ignored rather than failing the whole run).
        var filters = def.GetFilters();
        var resolvedFilters = new List<(ResolvedField Field, FilterOperator Op, string? Value)>();
        foreach (var cond in filters.Conditions)
        {
            if (string.IsNullOrWhiteSpace(cond.Column)) { continue; }
            var rf = resolver.Resolve(cond.Column);
            if (rf is not null) { resolvedFilters.Add((rf, cond.Operator, cond.Value)); }
        }

        // Which relations are actually used (by SELECT or WHERE) → LEFT JOINs.
        var usedFks = selected.Where(f => f.Relation is not null).Select(f => f.Relation!.FkColumn)
            .Concat(resolvedFilters.Where(f => f.Field.Relation is not null).Select(f => f.Field.Relation!.FkColumn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(fk => resolver.Relations[fk])
            .ToList();

        // Open a connection for the actual data query (metadata already came from the cache).
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 60;

        var select = string.Join(", ", selected.Select(f => f.Expr()));
        var sql = new StringBuilder($"SELECT TOP (@__max) {select} FROM {Bracket(def.SchemaName)}.{Bracket(def.TableName)} {BaseAlias}");
        AddParam(cmd, "@__max", maxRows + 1);
        foreach (var rel in usedFks)
        {
            sql.Append($" LEFT JOIN {Bracket(rel.RefSchema)}.{Bracket(rel.RefTable)} {JoinAlias(rel.FkColumn)}" +
                       $" ON {JoinAlias(rel.FkColumn)}.{Bracket(rel.RefColumn)} = {BaseAlias}.{Bracket(rel.FkColumn)}");
        }
        BuildWhere(cmd, sql, filters.Match, resolvedFilters);

        cmd.CommandText = sql.ToString();

        var headers = selected.Select(f => f.Header).ToList();
        var rows = new List<string[]>();
        var truncated = false;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= maxRows) { truncated = true; break; }
            var row = new string[headers.Count];
            for (var i = 0; i < headers.Count; i++) { row[i] = Format(reader.GetValue(i)); }
            rows.Add(row);
        }
        return new ReportData(headers, rows, truncated);
    }

    // ---- Field resolution (shared by validate + run) ----
    private sealed class ResolvedField
    {
        public required string Header { get; init; }
        public required string RealColumn { get; init; }
        public ReportRelation? Relation { get; init; }   // null = base column
        public string Expr() => Relation is null
            ? $"{BaseAlias}.{Bracket(RealColumn)}"
            : $"{JoinAlias(Relation.FkColumn)}.{Bracket(RealColumn)}";
    }

    private sealed class FieldResolver
    {
        public required Dictionary<string, string> BaseColumns { get; init; }       // real name -> type
        public required Dictionary<string, ReportRelation> Relations { get; init; } // key: FK column
        public required Dictionary<string, Dictionary<string, string>> RefColumns { get; init; } // key: FK column -> (real name -> type)

        public ResolvedField? Resolve(string field)
        {
            if (TryDecodeRelated(field, out var fk, out var refCol))
            {
                if (!Relations.TryGetValue(fk, out var rel)) { return null; }
                if (!RefColumns.TryGetValue(rel.FkColumn, out var cols)) { return null; }
                var realRef = cols.Keys.FirstOrDefault(k => string.Equals(k, refCol, StringComparison.OrdinalIgnoreCase));
                if (realRef is null) { return null; }
                return new ResolvedField { Header = $"{rel.Label}.{realRef}", RealColumn = realRef, Relation = rel };
            }
            var realBase = BaseColumns.Keys.FirstOrDefault(k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
            return realBase is null ? null : new ResolvedField { Header = realBase, RealColumn = realBase };
        }
    }

    // Builds a field resolver entirely from the cached catalog (no DB round-trips).
    private static FieldResolver BuildResolver(ReportCatalog cat, string schema, string table)
    {
        var relations = (cat.RelationsByTable.TryGetValue(Key(schema, table), out var rels) ? rels : [])
            .ToDictionary(r => r.FkColumn, r => r, StringComparer.OrdinalIgnoreCase);
        var baseCols = (cat.ColumnsByTable.TryGetValue(Key(schema, table), out var bc) ? bc : [])
            .ToDictionary(c => c.Name, c => c.DataType, StringComparer.OrdinalIgnoreCase);
        var refCols = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in relations.Values)
        {
            refCols[rel.FkColumn] = (cat.ColumnsByTable.TryGetValue(Key(rel.RefSchema, rel.RefTable), out var rc) ? rc : [])
                .ToDictionary(c => c.Name, c => c.DataType, StringComparer.OrdinalIgnoreCase);
        }
        return new FieldResolver { BaseColumns = baseCols, Relations = relations, RefColumns = refCols };
    }

    // ---- WHERE builder: operators from a fixed map, values always parameterised ----
    private static void BuildWhere(DbCommand cmd, StringBuilder sql, FilterMatch match,
        List<(ResolvedField Field, FilterOperator Op, string? Value)> filters)
    {
        var clauses = new List<string>();
        var p = 0;
        foreach (var (field, op, value) in filters)
        {
            var col = field.Relation is null
                ? $"{BaseAlias}.{Bracket(field.RealColumn)}"
                : $"{JoinAlias(field.Relation.FkColumn)}.{Bracket(field.RealColumn)}";
            var textCol = $"CONVERT(nvarchar(4000), {col})";
            var v = value ?? "";
            switch (op)
            {
                case FilterOperator.IsEmpty: clauses.Add($"({col} IS NULL OR {textCol} = '')"); break;
                case FilterOperator.IsNotEmpty: clauses.Add($"({col} IS NOT NULL AND {textCol} <> '')"); break;
                case FilterOperator.Equals: clauses.Add($"{textCol} = {Param(cmd, ref p, v)}"); break;
                case FilterOperator.NotEquals: clauses.Add($"{textCol} <> {Param(cmd, ref p, v)}"); break;
                case FilterOperator.Contains: clauses.Add($"{textCol} LIKE {Param(cmd, ref p, "%" + EscapeLike(v) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.NotContains: clauses.Add($"{textCol} NOT LIKE {Param(cmd, ref p, "%" + EscapeLike(v) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.StartsWith: clauses.Add($"{textCol} LIKE {Param(cmd, ref p, EscapeLike(v) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.GreaterThan: clauses.Add($"{col} > {Param(cmd, ref p, v)}"); break;
                case FilterOperator.LessThan: clauses.Add($"{col} < {Param(cmd, ref p, v)}"); break;
            }
        }
        if (clauses.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(match == FilterMatch.Or ? " OR " : " AND ", clauses));
        }
    }

    private const string BaseAlias = "[t0]";
    private static string JoinAlias(string fkColumn) => "[j_" + fkColumn.Replace("]", "]]") + "]";

    private static string Param(DbCommand cmd, ref int i, string value)
    {
        var name = $"@p{i++}";
        AddParam(cmd, name, value);
        return name;
    }

    private static async Task OpenAsync(DbConnection conn, CancellationToken ct)
    {
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); }
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name; p.DbType = DbType.String; p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static void AddParam(DbCommand cmd, string name, int value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name; p.DbType = DbType.Int32; p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static string Bracket(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private static string Format(object value) => value switch
    {
        null or DBNull => "",
        bool b => b ? "Yes" : "No",
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm"),
        byte[] => "(binary)",
        decimal or double or float => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? "",
    };
}
