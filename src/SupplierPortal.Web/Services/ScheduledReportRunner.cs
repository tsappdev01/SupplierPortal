using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Background service that runs administrator-defined scheduled reports. On each poll it finds the
/// active schedules whose next-run time is due, generates the supplier report as a PDF (or Excel) file, and
/// emails it as an attachment to the active members of the schedule's notification group — but only when that
/// group is itself active. After each schedule runs (or is skipped) it is advanced to its next occurrence.
/// Controlled by <c>Reports:SchedulerEnabled</c> (default true) and <c>Reports:PollSeconds</c> (default 60).</summary>
public class ScheduledReportRunner(
    IServiceProvider services, IConfiguration config, ILogger<ScheduledReportRunner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Reports:SchedulerEnabled", true))
        {
            logger.LogInformation("Scheduled report runner disabled (Reports:SchedulerEnabled=false).");
            return;
        }

        var pollSeconds = Math.Max(15, config.GetValue("Reports:PollSeconds", 60));
        var interval = TimeSpan.FromSeconds(pollSeconds);
        logger.LogInformation("Scheduled report runner started; polling every {Seconds}s ({Zone}).",
            pollSeconds, ReportTimeZone.DisplayName);

        // Let startup (migrations/seeding) settle before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled report sweep failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        using var scope = services.CreateScope();
        var scheduleService = scope.ServiceProvider.GetRequiredService<ScheduledReportService>();

        var due = await scheduleService.GetDueAsync(nowUtc);
        if (due.Count == 0)
        {
            return;
        }

        var supplierService = scope.ServiceProvider.GetRequiredService<SupplierService>();
        var exportService = scope.ServiceProvider.GetRequiredService<ExportService>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();

        foreach (var report in due)
        {
            if (ct.IsCancellationRequested) { break; }
            try
            {
                var (status, detail) = await RunOneAsync(report, supplierService, exportService, dispatcher, ct);
                await scheduleService.RecordRunAsync(report.Id, status, detail, nowUtc);
                logger.LogInformation("Scheduled report '{Name}' (#{Id}): {Status} — {Detail}",
                    report.Name, report.Id, status, detail);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled report '{Name}' (#{Id}) failed.", report.Name, report.Id);
                await scheduleService.RecordRunAsync(report.Id, "Failed", ex.Message, nowUtc);
            }
        }
    }

    private static async Task<(string Status, string Detail)> RunOneAsync(
        ScheduledReport report, SupplierService supplierService, ExportService exportService,
        EmailDispatcher dispatcher, CancellationToken ct)
    {
        var group = report.NotificationGroup;
        if (group is null || !group.IsActive)
        {
            return ("Skipped", group is null ? "Notification group missing." : $"Group '{group.Name}' is disabled.");
        }

        var recipients = group.Users
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => u.Email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipients.Count == 0)
        {
            return ("Skipped", $"Group '{group.Name}' has no active members with an email address.");
        }

        // Scope suppliers to the schedule's business unit, or all suppliers when none is set.
        var buScope = report.BusinessUnitId is { } buId ? new List<int> { buId } : null;
        var suppliers = await supplierService.GetSuppliersAsync(buScope);

        var today = ReportTimeZone.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd");
        byte[] bytes;
        string fileName, contentType;
        if (report.Format == ReportFormat.Excel)
        {
            bytes = exportService.ExportToExcelFull(suppliers);
            fileName = $"Supplier Report - {today}.xlsx";
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }
        else
        {
            bytes = exportService.ExportToPdf(suppliers);
            fileName = $"Supplier Report - {today}.pdf";
            contentType = "application/pdf";
        }

        var subject = $"{report.Name} — {today}";
        var html = BuildBody(report, suppliers.Count, today);

        var sent = 0;
        var failures = new List<string>();
        foreach (var to in recipients)
        {
            if (ct.IsCancellationRequested) { break; }
            var (ok, error) = await dispatcher.SendHtmlWithAttachmentAsync(to, subject, html, bytes, fileName, contentType);
            if (ok) { sent++; }
            else { failures.Add($"{to}: {error}"); }
        }

        if (sent == 0)
        {
            return ("Failed", $"No emails sent to {recipients.Count} recipient(s). " + string.Join("; ", failures));
        }
        var detail = $"Sent to {sent}/{recipients.Count} member(s) of '{group.Name}' ({suppliers.Count} supplier record(s)).";
        if (failures.Count > 0)
        {
            detail += " Failures: " + string.Join("; ", failures);
        }
        return (failures.Count == 0 ? "Sent" : "Partial", detail);
    }

    private static string BuildBody(ScheduledReport report, int supplierCount, string today)
    {
        var scope = report.BusinessUnitId is null ? "all business units" : "the selected business unit";
        return $@"<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;"">
    <p>Dear recipient,</p>
    <p>Please find attached the scheduled supplier report <strong>{System.Net.WebUtility.HtmlEncode(report.Name)}</strong>
       generated on <strong>{today}</strong>.</p>
    <ul>
        <li>Coverage: {scope}</li>
        <li>Supplier records: {supplierCount}</li>
        <li>Format: {report.Format.ToString().ToUpperInvariant()}</li>
    </ul>
    <p style=""color:#6b7280;font-size:12px;"">This is an automated message from the Supplier Portal. Please do not reply.</p>
</div>";
    }
}
