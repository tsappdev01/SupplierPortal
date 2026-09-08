using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Issues and validates the opaque link tokens and 4-digit OTP codes that protect the customer
/// self-service flow. Mirrors <see cref="SupplierAccessTokenService"/>: the link token is hashed
/// deterministically (SHA-256) for lookup, while the OTP code reuses the app's salted password hasher.</summary>
public class CustomerAccessTokenService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IPasswordHasher<User> hasher, IWebHostEnvironment env,
    EmailBrandingService branding)
{
    private static readonly TimeSpan LinkValidity = TimeSpan.FromDays(14);
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);
    private const int MaxOtpAttempts = 5;
    private const int MaxOtpSends = 5;
    private const string MailProfileName = "BP";
    private const string OtpTemplateRelativePath = "doc/customer_otp_code.html";

    private static readonly User HasherContext = new();

    /// <summary>The email to reach the customer on: the account Primary Email, else the contact's Email.</summary>
    public static string? EmailFor(Customer c) =>
        !string.IsNullOrWhiteSpace(c.PrimaryEmail) ? c.PrimaryEmail : c.Contact?.Email;

    public async Task<string> CreateTokenAsync(int customerId, string createdBy, bool isAdminTest = false)
    {
        var raw = GenerateRawToken();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CustomerAccessTokens.Add(new CustomerAccessToken
        {
            CustomerId = customerId,
            TokenHash = Hash(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(LinkValidity),
            CreatedBy = createdBy,
            IsAdminTest = isAdminTest
        });
        await db.SaveChangesAsync();
        return raw;
    }

    public async Task<CustomerAccessToken?> GetValidTokenAsync(string rawToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hash = Hash(rawToken);
        var token = await db.CustomerAccessTokens
            .Include(t => t.Customer).ThenInclude(c => c!.Contact)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        return token is null || token.ExpiresAt < DateTime.UtcNow ? null : token;
    }

    /// <summary>Generate, persist and email a fresh OTP for a link.</summary>
    public async Task<(bool Success, string? Error)> GenerateAndSendOtpAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.CustomerAccessTokens
            .Include(t => t.Customer).ThenInclude(c => c!.Contact)
            .FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null)
        {
            return (false, "Link not found.");
        }
        var email = token.Customer is null ? null : EmailFor(token.Customer);
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "This customer has no email address on file.");
        }
        if (token.OtpSendCount >= MaxOtpSends)
        {
            return (false, "Too many verification code requests for this link. Please contact us for assistance.");
        }

        var templatePath = Path.Combine(env.WebRootPath, "doc", "customer_otp_code.html");
        if (!File.Exists(templatePath))
        {
            return (false, $"Email template not found at wwwroot/{OtpTemplateRelativePath}.");
        }
        var body = await File.ReadAllTextAsync(templatePath);

        var code = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        token.OtpCodeHash = hasher.HashPassword(HasherContext, code);
        token.OtpGeneratedAt = DateTime.UtcNow;
        token.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        token.OtpAttemptCount = 0;
        token.OtpSendCount++;
        await db.SaveChangesAsync();

        var brand = System.Net.WebUtility.HtmlEncode(
            await branding.GetBusinessUnitNameAsync(token.Customer!.BusinessUnitId) ?? EmailBrandingService.DefaultBrandName);
        var brandLine = $"<div style=\"color:#ffffff;font-size:16px;font-weight:700;letter-spacing:.02em;margin-bottom:10px\">{brand}</div>";
        var logo = await branding.GetBusinessUnitLogoSrcAsync(token.Customer.BusinessUnitId);
        var brandBlock = string.IsNullOrWhiteSpace(logo)
            ? brandLine
            : $"<img src=\"{logo}\" alt=\"{brand}\" style=\"max-height:48px;max-width:220px;display:block\" />";
        body = body
            .Replace("{CompanyLogo}", brandBlock)
            .Replace("{Customer Name}", token.Customer.LegalName ?? "Customer")
            .Replace("{OTP Code}", code)
            .Replace("{Expiry Minutes}", ((int)OtpValidity.TotalMinutes).ToString());

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {email},
                    @subject = {"Your Verification Code"},
                    @body = {body},
                    @body_format = 'HTML';");
        }
        catch (Exception ex)
        {
            return (false, $"Database Mail send failed: {ex.Message}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(int tokenId, string enteredCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.CustomerAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
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

    public async Task<CustomerAccessToken?> ValidateSessionAsync(string rawToken, string rawSession)
    {
        var token = await GetValidTokenAsync(rawToken);
        if (token is null || token.OtpVerifiedAt is null)
        {
            return null;
        }
        if (token.SessionTokenHash is null || token.SessionExpiresAt is null || token.SessionExpiresAt < DateTime.UtcNow)
        {
            return null;
        }
        return token.SessionTokenHash == Hash(rawSession) ? token : null;
    }

    public async Task MarkUsedAsync(int tokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var token = await db.CustomerAccessTokens.FirstOrDefaultAsync(t => t.Id == tokenId);
        if (token is null)
        {
            return;
        }
        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
