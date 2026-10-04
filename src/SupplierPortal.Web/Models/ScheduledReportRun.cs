using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>One historical execution of a <see cref="ScheduledReport"/> — used to show a per-report run
/// history timeline. Written each time the runner (or a "Run now") completes.</summary>
public class ScheduledReportRun
{
    public int Id { get; set; }

    public int ScheduledReportId { get; set; }
    public ScheduledReport? ScheduledReport { get; set; }

    public DateTime RunAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Outcome: Sent / Partial / Skipped / Failed.</summary>
    [MaxLength(40)]
    public string Status { get; set; } = "";

    [MaxLength(1000)]
    public string? Detail { get; set; }
}
