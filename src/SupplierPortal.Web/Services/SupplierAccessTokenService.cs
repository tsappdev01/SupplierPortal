using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Issues and validates the opaque link tokens and 4-digit OTP codes that protect the
/// supplier self-service flow. The link token is looked up by value, so it's hashed
/// deterministically (SHA-256); the OTP code is always verified against one already-known row,
/// so it reuses the app's existing salted <see cref="IPasswordHasher{TUser}"/> exactly as
/// <c>AuthService</c> does for login passwords.</summary>
public class SupplierAccessTokenService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IPasswordHasher<User> hasher, IWebHostEnvironment env,
    EmailBrandingService branding, ILogger<SupplierAccessTokenService> logger)
{
    private static readonly TimeSpan LinkValidity = TimeSpan.FromDays(14);
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);
    private const int MaxOtpAttempts = 5;
    private const int MaxOtpSends = 5;
    private const string MailProfileName = "BP";
    private const string OtpTemplateRelativePath = "doc/supplier_otp_code.html";

    // IPasswordHasher<TUser>'s default implementation never reads TUser's fields - it hashes only
    // the password/code string itself - so a single throwaway instance is safe to share here.
    private static readonly User HasherContext = new();

    public async Task<string> CreateTokenAsync(int supplierId, string createdBy, bool isAdminTest = false)
    {
        var raw = GenerateRawToken();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.SupplierAccessTokens.Add(new SupplierAccessToken
        {
            SupplierId = supplierId,
            TokenHash = Hash(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(LinkValidity),
            CreatedBy = createdBy,
            IsAdminTest = isAdminTest
        });
        await db.SaveChangesAsync();
        return raw;
    }

    /// <summary>All self-service notification emails ever sent for a supplier (most recent first),
    /// including admin test links, for the Email History tab on the supplier edit screen.</summary>
    public async Task<List<SupplierAccessToken>> GetHistoryForSupplierAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SupplierAccessTokens
            .Where(t => t.SupplierId == supplierId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<SupplierAccessToken?> GetValidTokenAsync(string rawToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hash = Hash(rawToken);
        var token = await db.SupplierAccessTokens.Include(t => t.Supplier).FirstOrDefaultAsync(t => t.TokenHash == hash);
        return token is null || token.ExpiresAt < DateTime.UtcNow ? null : token;
    }

    /// <summary>Blazor internal-app entry point: generate, persist and email a fresh OTP.</summary>
    public async Task<(bool Success, string? Error)> GenerateAndSendOtpAsync(int tokenId)
    {
        var (success, error, _) = await IssueOtpAsync(tokenId, sendEmail: true);
        return (success, error);
    }

    /// <summary>
    /// Generates, persists and (optionally) emails a fresh OTP for a link. Returns the plaintext code so
    /// that a NON-PRODUCTION caller (the REST API in Development) can surface it for automated end-to-end
    /// testing when no Database Mail profile exists. Production callers must send the email
    /// (<paramref name="sendEmail"/> = true) and must never expose the returned code.
    /// </summary>
    public async Task<(bool Success, string? Error, string? Code)> IssueOtpAsync(int tokenId, bool sendEmail = true)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.SupplierAccessTokens.Include(t => t.Supplier).FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null)
        {
            return (false, "Link not found.", null);
        }
        if (token.Supplier is null || string.IsNullOrWhiteSpace(token.Supplier.ContactEmail))
        {
            return (false, "This supplier has no Contact Email on file.", null);
        }
        if (token.OtpSendCount >= MaxOtpSends)
        {
            return (false, "Too many verification code requests for this link. Please contact us for assistance.", null);
        }

        string? body = null;
        if (sendEmail)
        {
            var templatePath = Path.Combine(env.WebRootPath, "doc", "supplier_otp_code.html");
            if (!File.Exists(templatePath))
            {
                return (false, $"Email template not found at wwwroot/{OtpTemplateRelativePath}.", null);
            }
            body = await File.ReadAllTextAsync(templatePath);
        }

        var code = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        token.OtpCodeHash = hasher.HashPassword(HasherContext, code);
        token.OtpGeneratedAt = DateTime.UtcNow;
        token.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        token.OtpAttemptCount = 0;
        token.OtpSendCount++;

        // Persist the OTP state before attempting delivery: if the send fails (transient mail
        // outage), the code the supplier will retry with via "Resend" is still consistent, rather
        // than silently discarding a hashed code no email ever carried.
        await db.SaveChangesAsync();

        if (sendEmail)
        {
            var brand = System.Net.WebUtility.HtmlEncode(
                await branding.GetBusinessUnitNameAsync(token.Supplier.BusinessUnitId) ?? EmailBrandingService.DefaultBrandName);
            var brandLine = $"<div style=\"color:#0b2545;font-size:16px;font-weight:700;letter-spacing:.02em;margin-bottom:10px\">{brand}</div>";
            // Brand the header with the respective Business Unit's logo (hosted Logo URL when set, else the
            // embedded logo) when available.
            var logo = await branding.GetBusinessUnitLogoSrcAsync(token.Supplier.BusinessUnitId);
            // When a logo image is available, show it alone (the brand name is not repeated as text).
            var brandBlock = string.IsNullOrWhiteSpace(logo)
                ? brandLine
                : $"<img src=\"{logo}\" alt=\"{brand}\" style=\"max-height:48px;max-width:220px;display:block\" />";
            body = body!
                .Replace("{CompanyLogo}", brandBlock)
                .Replace("{Supplier Name}", token.Supplier.SupplierName)
                .Replace("{OTP Code}", code)
                .Replace("{Expiry Minutes}", ((int)OtpValidity.TotalMinutes).ToString());
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    EXEC msdb.dbo.sp_send_dbmail
                        @profile_name = {MailProfileName},
                        @recipients = {token.Supplier.ContactEmail},
                        @subject = {"Your Verification Code"},
                        @body = {body},
                        @body_format = 'HTML';");
            }
            catch (Exception ex)
            {
                return (false, $"Database Mail send failed: {ex.Message}", null);
            }
        }

        return (true, null, code);
    }

    /// <summary>Masks an email like <c>jo***@acme.com</c> for safe display on the OTP screen.</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "your email on file";
        }
        var parts = email.Split('@', 2);
        var name = parts[0];
        var shown = name.Length <= 2 ? name[..1] : name[..2];
        return $"{shown}{new string('*', Math.Max(2, name.Length - shown.Length))}@{parts[1]}";
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(int tokenId, string enteredCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.SupplierAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null)
        {
            return (false, "Link not found.", null);
        }
        if (token.OtpCodeHash is null || token.OtpExpiresAt is null || token.OtpExpiresAt < DateTime.UtcNow)
        {
            return (false, "Your code has expired. Please request a new one.", null);
        }
        if (token.OtpAttemptCount >= MaxOtpAttempts)
        {
            return (false, "Too many incorrect attempts. Please request a new code.", null);
        }

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

    public async Task<SupplierAccessToken?> ValidateSessionAsync(string rawToken, string rawSession)
        => (await ValidateSessionDetailedAsync(rawToken, rawSession)).Token;

    /// <summary>Validates the session and, when it fails, returns a short reason code identifying which
    /// check rejected it. The reason is logged and can be surfaced to aid diagnosis of "session expired".</summary>
    public async Task<(SupplierAccessToken? Token, string? Reason)> ValidateSessionDetailedAsync(string rawToken, string rawSession)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Pre-compute the hash into a local so EF binds it as a parameter (never tries to translate the
        // custom Hash() into SQL), matching GetValidTokenAsync.
        var tokenHash = Hash(rawToken);
        var token = await db.SupplierAccessTokens.Include(t => t.Supplier)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        string? reason = null;
        if (token is null)
        {
            reason = $"link-not-found (tokenLen={rawToken?.Length ?? 0})";
        }
        else if (token.ExpiresAt < DateTime.UtcNow)
        {
            reason = $"link-expired (expired {token.ExpiresAt:u})";
        }
        else if (token.OtpVerifiedAt is null)
        {
            reason = "otp-not-verified";
        }
        else if (token.SessionTokenHash is null)
        {
            reason = "no-session-on-link";
        }
        else if (token.SessionExpiresAt is null || token.SessionExpiresAt < DateTime.UtcNow)
        {
            reason = $"session-expired (expired {token.SessionExpiresAt:u}, nowUtc {DateTime.UtcNow:u})";
        }
        else if (token.SessionTokenHash != Hash(rawSession))
        {
            reason = "session-superseded (a newer session was issued for this link — e.g. the link/OTP was opened/verified again elsewhere)";
        }

        if (reason is not null)
        {
            logger.LogWarning("SS session reject [{Reason}] supplier {SupplierId} tokenLen={TokenLen} sessionLen={SessionLen}.",
                reason, token?.SupplierId, rawToken?.Length ?? 0, rawSession?.Length ?? 0);
            return (null, reason);
        }
        return (token, null);
    }

    public async Task MarkUsedAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.SupplierAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null)
        {
            return;
        }
        token.UsedAt = DateTime.UtcNow;
        // Final submit invalidates the link and session (validation rejects on ExpiresAt/SessionExpiresAt),
        // so a Completed record can't be reopened, re-edited and re-submitted — which would re-send the
        // requestor/supplier emails and regenerate the declaration — within the remaining link window.
        // MarkUsedAsync is only called on the completing submit, so expiring here is correct.
        token.ExpiresAt = DateTime.UtcNow;
        token.SessionExpiresAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
