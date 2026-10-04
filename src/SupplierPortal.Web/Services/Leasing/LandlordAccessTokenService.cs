using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Validates the landlord self-service link token and drives OTP verification, mirroring
/// <see cref="SupplierAccessTokenService"/>: the opaque link token is looked up by a SHA-256 hash, and the
/// 4-digit OTP is verified against the salted <see cref="IPasswordHasher{TUser}"/>. Verification is
/// pluggable — OTP today, UAE&nbsp;Pass later. In Development (no Database Mail profile) the OTP is surfaced
/// to the caller instead of emailed so the flow is testable locally.
/// </summary>
public class LandlordAccessTokenService(
    IDbContextFactory<LeasingDbContext> dbFactory, IPasswordHasher<User> hasher,
    Microsoft.Extensions.Options.IOptions<LeasingOptions> options,
    SupplierPortal.Web.Services.EmailBrandingService branding)
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);
    private const int MaxOtpAttempts = 5;
    private const int MaxOtpSends = 5;
    private string MailProfileName => options.Value.MailProfile;

    private static readonly User HasherContext = new();

    /// <summary>Statuses in which the secure link may still be used. A completed or rejected registration —
    /// or one whose link has been explicitly deactivated — is closed (FRD §26).</summary>
    public static readonly LandlordStatus[] LinkUsableStatuses =
    [
        LandlordStatus.InvitationSent, LandlordStatus.OtpVerified, LandlordStatus.InProgress,
        LandlordStatus.Submitted, LandlordStatus.UnderReview, LandlordStatus.CorrectionRequired
    ];

    /// <summary>Outcome of resolving a link: the token when usable, or a reason it is not (expired vs closed).</summary>
    public enum LinkState { Valid, NotFound, Expired, Closed }

    public async Task<(LinkState State, LandlordAccessToken? Token)> ResolveTokenAsync(string rawToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hash = Hash(rawToken);
        var token = await db.LandlordAccessTokens.Include(t => t.Landlord)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token is null) return (LinkState.NotFound, null);
        if (token.ExpiresAt < DateTime.UtcNow) return (LinkState.Expired, null);
        if (token.Landlord is null || !token.Landlord.LinkActive || !LinkUsableStatuses.Contains(token.Landlord.Status))
            return (LinkState.Closed, null);
        return (LinkState.Valid, token);
    }

    public async Task<LandlordAccessToken?> GetValidTokenAsync(string rawToken)
    {
        var (state, token) = await ResolveTokenAsync(rawToken);
        return state == LinkState.Valid ? token : null;
    }

    public static bool NeedsFreshOtp(LandlordAccessToken t) =>
        t.OtpGeneratedAt is null || t.OtpExpiresAt is null || t.OtpExpiresAt < DateTime.UtcNow;

    /// <summary>Generates, persists and emails a fresh OTP via SQL Server Database Mail (profile
    /// <c>Leasing:MailProfile</c>, default <c>BP</c>) — unconditionally, in every environment, exactly like
    /// the supplier OTP. The <c>DevCode</c> element is always null (the code is only ever delivered by email).</summary>
    public async Task<(bool Success, string? Error, string? DevCode)> IssueOtpAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.Include(t => t.Landlord).FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null) return (false, "Link not found.", null);
        if (token.Landlord is null || string.IsNullOrWhiteSpace(token.Landlord.Email))
            return (false, "This landlord has no email on file.", null);
        if (token.OtpSendCount >= MaxOtpSends)
            return (false, "Too many verification code requests for this link. Please contact us for assistance.", null);

        var code = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");

        var who = System.Net.WebUtility.HtmlEncode(token.Landlord.CompanyName ?? token.Landlord.PropertyCode);
        var brand = System.Net.WebUtility.HtmlEncode(await branding.GetBusinessUnitNameAsync(token.Landlord.BusinessUnitId)
                                                     ?? EmailBrandingService.DefaultBrandName);
        var body =
$@"<!DOCTYPE html><html><body style=""margin:0;background:#06213D;font-family:Segoe UI,Arial,sans-serif"">
  <table role=""presentation"" width=""100%"" style=""background:#06213D;padding:24px 0""><tr><td align=""center"">
    <table role=""presentation"" width=""600"" style=""max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden"">
      <tr><td style=""background:#092F56;padding:20px 28px"">
        <div style=""color:#fff;font-size:18px;font-weight:700"">{brand}</div>
        <div style=""color:#9DB3C8;font-size:12px;letter-spacing:.08em;text-transform:uppercase;margin-top:2px"">Business Centre · Leasing</div>
      </td></tr>
      <tr><td style=""padding:24px 28px 8px"">
        <h1 style=""margin:0 0 10px;font-size:20px;color:#092F56"">Your verification code</h1>
        <p style=""margin:0 0 16px;color:#20262D"">Use this code to continue the landlord registration for <strong>{who}</strong>.</p>
        <div style=""font-family:Consolas,monospace;font-size:34px;letter-spacing:.4em;font-weight:700;color:#092F56;background:#F6F3EF;border-radius:6px;padding:16px 0;text-align:center"">{code}</div>
        <p style=""margin:14px 0 0;color:#8A929A;font-size:12px"">The code expires in {(int)OtpValidity.TotalMinutes} minutes. If you didn't request it, you can ignore this email.</p>
      </td></tr>
      <tr><td style=""padding:20px 28px;color:#8A929A;font-size:12px;border-top:1px solid #E2DCD4"">Automated message from {brand} Business Centre — please do not reply.</td></tr>
    </table>
  </td></tr></table>
