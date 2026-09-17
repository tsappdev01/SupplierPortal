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

public record ReportData(List<string> Headers, List<string[]> Rows, bool Truncated);

/// <summary>Read-only database metadata browser and safe query executor for the Report Builder. Every table,
/// view and column an administrator picks is re-validated against live INFORMATION_SCHEMA metadata; identifiers
/// are taken only from that metadata and bracket-quoted, and all filter values are passed as parameters — so a
/// report definition can never inject SQL or reach an object the database does not expose. Obviously-sensitive
/// columns (passwords, secrets, hashes, tokens) and binary/blob columns are hidden from selection.</summary>
public class ReportSchemaService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private const int MaxColumns = 40;
    private const int DefaultMaxRows = 5000;
    private const int HardMaxRows = 20000;

    // Schemas never offered as report sources.
    private static readonly HashSet<string> BlockedSchemas =
        new(StringComparer.OrdinalIgnoreCase) { "sys", "INFORMATION_SCHEMA", "guest" };

    // Tables never offered.
    private static readonly HashSet<string> BlockedTables =
        new(StringComparer.OrdinalIgnoreCase) { "__EFMigrationsHistory" };

    // Column-name fragments that mark a column as sensitive (hidden from selection).
    private static readonly string[] SensitiveColumnMarkers =
        ["password", "pwd", "secret", "salt", "hash", "token", "apikey", "api_key", "connectionstring"];

    // SQL data types excluded from selection (binary / blob / rowversion).
    private static readonly HashSet<string> BinaryTypes =
        new(StringComparer.OrdinalIgnoreCase) { "varbinary", "binary", "image", "timestamp", "rowversion", "geography", "geometry", "hierarchyid" };

    public async Task<List<ReportTable>> GetTablesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
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
        if (!await TableExistsAsync(conn, schema, table, ct)) { return []; }

        await using var cmd = conn.CreateCommand();
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

    /// <summary>Validates a definition's source and columns against live metadata; returns an error message
    /// when the table/view is missing or any selected column is unknown or not selectable.</summary>
    public async Task<string?> ValidateAsync(string schema, string table, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(table)) { return "Select a table or view."; }
        if (columns.Count == 0) { return "Select at least one column."; }
        if (columns.Count > MaxColumns) { return $"Select at most {MaxColumns} columns."; }

        var allowed = (await GetColumnsAsync(schema, table, ct)).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0) { return "The selected table or view is not available."; }
        var unknown = columns.Where(c => !allowed.Contains(c)).ToList();
        return unknown.Count > 0 ? $"Unknown or unavailable column(s): {string.Join(", ", unknown)}." : null;
    }

    /// <summary>Runs the report query for a definition and returns the header row + data rows as strings.</summary>
    public async Task<ReportData> RunAsync(ReportDefinition def, int maxRows = DefaultMaxRows, CancellationToken ct = default)
    {
        maxRows = Math.Clamp(maxRows, 1, HardMaxRows);
        var columns = def.GetColumns();
        var error = await ValidateAsync(def.SchemaName, def.TableName, columns, ct);
        if (error is not null) { throw new InvalidOperationException(error); }

        // Re-fetch the validated, type-aware column set (used to bracket only real columns).
        var allowed = (await GetColumnsAsync(def.SchemaName, def.TableName, ct))
            .ToDictionary(c => c.Name, c => c.DataType, StringComparer.OrdinalIgnoreCase);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await OpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();

        var select = string.Join(", ", columns.Select(c => Bracket(allowed.Keys.First(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase)))));
        var sql = new StringBuilder($"SELECT TOP (@__max) {select} FROM {Bracket(def.SchemaName)}.{Bracket(def.TableName)}");

        AddParam(cmd, "@__max", maxRows + 1); // fetch one extra to detect truncation
        BuildWhere(cmd, sql, def.GetFilters(), allowed);
        cmd.CommandText = sql.ToString();
        cmd.CommandTimeout = 60;

        var headers = columns.ToList();
        var rows = new List<string[]>();
        var truncated = false;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (rows.Count >= maxRows) { truncated = true; break; }
            var row = new string[headers.Count];
            for (var i = 0; i < headers.Count; i++) { row[i] = Format(r.GetValue(i)); }
            rows.Add(row);
        }
        return new ReportData(headers, rows, truncated);
    }

    // ---- WHERE builder: operators from a fixed map, values always parameterised ----
    private static void BuildWhere(DbCommand cmd, StringBuilder sql, ReportFilterModel filters, Dictionary<string, string> allowed)
    {
        var clauses = new List<string>();
        var p = 0;
        foreach (var cond in filters.Conditions)
        {
            if (string.IsNullOrWhiteSpace(cond.Column) ||
                !allowed.Keys.Any(k => string.Equals(k, cond.Column, StringComparison.OrdinalIgnoreCase)))
            {
                continue; // ignore filters on unknown columns
            }
            var realName = allowed.Keys.First(k => string.Equals(k, cond.Column, StringComparison.OrdinalIgnoreCase));
            var col = Bracket(realName);
            var textCol = $"CONVERT(nvarchar(4000), {col})";
            var val = cond.Value ?? "";
            switch (cond.Operator)
            {
                case FilterOperator.IsEmpty:
                    clauses.Add($"({col} IS NULL OR {textCol} = '')"); break;
                case FilterOperator.IsNotEmpty:
                    clauses.Add($"({col} IS NOT NULL AND {textCol} <> '')"); break;
                case FilterOperator.Equals:
                    clauses.Add($"{textCol} = {Param(cmd, ref p, val)}"); break;
                case FilterOperator.NotEquals:
                    clauses.Add($"{textCol} <> {Param(cmd, ref p, val)}"); break;
                case FilterOperator.Contains:
                    clauses.Add($"{textCol} LIKE {Param(cmd, ref p, "%" + EscapeLike(val) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.NotContains:
                    clauses.Add($"{textCol} NOT LIKE {Param(cmd, ref p, "%" + EscapeLike(val) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.StartsWith:
                    clauses.Add($"{textCol} LIKE {Param(cmd, ref p, EscapeLike(val) + "%")} ESCAPE '\\'"); break;
                case FilterOperator.GreaterThan:
                    clauses.Add($"{col} > {Param(cmd, ref p, val)}"); break;
                case FilterOperator.LessThan:
                    clauses.Add($"{col} < {Param(cmd, ref p, val)}"); break;
            }
        }
        if (clauses.Count > 0)
        {
            var joiner = filters.Match == FilterMatch.Or ? " OR " : " AND ";
            sql.Append(" WHERE ").Append(string.Join(joiner, clauses));
        }
    }

    private static string Param(DbCommand cmd, ref int i, string value)
    {
        var name = $"@p{i++}";
        AddParam(cmd, name, value);
        return name;
    }

    private static async Task<bool> TableExistsAsync(DbConnection conn, string schema, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t";
        AddParam(cmd, "@s", schema);
        AddParam(cmd, "@t", table);
        if (BlockedSchemas.Contains(schema) || BlockedTables.Contains(table)) { return false; }
        var n = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return n > 0;
    }

    private static async Task OpenAsync(DbConnection conn, CancellationToken ct)
    {
        if (conn.State != ConnectionState.Open) { await conn.OpenAsync(ct); }
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.String;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static void AddParam(DbCommand cmd, string name, int value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.Int32;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    // Bracket-quote an identifier, escaping any embedded ] (defence in depth — names come from metadata).
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
