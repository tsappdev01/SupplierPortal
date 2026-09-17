using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>How often a <see cref="ScheduledReport"/> runs.</summary>
public enum ReportFrequency
{
    /// <summary>Runs exactly once at the scheduled time, then disables itself.</summary>
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5,
}

/// <summary>The file format the report is generated in and attached to the email.</summary>
public enum ReportFormat
{
    Pdf = 0,
    Excel = 1,
    /// <summary>Both a PDF and an Excel file are generated and attached.</summary>
    PdfAndExcel = 2,
}

/// <summary>An administrator-defined schedule that generates a supplier report and emails it as a file
/// attachment to the members of a notification group. The runner (<c>ScheduledReportRunner</c>) fires
/// a schedule when <see cref="NextRunUtc"/> is due, then advances it according to <see cref="Frequency"/>
/// (a <see cref="ReportFrequency.Once"/> schedule disables itself after running). Only <see cref="IsActive"/>
/// schedules fire, and only when their target group is itself active.</summary>
public class ScheduledReport
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The report content/format that is generated and attached.</summary>
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;

    /// <summary>How often the schedule repeats.</summary>
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Monthly;

    /// <summary>The next time (UTC) the report is due to run. Computed from the admin-entered local
    /// date/time on save, and advanced by one interval after each run.</summary>
    public DateTime NextRunUtc { get; set; }

    /// <summary>When false, the schedule is kept but never fires.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional business-unit filter (legacy supplier report only). When set, the report includes
    /// only that BU's suppliers; when null, all suppliers. Ignored for definition-based reports, whose data,
    /// columns and filters come from the <see cref="ReportDefinition"/>.</summary>
    public int? BusinessUnitId { get; set; }

    /// <summary>The reusable report definition (data source, columns, filters, title, format) this schedule
    /// runs. When set, the runner builds the report from the definition; when null, it falls back to the
    /// legacy fixed supplier export.</summary>
    public int? ReportDefinitionId { get; set; }
    public ReportDefinition? ReportDefinition { get; set; }

    /// <summary>The notification group whose active members receive the report. The group must itself be
    /// active at run time for the report to be sent.</summary>
    public int NotificationGroupId { get; set; }
    public NotificationGroup? NotificationGroup { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string? CreatedBy { get; set; }

    /// <summary>When the schedule last attempted to run (UTC), or null if it never has.</summary>
    public DateTime? LastRunUtc { get; set; }

    /// <summary>Outcome of the last run (e.g. "Sent", "Skipped", "Failed").</summary>
    [MaxLength(40)]
    public string? LastRunStatus { get; set; }

    /// <summary>Human-readable detail about the last run (recipient count, error, or skip reason).</summary>
    [MaxLength(1000)]
    public string? LastRunDetail { get; set; }
}
