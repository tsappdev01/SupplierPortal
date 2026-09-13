namespace SupplierPortal.Web.Services.SelfService;

// Request/response shapes for the internal Self-Service API and its DMZ HTTP-client gateways.
// Kept deliberately small and free of EF navigation graphs so they serialize cleanly over HTTP.

public record SsTokenRequest(string Token);
public record SsOtpVerifyRequest(string Token, string Code);

public record SsResult(bool Success, string? Error);
public record SsVerifyResult(bool Success, string? Error, string? SessionToken);
