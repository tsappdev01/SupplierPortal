using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>The editable fields of a scheduled report. <see cref="StartLocal"/> is the administrator's
/// local wall-clock date/time for the first (or next) run; it is converted to UTC on save.</summary>
public record ScheduledReportInput(
    string Name, ReportFormat Format, ReportFrequency Frequency, DateTime StartLocal,
    int? BusinessUnitId, int NotificationGroupId, bool IsActive, int? ReportDefinitionId = null,
    int WeekDaysMask = 0);

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
            .Include(r => r.ReportDefinition)
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
            ReportDefinitionId = input.ReportDefinitionId,
            WeekDaysMask = input.Frequency == ReportFrequency.Weekly ? input.WeekDaysMask : 0,
            NextRunUtc = InitialNextRunUtc(input),
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
        report.ReportDefinitionId = input.ReportDefinitionId;
        report.WeekDaysMask = input.Frequency == ReportFrequency.Weekly ? input.WeekDaysMask : 0;
        report.NextRunUtc = InitialNextRunUtc(input);

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
            .Include(r => r.ReportDefinition)
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

        var trimmed = detail.Length > 1000 ? detail[..1000] : detail;
        report.LastRunUtc = nowUtc;
        report.LastRunStatus = status;
        report.LastRunDetail = trimmed;

        // Append to the run-history timeline.
        db.ScheduledReportRuns.Add(new ScheduledReportRun
        {
            ScheduledReportId = id, RunAtUtc = nowUtc, Status = status, Detail = trimmed
        });

        if (report.Frequency == ReportFrequency.Once)
        {
            report.IsActive = false;   // one-off schedules run once, then stop
        }
        else
        {
            report.NextRunUtc = AdvancePast(report.NextRunUtc, report.Frequency, nowUtc, report.WeekDaysMask);
        }

        await db.SaveChangesAsync();
    }

    // ----- Weekly-days helpers -----
    public static bool IsDaySelected(int mask, DayOfWeek day) => (mask & (1 << (int)day)) != 0;

    /// <summary>The first run time (UTC) for a schedule from its input: for a Weekly schedule with specific
    /// days, the first selected weekday on/after the chosen start (at the start's time of day); otherwise the
    /// start itself.</summary>
    private static DateTime InitialNextRunUtc(ScheduledReportInput input)
    {
        if (input.Frequency == ReportFrequency.Weekly && input.WeekDaysMask != 0)
        {
            var start = input.StartLocal;
            for (var i = 0; i < 14; i++)
            {
                var day = start.AddDays(i);
                if (IsDaySelected(input.WeekDaysMask, day.DayOfWeek))
                {
                    return ReportTimeZone.ToUtc(day);
                }
            }
        }
        return ReportTimeZone.ToUtc(input.StartLocal);
    }

    /// <summary>The most recent run-history rows for a schedule (newest first), for the timeline.</summary>
    public async Task<List<ScheduledReportRun>> GetRunsAsync(int reportId, int take = 8)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ScheduledReportRuns
            .Where(r => r.ScheduledReportId == reportId)
            .OrderByDescending(r => r.RunAtUtc)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>Duplicates a schedule (same definition, group, format, frequency and timing), disabled, with a
    /// "(copy)" name, so an administrator can tweak and enable it.</summary>
    public async Task<(bool Success, string? Error, int Id)> DuplicateAsync(int id, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var src = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id);
        if (src is null) { return (false, "Scheduled report not found.", 0); }

        var baseName = $"{src.Name} (copy)";
        var name = baseName;
        var n = 2;
        while (await db.ScheduledReports.AnyAsync(r => r.Name == name)) { name = $"{baseName} {n++}"; }

        var copy = new ScheduledReport
        {
            Name = name,
            Format = src.Format,
            Frequency = src.Frequency,
            NextRunUtc = src.NextRunUtc,
            IsActive = false,
            BusinessUnitId = src.BusinessUnitId,
            NotificationGroupId = src.NotificationGroupId,
            ReportDefinitionId = src.ReportDefinitionId,
            WeekDaysMask = src.WeekDaysMask,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        db.ScheduledReports.Add(copy);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Create, "ScheduledReport", copy.Id,
            $"Duplicated scheduled report '{src.Name}' as '{copy.Name}'.");
        return (true, null, copy.Id);
    }

    /// <summary>Enables or disables several schedules at once. Returns how many changed.</summary>
    public async Task<int> SetActiveManyAsync(IEnumerable<int> ids, bool isActive, string actor)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) { return 0; }
        await using var db = await dbFactory.CreateDbContextAsync();
        var reports = await db.ScheduledReports.Where(r => idList.Contains(r.Id) && r.IsActive != isActive).ToListAsync();
        foreach (var r in reports) { r.IsActive = isActive; }
        if (reports.Count > 0)
        {
            await db.SaveChangesAsync();
            await auditService.LogAsync(actor, AuditAction.Update, "ScheduledReport", null,
                $"{(isActive ? "Enabled" : "Disabled")} {reports.Count} scheduled report(s) in bulk.");
        }
        return reports.Count;
    }

    /// <summary>Queues several schedules to run now. Returns how many were queued.</summary>
    public async Task<int> TriggerManyAsync(IEnumerable<int> ids, string actor)
    {
        var idList = ids.Distinct().ToList();
        var count = 0;
        foreach (var id in idList)
        {
            if (await TriggerNowAsync(id, actor)) { count++; }
        }
        return count;
    }

    /// <summary>Advances a next-run time to its next occurrence strictly after <paramref name="afterUtc"/>.
    /// For a Weekly schedule with specific days (<paramref name="weekDaysMask"/> != 0), the next selected
    /// weekday at the same local time of day; otherwise whole intervals of the frequency.</summary>
    public static DateTime AdvancePast(DateTime nextRunUtc, ReportFrequency frequency, DateTime afterUtc, int weekDaysMask = 0)
    {
        if (frequency == ReportFrequency.Weekly && weekDaysMask != 0)
        {
            // Compute in the local zone so "which weekday" and "same time of day" are correct (UAE has no DST).
            var timeOfDay = ReportTimeZone.ToLocal(nextRunUtc).TimeOfDay;
            var afterLocal = ReportTimeZone.ToLocal(afterUtc);
            var day = afterLocal.Date + timeOfDay;
            for (var i = 0; i < 14; i++, day = day.AddDays(1))
            {
                if (day > afterLocal && IsDaySelected(weekDaysMask, day.DayOfWeek))
                {
                    return ReportTimeZone.ToUtc(day);
                }
            }
            return ReportTimeZone.ToUtc(afterLocal.Date.AddDays(7) + timeOfDay); // safety fallback
        }

        var next = nextRunUtc;
        // Guard against a pathological loop if a schedule is far in the past.
        for (var i = 0; i < 5000 && next <= afterUtc; i++)
        {
            next = frequency switch
            {
                ReportFrequency.Daily => next.AddDays(1),
                ReportFrequency.Weekly => next.AddDays(7),
                ReportFrequency.Monthly => next.AddMonths(1),
                ReportFrequency.Quarterly => next.AddMonths(3),
                ReportFrequency.Yearly => next.AddYears(1),
                _ => afterUtc.AddYears(100),   // Once: push far out (it is also disabled)
            };
        }
        return next;
    }
}
