using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
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

/// <summary>Read-only database metadata browser and safe query executor for the Report Builder. Administrators
/// pick a table/view plus (optionally) columns reached through its foreign keys; every table, view, column and
/// relationship is re-validated against live metadata, identifiers are taken only from that metadata and
/// bracket-quoted, related tables are LEFT-joined only along real FK relationships, and all filter values are
/// parameterised — so a report definition can never inject SQL or reach an object the database does not expose.
/// Sensitive columns (passwords, secrets, hashes, tokens) and binary/blob columns are hidden.</summary>
public class ReportSchemaService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Separator in an encoded related-field key: "{FkColumn}→{RefColumn}".</summary>
    public const string RelSep = "→"; // →

    private const int MaxColumns = 60;
    private const int DefaultMaxRows = 5000;
    private const int HardMaxRows = 20000;
    private const int MetaTimeoutSeconds = 90;  // metadata queries can be slow on a loaded/serverless DB

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

    public async Task<List<ReportTable>> GetTablesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = MetaTimeoutSeconds;
        cmd.CommandText =
            "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_TYPE IN ('BASE TABLE','VIEW') ORDER BY TABLE_SCHEMA, TABLE_NAME";
        var list = new List<ReportTable>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var schema = r.GetString(0);
            var name = r.GetString(1);
            if (BlockedSchemas.Contains(schema) || BlockedTables.Contains(name)) { continue; }
            list.Add(new ReportTable(schema, name, string.Equals(r.GetString(2), "VIEW", StringComparison.OrdinalIgnoreCase)));
        }
        return list;
    }

    /// <summary>Selectable columns for a validated table/view, with sensitive and binary columns removed.</summary>
    public async Task<List<ReportColumn>> GetColumnsAsync(string schema, string table, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        return await GetColumnsAsync(conn, schema, table, ct);
    }

    private static async Task<List<ReportColumn>> GetColumnsAsync(DbConnection conn, string schema, string table, CancellationToken ct)
    {
        if (BlockedSchemas.Contains(schema) || BlockedTables.Contains(table)) { return []; }
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = MetaTimeoutSeconds;
        cmd.CommandText =
            "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t ORDER BY ORDINAL_POSITION";
        AddParam(cmd, "@s", schema);
        AddParam(cmd, "@t", table);
        var cols = new List<ReportColumn>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            var type = r.GetString(1);
            if (BinaryTypes.Contains(type)) { continue; }
            if (SensitiveColumnMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase))) { continue; }
            cols.Add(new ReportColumn(name, type));
        }
        return cols;
    }

    private static bool IsSelectable(string columnName, string dataType) =>
        !BinaryTypes.Contains(dataType)
        && !SensitiveColumnMarkers.Any(m => columnName.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>Fetches selectable columns for several tables in ONE query (avoids a round-trip per table).
    /// Returns a map keyed "schema|table" (case-insensitive).</summary>
    private static async Task<Dictionary<string, List<ReportColumn>>> FetchColumnsForTablesAsync(
        DbConnection conn, IReadOnlyList<(string Schema, string Table)> pairs, CancellationToken ct)
    {
        var result = new Dictionary<string, List<ReportColumn>>(StringComparer.OrdinalIgnoreCase);
        var distinct = pairs
            .Where(p => !BlockedSchemas.Contains(p.Schema) && !BlockedTables.Contains(p.Table))
            .Select(p => (p.Schema, p.Table))
            .Distinct()
            .ToList();
        if (distinct.Count == 0) { return result; }

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = MetaTimeoutSeconds;
        var ors = new List<string>();
        for (var i = 0; i < distinct.Count; i++)
        {
            ors.Add($"(TABLE_SCHEMA = @s{i} AND TABLE_NAME = @t{i})");
            AddParam(cmd, $"@s{i}", distinct[i].Schema);
            AddParam(cmd, $"@t{i}", distinct[i].Table);
        }
        cmd.CommandText =
            "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE " + string.Join(" OR ", ors) + " ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION";

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var schema = r.GetString(0);
            var table = r.GetString(1);
            var name = r.GetString(2);
            var type = r.GetString(3);
            if (!IsSelectable(name, type)) { continue; }
            var key = $"{schema}|{table}";
            (result.TryGetValue(key, out var list) ? list : result[key] = []).Add(new ReportColumn(name, type));
        }
        return result;
    }

    /// <summary>Loads everything the Report Builder needs for a source in as few round-trips as possible:
    /// the base table's columns, its FK relations, and every related table's columns — batched.</summary>
    public async Task<ReportSource> GetSourceAsync(string schema, string table, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);

        var relations = await GetRelationsAsync(conn, schema, table, ct);
        var pairs = new List<(string, string)> { (schema, table) };
        pairs.AddRange(relations.Select(rel => (rel.RefSchema, rel.RefTable)));
        var cols = await FetchColumnsForTablesAsync(conn, pairs, ct);

        var baseCols = cols.TryGetValue($"{schema}|{table}", out var bc) ? bc : [];
        var related = relations
            .Select(rel => (Rel: rel, Cols: cols.TryGetValue($"{rel.RefSchema}|{rel.RefTable}", out var rc) ? rc : []))
            .Where(x => x.Cols.Count > 0)
            .ToList();
        return new ReportSource(baseCols, related);
    }

    /// <summary>The single-column foreign-key relationships from a base table to other tables (empty for views).</summary>
    public async Task<List<ReportRelation>> GetRelationsAsync(string schema, string table, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        return await GetRelationsAsync(conn, schema, table, ct);
    }

    private static async Task<List<ReportRelation>> GetRelationsAsync(DbConnection conn, string schema, string table, CancellationToken ct)
    {
        if (BlockedSchemas.Contains(schema) || BlockedTables.Contains(table)) { return []; }
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = MetaTimeoutSeconds;
        // One row per FK column; multi-column FKs are grouped out afterwards (only single-column FKs are used).
        cmd.CommandText = @"
SELECT fk.name AS FkName, pc.name AS FkColumn, rs.name AS RefSchema, rt.name AS RefTable, rc.name AS RefColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
WHERE ps.name = @s AND pt.name = @t
ORDER BY fk.name";
        AddParam(cmd, "@s", schema);
        AddParam(cmd, "@t", table);

        var byConstraint = new Dictionary<string, List<ReportRelation>>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var fkName = r.GetString(0);
                var fkColumn = r.GetString(1);
                var refSchema = r.GetString(2);
                var refTable = r.GetString(3);
                var refColumn = r.GetString(4);
                if (BlockedSchemas.Contains(refSchema) || BlockedTables.Contains(refTable)) { continue; }
                (byConstraint.TryGetValue(fkName, out var l) ? l : byConstraint[fkName] = [])
                    .Add(new ReportRelation(fkColumn, refSchema, refTable, refColumn, Label(fkColumn)));
            }
        }

        // Keep only single-column FKs; de-duplicate on the FK column (its identity in the field key).
        var result = new List<ReportRelation>();
        var seenFk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in byConstraint.Values.Where(g => g.Count == 1))
        {
            var rel = group[0];
            if (seenFk.Add(rel.FkColumn)) { result.Add(rel); }
        }
        return result.OrderBy(r => r.Label).ToList();
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

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        var resolver = await BuildResolverAsync(conn, schema, table, ct);
        if (resolver.BaseColumns.Count == 0) { return "The selected table or view is not available."; }

        var unknown = columns.Where(c => resolver.Resolve(c) is null).ToList();
        return unknown.Count > 0 ? $"Unknown or unavailable field(s): {string.Join(", ", unknown)}." : null;
    }

    public async Task<ReportData> RunAsync(ReportDefinition def, int maxRows = DefaultMaxRows, CancellationToken ct = default)
    {
        maxRows = Math.Clamp(maxRows, 1, HardMaxRows);
        var columns = def.GetColumns();
        if (columns.Count == 0) { throw new InvalidOperationException("The report has no columns."); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        var resolver = await BuildResolverAsync(conn, def.SchemaName, def.TableName, ct);
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

    private static async Task<FieldResolver> BuildResolverAsync(DbConnection conn, string schema, string table, CancellationToken ct)
    {
        var relationList = await GetRelationsAsync(conn, schema, table, ct);
        var relations = relationList.ToDictionary(r => r.FkColumn, r => r, StringComparer.OrdinalIgnoreCase);

        // Fetch base + all referenced tables' columns in a single query.
        var pairs = new List<(string, string)> { (schema, table) };
        pairs.AddRange(relationList.Select(rel => (rel.RefSchema, rel.RefTable)));
        var colsByTable = await FetchColumnsForTablesAsync(conn, pairs, ct);

        var baseCols = (colsByTable.TryGetValue($"{schema}|{table}", out var bc) ? bc : [])
            .ToDictionary(c => c.Name, c => c.DataType, StringComparer.OrdinalIgnoreCase);
        var refCols = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in relations.Values)
        {
            refCols[rel.FkColumn] = (colsByTable.TryGetValue($"{rel.RefSchema}|{rel.RefTable}", out var rc) ? rc : [])
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
