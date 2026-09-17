using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services.Reporting;

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
        var schemaService = scope.ServiceProvider.GetRequiredService<ReportSchemaService>();
        var renderService = scope.ServiceProvider.GetRequiredService<ReportRenderService>();

        foreach (var report in due)
        {
            if (ct.IsCancellationRequested) { break; }
            try
            {
                var (status, detail) = await RunOneAsync(
                    report, supplierService, exportService, schemaService, renderService, dispatcher, ct);
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

    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static async Task<(string Status, string Detail)> RunOneAsync(
        ScheduledReport report, SupplierService supplierService, ExportService exportService,
        ReportSchemaService schemaService, ReportRenderService renderService,
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

        var today = ReportTimeZone.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var attachments = new List<(byte[] Bytes, string FileName, string ContentType)>();
        string reportName, coverage;
        int rowCount;

        if (report.ReportDefinition is { } def)
        {
            // Definition-based: run the safe query and render the exact chosen columns.
            var data = await schemaService.RunAsync(def, ct: ct);
            rowCount = data.Rows.Count;
            reportName = def.Name;
            coverage = $"{def.SchemaName}.{def.TableName}";
            var baseName = SafeFileName(def.Name);
            if (def.Format is ReportFormat.Pdf or ReportFormat.PdfAndExcel)
            {
                attachments.Add((renderService.RenderPdf(def.Title, data.Headers, data.Rows, data.Truncated),
                    $"{baseName} - {today}.pdf", "application/pdf"));
            }
            if (def.Format is ReportFormat.Excel or ReportFormat.PdfAndExcel)
            {
                attachments.Add((renderService.RenderExcel(def.Title, data.Headers, data.Rows),
                    $"{baseName} - {today}.xlsx", XlsxType));
            }
        }
        else
        {
            // Legacy fixed supplier export (schedules created before the Report Builder).
            var buScope = report.BusinessUnitId is { } buId ? new List<int> { buId } : null;
            var suppliers = await supplierService.GetSuppliersAsync(buScope);
            rowCount = suppliers.Count;
            reportName = report.Name;
            coverage = report.BusinessUnitId is null ? "all business units" : "the selected business unit";
            var baseName = SafeFileName(report.Name);
            if (report.Format is ReportFormat.Pdf or ReportFormat.PdfAndExcel)
            {
                attachments.Add((exportService.ExportToPdf(suppliers), $"{baseName} - {today}.pdf", "application/pdf"));
            }
            if (report.Format is ReportFormat.Excel or ReportFormat.PdfAndExcel)
            {
                attachments.Add((exportService.ExportToExcelFull(suppliers), $"{baseName} - {today}.xlsx", XlsxType));
            }
        }

        if (attachments.Count == 0)
        {
            return ("Failed", "No report file could be generated.");
        }

        var subject = $"{reportName} — {today}";
        var html = BuildBody(reportName, coverage, rowCount, today);

        var sent = 0;
        var failures = new List<string>();
        foreach (var to in recipients)
        {
            if (ct.IsCancellationRequested) { break; }
            var (ok, error) = await dispatcher.SendHtmlWithAttachmentsAsync(to, subject, html, attachments);
            if (ok) { sent++; }
            else { failures.Add($"{to}: {error}"); }
        }

        if (sent == 0)
        {
            return ("Failed", $"No emails sent to {recipients.Count} recipient(s). " + string.Join("; ", failures));
        }
        var detail = $"Sent to {sent}/{recipients.Count} member(s) of '{group.Name}' ({rowCount} row(s)).";
        if (failures.Count > 0)
        {
            detail += " Failures: " + string.Join("; ", failures);
        }
        return (failures.Count == 0 ? "Sent" : "Partial", detail);
    }

    private static string SafeFileName(string name)
    {
        var cleaned = new string((name ?? "Report").Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Report" : cleaned;
    }

    private static string BuildBody(string reportName, string coverage, int rowCount, string today)
    {
        return $@"<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937;"">
    <p>Dear recipient,</p>
    <p>Please find attached the scheduled report <strong>{System.Net.WebUtility.HtmlEncode(reportName)}</strong>
       generated on <strong>{today}</strong>.</p>
    <ul>
        <li>Source: {System.Net.WebUtility.HtmlEncode(coverage)}</li>
        <li>Rows: {rowCount}</li>
    </ul>
    <p style=""color:#6b7280;font-size:12px;"">This is an automated message from the Supplier Portal. Please do not reply.</p>
</div>";
    }
}
