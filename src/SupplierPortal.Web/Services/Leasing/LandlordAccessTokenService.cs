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
    IDbContextFactory<LeasingDbContext> dbFactory, IPasswordHasher<User> hasher, IWebHostEnvironment env)
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);
    private const int MaxOtpAttempts = 5;
    private const int MaxOtpSends = 5;
    private const string MailProfileName = "BP";

    private static readonly User HasherContext = new();

    public async Task<LandlordAccessToken?> GetValidTokenAsync(string rawToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hash = Hash(rawToken);
        var token = await db.LandlordAccessTokens.Include(t => t.Landlord)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        return token is null || token.ExpiresAt < DateTime.UtcNow ? null : token;
    }

    public static bool NeedsFreshOtp(LandlordAccessToken t) =>
        t.OtpGeneratedAt is null || t.OtpExpiresAt is null || t.OtpExpiresAt < DateTime.UtcNow;

    /// <summary>Generates, persists and (in Production) emails a fresh OTP. Returns the plaintext code only
    /// so a Development caller can display it locally where no mail profile exists; never surface it in
    /// Production.</summary>
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
        token.OtpCodeHash = hasher.HashPassword(HasherContext, code);
        token.OtpGeneratedAt = DateTime.UtcNow;
        token.OtpExpiresAt = DateTime.UtcNow.Add(OtpValidity);
        token.OtpAttemptCount = 0;
        token.OtpSendCount++;
        await db.SaveChangesAsync();

        if (env.IsDevelopment())
        {
            // No Database Mail locally — surface the code so the flow can be exercised end to end.
            return (true, null, code);
        }

        var body = $"<p>Dear {token.Landlord.PrimaryContactName},</p>" +
                   $"<p>Your verification code is <strong>{code}</strong>. It expires in {(int)OtpValidity.TotalMinutes} minutes.</p>" +
                   "<p>Dubai Investments — Leasing</p>";
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
