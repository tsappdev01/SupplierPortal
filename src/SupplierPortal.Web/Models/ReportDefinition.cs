using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SupplierPortal.Web.Models;

/// <summary>How a single filter condition compares a column's value.</summary>
public enum FilterOperator
{
    Equals = 0,
    NotEquals = 1,
    Contains = 2,
    NotContains = 3,
    StartsWith = 4,
    GreaterThan = 5,
    LessThan = 6,
    IsEmpty = 7,
    IsNotEmpty = 8,
}

/// <summary>Whether all filter conditions must match (AND) or any (OR).</summary>
public enum FilterMatch
{
    And = 0,
    Or = 1,
}

/// <summary>One filter condition: a column, an operator, and a comparison value (unused for IsEmpty/IsNotEmpty).</summary>
public class ReportFilterCondition
{
    public string Column { get; set; } = "";
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;
    public string? Value { get; set; }
}

/// <summary>The filter set of a report definition: a match mode plus zero or more conditions.</summary>
public class ReportFilterModel
{
    public FilterMatch Match { get; set; } = FilterMatch.And;
    public List<ReportFilterCondition> Conditions { get; set; } = [];
}

/// <summary>A reusable report definition built with the Report Builder: a data source (schema + table/view),
/// an ordered list of columns, an optional filter set, a display title and an output format. Schedules
/// reference a definition so the data shape is defined once and reused. The schema/table/columns are always
/// re-validated against live database metadata at query time, and filter values are parameterised, so a
/// definition can never be used for SQL injection.</summary>
public class ReportDefinition
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The heading printed at the top of the generated report.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string SchemaName { get; set; } = "dbo";

    [Required, MaxLength(256)]
    public string TableName { get; set; } = string.Empty;

    /// <summary>Ordered column names, stored as a JSON array.</summary>
    [Required]
    public string ColumnsJson { get; set; } = "[]";

    /// <summary>Filter set, stored as JSON (nullable = no filters).</summary>
    public string? FiltersJson { get; set; }

    public ReportFormat Format { get; set; } = ReportFormat.Pdf;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string? CreatedBy { get; set; }

    // ----- Convenience (de)serialisation -----
    public List<string> GetColumns()
    {
        try { return JsonSerializer.Deserialize<List<string>>(ColumnsJson ?? "[]", Json) ?? []; }
        catch { return []; }
    }

    public void SetColumns(IEnumerable<string> columns) =>
        ColumnsJson = JsonSerializer.Serialize(columns.ToList(), Json);

    public ReportFilterModel GetFilters()
    {
        if (string.IsNullOrWhiteSpace(FiltersJson)) { return new ReportFilterModel(); }
        try { return JsonSerializer.Deserialize<ReportFilterModel>(FiltersJson, Json) ?? new ReportFilterModel(); }
        catch { return new ReportFilterModel(); }
    }

    public void SetFilters(ReportFilterModel filters) =>
        FiltersJson = filters.Conditions.Count == 0 ? null : JsonSerializer.Serialize(filters, Json);
}
