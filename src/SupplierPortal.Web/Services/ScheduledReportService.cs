using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>The editable fields of a scheduled report. <see cref="StartLocal"/> is the administrator's
/// local wall-clock date/time for the first (or next) run; it is converted to UTC on save.</summary>
public record ScheduledReportInput(
    string Name, ReportFormat Format, ReportFrequency Frequency, DateTime StartLocal,
    int? BusinessUnitId, int NotificationGroupId, bool IsActive);

/// <summary>CRUD and scheduling logic for administrator-defined scheduled supplier reports. The next-run
/// time is stored in UTC and advanced by one interval after each run; a <see cref="ReportFrequency.Once"/>
/// schedule disables itself once it has run.</summary>
public class ScheduledReportService(
    IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    public async Task<List<ScheduledReport>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ScheduledReports
            .Include(r => r.NotificationGroup)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<ScheduledReport?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ScheduledReports.Include(r => r.NotificationGroup).FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<(bool Success, string? Error, int Id)> CreateAsync(ScheduledReportInput input, string actor)
    {
        var name = (input.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Report name is required.", 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.NotificationGroups.AnyAsync(g => g.Id == input.NotificationGroupId))
        {
            return (false, "Select a valid notification group.", 0);
        }

        var report = new ScheduledReport
        {
            Name = name,
            Format = input.Format,
            Frequency = input.Frequency,
            BusinessUnitId = input.BusinessUnitId,
            NotificationGroupId = input.NotificationGroupId,
            IsActive = input.IsActive,
            NextRunUtc = ReportTimeZone.ToUtc(input.StartLocal),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        db.ScheduledReports.Add(report);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Create, "ScheduledReport", report.Id,
            $"Created scheduled report '{report.Name}' ({report.Frequency}, {report.Format}); next run {report.NextRunUtc:u}.");
        return (true, null, report.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, ScheduledReportInput input, string actor)
    {
        var name = (input.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Report name is required.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
        {
            return (false, "Scheduled report not found.");
        }
        if (!await db.NotificationGroups.AnyAsync(g => g.Id == input.NotificationGroupId))
        {
            return (false, "Select a valid notification group.");
        }

        report.Name = name;
        report.Format = input.Format;
        report.Frequency = input.Frequency;
        report.BusinessUnitId = input.BusinessUnitId;
        report.NotificationGroupId = input.NotificationGroupId;
        report.IsActive = input.IsActive;
        report.NextRunUtc = ReportTimeZone.ToUtc(input.StartLocal);

        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Update, "ScheduledReport", report.Id,
            $"Updated scheduled report '{report.Name}' ({report.Frequency}, {report.Format}); next run {report.NextRunUtc:u}.");
        return (true, null);
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null || report.IsActive == isActive)
        {
            return false;
        }

        report.IsActive = isActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Update, "ScheduledReport", report.Id,
            $"{(isActive ? "Enabled" : "Disabled")} scheduled report '{report.Name}'.");
        return true;
    }

    /// <summary>Queues an immediate run by setting the next-run time to now (and re-enabling the schedule),
    /// so the background runner picks it up on its next poll. Useful for testing a schedule on demand.</summary>
    public async Task<bool> TriggerNowAsync(int id, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
        {
            return false;
        }

        report.IsActive = true;
        report.NextRunUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Update, "ScheduledReport", report.Id,
            $"Queued scheduled report '{report.Name}' to run now.");
        return true;
    }

    public async Task<bool> DeleteAsync(int id, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
        {
            return false;
        }

        db.ScheduledReports.Remove(report);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Delete, "ScheduledReport", report.Id,
            $"Deleted scheduled report '{report.Name}'.");
        return true;
    }

    /// <summary>The active schedules that are due to run at <paramref name="nowUtc"/>, with their group and
    /// members loaded. Used by the background runner.</summary>
    public async Task<List<ScheduledReport>> GetDueAsync(DateTime nowUtc)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ScheduledReports
            .Include(r => r.NotificationGroup!).ThenInclude(g => g.Users)
            .Where(r => r.IsActive && r.NextRunUtc <= nowUtc)
            .ToListAsync();
    }

    /// <summary>Records a run's outcome and advances the schedule to its next occurrence. A one-off schedule
    /// is disabled after it runs; a recurring schedule's next-run is advanced past <paramref name="nowUtc"/>
    /// (skipping any missed occurrences so a downtime doesn't cause a burst of sends).</summary>
    public async Task RecordRunAsync(int id, string status, string detail, DateTime nowUtc)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
        {
            return;
        }

        report.LastRunUtc = nowUtc;
        report.LastRunStatus = status;
        report.LastRunDetail = detail.Length > 1000 ? detail[..1000] : detail;

        if (report.Frequency == ReportFrequency.Once)
        {
            report.IsActive = false;   // one-off schedules run once, then stop
        }
        else
        {
            report.NextRunUtc = AdvancePast(report.NextRunUtc, report.Frequency, nowUtc);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Advances a next-run time by whole intervals until it is strictly after <paramref name="afterUtc"/>.</summary>
    public static DateTime AdvancePast(DateTime nextRunUtc, ReportFrequency frequency, DateTime afterUtc)
    {
        var next = nextRunUtc;
        // Guard against a pathological loop if a schedule is far in the past.
        for (var i = 0; i < 5000 && next <= afterUtc; i++)
        {
            next = frequency switch
            {
                ReportFrequency.Daily => next.AddDays(1),
                ReportFrequency.Weekly => next.AddDays(7),
                ReportFrequency.Monthly => next.AddMonths(1),
                _ => afterUtc.AddYears(100),   // Once: push far out (it is also disabled)
            };
        }
        return next;
    }
}
