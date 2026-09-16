using SupplierPortal.Contracts;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

namespace SupplierPortal.Web.SelfServiceApi;

public static class SelfServiceEndpoints
{
    public const string PortalPolicy = "PortalClient";
    private const long MaxDocumentBytes = 10 * 1024 * 1024;

    public static void MapSelfServiceApi(this WebApplication app)
    {
        // ---- OAuth2 client-credentials token endpoint (portal -> API trust) ----
        app.MapPost("/connect/token", async (HttpContext ctx, TokenService tokens) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();
            var clientId = form["client_id"].ToString();
            var clientSecret = form["client_secret"].ToString();

            if (!string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = "unsupported_grant_type" });
            }
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)
                || !tokens.ValidateClient(clientId, clientSecret))
            {
                return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var (token, expiresIn) = tokens.IssuePortalToken(clientId);
            return Results.Ok(new TokenResponse { access_token = token, token_type = "Bearer", expires_in = expiresIn });
        })
        .RequireRateLimiting("token")
        .WithName("IssueToken");

        // ---- Everything below requires a valid portal (client-credentials) token ----
        var group = app.MapGroup("/api/selfservice").RequireAuthorization(PortalPolicy);

        // Request an OTP for a supplier link token.
        group.MapPost("/otp/request", async (
            OtpRequestRequest req, SupplierAccessTokenService accessTokens, EmailBrandingService branding,
            IWebHostEnvironment env) =>
        {
            var token = await accessTokens.GetValidTokenAsync(req.LinkToken ?? "");
            if (token is null)
            {
                return Results.Ok(new OtpRequestResponse
                {
                    Success = false,
                    Error = "This link is invalid or has expired. Please contact the requester for a new link."
                });
            }

            // In Development there is no Database Mail profile, so skip the send and surface the code for
            // automated testing. In every other environment the OTP is emailed and never returned.
            var devMode = env.IsDevelopment();
            var (success, error, code) = await accessTokens.IssueOtpAsync(token.Id, sendEmail: !devMode, forceResend: req.Resend);

            // The supplier's business-unit logo, using the SAME source as the internal self-service header
            // (uploaded logo as a self-contained data URI, falling back to the hosted URL) so the public
            // front-end brands identically to the portal.
            var logo = await branding.GetBusinessUnitUploadedLogoSrcAsync(token.Supplier?.BusinessUnitId);

            return Results.Ok(new OtpRequestResponse
            {
                Success = success,
                Error = error,
                MaskedEmail = SupplierAccessTokenService.MaskEmail(token.Supplier?.ContactEmail),
                ExpiresInMinutes = 10,
                DevOtp = devMode ? code : null,
                LogoUrl = logo
            });
        })
        .RequireRateLimiting("otp");

        // Verify an OTP and mint a supplier session token.
        group.MapPost("/otp/verify", async (
            OtpVerifyRequest req, SupplierAccessTokenService accessTokens, TokenService tokens) =>
        {
            var token = await accessTokens.GetValidTokenAsync(req.LinkToken ?? "");
            if (token is null)
            {
                return Results.Ok(new OtpVerifyResponse { Success = false, Error = "This link is invalid or has expired." });
            }

            var (success, error, sessionRaw) = await accessTokens.VerifyOtpAsync(token.Id, req.Code ?? "");
            if (!success || sessionRaw is null)
            {
                return Results.Ok(new OtpVerifyResponse { Success = false, Error = error });
            }

            var (sessionJwt, expiresAt) = tokens.IssueSupplierSessionToken(
                token.SupplierId, token.Id, req.LinkToken!, sessionRaw);

            return Results.Ok(new OtpVerifyResponse
            {
                Success = true,
                SessionToken = sessionJwt,
                ExpiresAtUtc = expiresAt,
                SupplierName = token.Supplier?.SupplierName,
                ContactEmail = token.Supplier?.ContactEmail
            });
        })
        .RequireRateLimiting("otp");

        // ---- Data endpoints: require BOTH the portal token AND a valid supplier session ----

        group.MapGet("/supplier", async (HttpContext ctx, SupplierSessionResolver resolver, SelfServiceApiService svc) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            var bootstrap = await svc.BuildBootstrapAsync(session.SupplierId);
            return bootstrap is null ? Results.NotFound() : Results.Ok(bootstrap);
        });

        group.MapPut("/supplier", async (
            HttpContext ctx, SupplierDto dto, SupplierSessionResolver resolver, SelfServiceApiService svc) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            var result = await svc.SaveAsync(session, dto, ActorLabel(session));
            return Results.Ok(result);
        });

        group.MapPost("/submit", async (
            HttpContext ctx, SupplierSessionResolver resolver, SupplierAccessTokenService accessTokens,
            SelfServiceApiService svc, IConfiguration config) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            await accessTokens.MarkUsedAsync(session.Id);
            var adminBaseUrl = config["Notifications:AdminBaseUrl"] ?? "";
            var result = await svc.SubmitAsync(session, adminBaseUrl);
            return Results.Ok(result);
        });

        // ---- Documents ----

        group.MapPost("/documents", async (
            HttpContext ctx, SupplierSessionResolver resolver, SupplierDocumentService docs) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            if (!ctx.Request.HasFormContentType)
            {
                return Results.BadRequest(new DocumentUploadResponse { Success = false, Error = "Expected a multipart form." });
            }

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new DocumentUploadResponse { Success = false, Error = "No file provided." });
            }
            if (file.Length > MaxDocumentBytes)
            {
                return Results.BadRequest(new DocumentUploadResponse { Success = false, Error = "File exceeds the 10 MB size limit." });
            }
            if (!Enum.TryParse<SelfServiceDocumentType>(form["documentType"], out var dtoType))
            {
                return Results.BadRequest(new DocumentUploadResponse { Success = false, Error = "Invalid document type." });
            }

            await using var stream = file.OpenReadStream();
            var result = await docs.UploadFromStreamAsync(
                session.SupplierId, (SupplierDocumentType)(int)dtoType, stream, file.FileName,
                file.ContentType, file.Length, ActorLabel(session));

            var current = await docs.GetDocumentsAsync(session.SupplierId);
            return Results.Ok(new DocumentUploadResponse
            {
                Success = result.Success,
                Error = result.Error,
                ExtractedNumber = result.ExtractedNumber,
                DunsNumber = result.DunsNumber,
                Documents = current.Select(DtoMapper.ToDto).ToList()
            });
        })
        .RequireRateLimiting("upload");

        group.MapGet("/documents/{id:int}", async (
            int id, HttpContext ctx, SupplierSessionResolver resolver, SupplierDocumentService docs) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            // Ownership check: the document must belong to the session's supplier.
            var owned = await docs.GetDocumentsAsync(session.SupplierId);
            if (owned.All(d => d.Id != id))
            {
                return Results.NotFound();
            }
            var file = await docs.GetFileAsync(id);
            return file is null ? Results.NotFound() : Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
        });

        group.MapDelete("/documents/{id:int}", async (
            int id, HttpContext ctx, SupplierSessionResolver resolver, SupplierDocumentService docs) =>
        {
            var session = await resolver.ResolveAsync(ctx);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            var owned = await docs.GetDocumentsAsync(session.SupplierId);
            if (owned.All(d => d.Id != id))
            {
                return Results.NotFound();
            }
            var (success, error) = await docs.DeleteAsync(id, ActorLabel(session));
            var current = await docs.GetDocumentsAsync(session.SupplierId);
            return Results.Ok(new DocumentUploadResponse
            {
                Success = success,
                Error = error,
                Documents = current.Select(DtoMapper.ToDto).ToList()
            });
        });
    }

    private static string ActorLabel(SupplierAccessToken session) =>
        $"Supplier Self-Service ({session.Supplier?.ContactEmail})";
}
