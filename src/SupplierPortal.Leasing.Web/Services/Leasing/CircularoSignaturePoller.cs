using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Periodically asks Circularo for the status of every landlord registration that has been sent for signature
/// but is not yet resolved, and applies any completed signature — so the signed document lands in the portal
/// automatically without depending on the inbound completion webhook (which can't reach a portal that isn't
/// publicly addressable). Controlled by <see cref="CircularoOptions.PollIntervalMinutes"/> (0 disables it).
/// </summary>
public class CircularoSignaturePoller(
    IServiceProvider services, IOptions<CircularoOptions> options, ILogger<CircularoSignaturePoller> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled || o.PollIntervalMinutes <= 0)
        {
            logger.LogInformation("Circularo signature poller disabled (Enabled={Enabled}, PollIntervalMinutes={Mins}).",
                o.Enabled, o.PollIntervalMinutes);
            return;
        }

        var interval = TimeSpan.FromMinutes(o.PollIntervalMinutes);
        logger.LogInformation("Circularo signature poller started; interval {Mins} min.", o.PollIntervalMinutes);

        // Let startup (migrations/seeding) settle before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var landlordService = scope.ServiceProvider.GetRequiredService<LandlordService>();
                var changed = await landlordService.PollPendingSignaturesAsync();
                if (changed > 0)
                    logger.LogInformation("Circularo poll: {Count} registration(s) updated from signature status.", changed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Circularo signature poll sweep failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