</body></html>";
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {token.Landlord.Email},
                    @subject = {"Your Landlord Verification Code"},
                    @body = {body},
                    @body_format = 'HTML';");
        }
        catch (Exception ex)
        {
            return (false, $"Database Mail send failed: {ex.Message}", null);
        }

        // Delivery succeeded — only now record the code as "sent" and count it against the send cap. Persisting
        // before the send would make a failed send look successful (blocking the retry and hiding the error).
        token.OtpCodeHash = hasher.HashPassword(HasherContext, code);
        token.OtpGeneratedAt = DateTime.UtcNow;
        token.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        token.OtpAttemptCount = 0;
        token.OtpSendCount++;
        await db.SaveChangesAsync();

        return (true, null, null);
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(int tokenId, string enteredCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null) return (false, "Link not found.", null);
        if (token.OtpCodeHash is null || token.OtpExpiresAt is null || token.OtpExpiresAt < DateTime.UtcNow)
            return (false, "Your code has expired. Please request a new one.", null);
        if (token.OtpAttemptCount >= MaxOtpAttempts)
            return (false, "Too many incorrect attempts. Please request a new code.", null);

        var result = hasher.VerifyHashedPassword(HasherContext, token.OtpCodeHash, enteredCode.Trim());
        if (result == PasswordVerificationResult.Failed)
        {
            token.OtpAttemptCount++;
            await db.SaveChangesAsync();
            return (false, "Incorrect code. Please try again.", null);
        }

        var sessionRaw = GenerateRawToken();
        token.OtpVerifiedAt = DateTime.UtcNow;
        token.SessionTokenHash = Hash(sessionRaw);
        token.SessionExpiresAt = token.ExpiresAt;
        await db.SaveChangesAsync();
        return (true, null, sessionRaw);
    }

    public async Task<LandlordAccessToken?> ValidateSessionAsync(string rawToken, string rawSession)
    {
        var token = await GetValidTokenAsync(rawToken);
        if (token is null || token.OtpVerifiedAt is null) return null;
        if (token.SessionTokenHash is null || token.SessionExpiresAt is null || token.SessionExpiresAt < DateTime.UtcNow)
            return null;
        return token.SessionTokenHash == Hash(rawSession) ? token : null;
    }

    // ----------------------------------------------------------------- UAE Pass

    /// <summary>Begins a UAE Pass round-trip: stores a fresh anti-forgery <c>state</c> on the token and returns it.</summary>
    public async Task<string?> StartUaePassAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null) return null;
        var state = GenerateRawToken();
        token.UaePassState = state;
        await db.SaveChangesAsync();
        return state;
    }

    /// <summary>Resolves the token a UAE Pass callback belongs to, by the <c>state</c> we issued.</summary>
    public async Task<LandlordAccessToken?> ResolveByUaePassStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordAccessTokens.Include(t => t.Landlord)
            .FirstOrDefaultAsync(t => t.UaePassState == state);
    }

    /// <summary>Stores the verified UAE Pass profile on the token, marks it verified, clears the state and
    /// issues a fresh self-service session. Returns the raw session token for the continuation URL.</summary>
    public async Task<string?> StoreUaePassProfileAsync(int tokenId, UaePassProfile profile)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null) return null;

        token.UaePassVerifiedAt = DateTime.UtcNow;
        token.UaePassSub = profile.Sub;
        token.UaePassFullName = profile.FullName;
        token.UaePassEmail = profile.Email;
        token.UaePassMobile = profile.Mobile;
        token.UaePassEmiratesId = profile.EmiratesId;
        token.UaePassState = null;

        var sessionRaw = GenerateRawToken();
        token.OtpVerifiedAt ??= DateTime.UtcNow;
        token.SessionTokenHash = Hash(sessionRaw);
        token.SessionExpiresAt = token.ExpiresAt;
        await db.SaveChangesAsync();
        return sessionRaw;
    }

    /// <summary>Validates a self-service session by the token's numeric id (used for the post-UAE-Pass
    /// continuation URL, which carries the non-secret id + the secret session token — never the link token).</summary>
    public async Task<LandlordAccessToken?> ValidateSessionByIdAsync(int tokenId, string rawSession)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.Include(t => t.Landlord).FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null || token.ExpiresAt < DateTime.UtcNow) return null;
        if (token.Landlord is null || !token.Landlord.LinkActive || !LinkUsableStatuses.Contains(token.Landlord.Status))
            return null;
        if (token.OtpVerifiedAt is null || token.SessionTokenHash is null
            || token.SessionExpiresAt is null || token.SessionExpiresAt < DateTime.UtcNow)
            return null;
        return token.SessionTokenHash == Hash(rawSession) ? token : null;
    }

    public async Task MarkUsedAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.LandlordAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null) return;
        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static string MaskEmail(string? email) => SupplierAccessTokenService.MaskEmail(email);

    private static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
