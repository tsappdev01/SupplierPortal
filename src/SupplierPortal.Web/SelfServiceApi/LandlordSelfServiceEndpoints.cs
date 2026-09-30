using SupplierPortal.Contracts;
using SupplierPortal.Web.Models.Leasing;
using SupplierPortal.Web.Services;
using SupplierPortal.Web.Services.Leasing;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>Landlord self-service OAuth API (mirrors the customer API) — exposes the OTP → form → documents →
/// submit flow to the DMZ Partner Connect app over the same portal client-credentials trust. The landlord link
/// token + session token travel in the <c>X-Landlord-Link</c> / <c>X-Landlord-Session</c> headers. The primary-
/// contact field metadata is admin-configurable (Landlord Field Manager) and travels in the bootstrap.
/// UAE Pass and Circularo signing remain served by the in-app host; the DMZ path covers the OTP core flow.</summary>
public static class LandlordSelfServiceEndpoints
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;
    private const string LinkHeader = "X-Landlord-Link";
    private const string SessionHeader = "X-Landlord-Session";
    private const string Actor = "Landlord Self-Service (Partner Connect)";

    // Landlord-facing document types offered on the DMZ form (Emirates ID is mandatory).
    private static readonly (LandlordDocumentType Type, string Name, bool Required, int Seq)[] DocTypes =
    {
        (LandlordDocumentType.EmiratesId, "Emirates ID", true, 1),
        (LandlordDocumentType.TradeLicense, "Trade Licence", false, 2),
        (LandlordDocumentType.MoaOrPoa, "MOA / POA", false, 3),
        (LandlordDocumentType.TitleDeed, "Title Deed", false, 4),
        (LandlordDocumentType.AuthorisationLetter, "Authorisation Letter", false, 5),
        (LandlordDocumentType.Other, "Other", false, 6),
    };

    public static void MapLandlordSelfServiceApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/selfservice/landlord")
            .RequireAuthorization(SelfServiceEndpoints.PortalPolicy);

        // ---- OTP / session ----

        group.MapPost("/otp/request", async (
            OtpRequestRequest req, LandlordAccessTokenService tokens, EmailBrandingService branding) =>
        {
            var (state, token) = await tokens.ResolveTokenAsync(req.LinkToken ?? "");
            if (state != LandlordAccessTokenService.LinkState.Valid || token?.Landlord is null)
            {
                return Results.Ok(new OtpRequestResponse
                {
                    Success = false,
                    Error = "This link is invalid or has expired. Please contact the requester for a new link."
                });
            }
            var (ok, error, _) = await tokens.IssueOtpAsync(token.Id);
            return Results.Ok(new OtpRequestResponse
            {
                Success = ok,
                Error = error,
                MaskedEmail = SupplierAccessTokenService.MaskEmail(token.Landlord.Email),
                ExpiresInMinutes = 10,
                LogoUrl = await branding.GetBusinessUnitLogoSrcAsync(token.Landlord.BusinessUnitId)
            });
        }).RequireRateLimiting("otp");

        group.MapGet("/link-info", async (string token, LandlordAccessTokenService tokens, EmailBrandingService branding) =>
        {
            var (_, t) = await tokens.ResolveTokenAsync(token ?? "");
            var buId = t?.Landlord?.BusinessUnitId;
            return Results.Ok(new LinkInfoResponse
            {
                Success = t is not null,
                BusinessUnitName = buId is int b1 ? await branding.GetBusinessUnitNameAsync(b1) : null,
                BusinessUnitEmail = buId is int b2 ? await branding.GetPortalSenderEmailAsync(b2, Models.PortalType.Landlord) : null,
                LogoUrl = buId is int b3 ? await branding.GetBusinessUnitLogoSrcAsync(b3) : null
            });
        }).RequireRateLimiting("otp");

        group.MapPost("/otp/verify", async (OtpVerifyRequest req, LandlordAccessTokenService tokens) =>
        {
            var (state, token) = await tokens.ResolveTokenAsync(req.LinkToken ?? "");
            if (state != LandlordAccessTokenService.LinkState.Valid || token is null)
            {
                return Results.Ok(new LandlordOtpVerifyResponse { Success = false, Error = "This link is invalid or has expired." });
            }
            var (ok, error, session) = await tokens.VerifyOtpAsync(token.Id, req.Code ?? "");
            if (!ok || session is null)
            {
                return Results.Ok(new LandlordOtpVerifyResponse { Success = false, Error = error });
            }
            return Results.Ok(new LandlordOtpVerifyResponse
            {
                Success = true,
                SessionToken = session,
                LandlordName = token.Landlord?.CompanyName ?? token.Landlord?.PropertyCode,
                ContactEmail = token.Landlord?.Email
            });
        }).RequireRateLimiting("otp");

        // ---- Data endpoints (require both the portal token AND a valid link+session) ----

        group.MapGet("/form", async (HttpContext ctx, LandlordAccessTokenService tokens, LandlordService landlords,
            LandlordDocumentService docs, LandlordFieldConfigService fieldConfig, EmailBrandingService branding) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            var landlord = await landlords.GetForSelfServiceAsync(token.LandlordId);
            if (landlord is null) return Results.Unauthorized();

            var configs = (await fieldConfig.GetAllAsync()).Where(c => c.Section == "Contact").ToList();
            var tabs = await fieldConfig.GetTabsAsync();
            var documents = (await docs.GetDocumentsAsync(landlord.Id))
                .Where(d => d.DocumentType is not (LandlordDocumentType.RegistrationSummary or LandlordDocumentType.SignedDocument))
                .ToList();
            var correction = await landlords.GetOpenCorrectionAsync(landlord.Id);

            return Results.Ok(new LandlordFormBootstrapDto
            {
                LandlordId = landlord.Id,
                PropertyCode = landlord.PropertyCode,
                CompanyName = landlord.CompanyName,
                Status = landlord.Status.ToString(),
                SigningRule = landlord.SigningRule.ToString(),
                CorrectionComment = correction?.Comment,
                LogoUrl = await branding.GetBusinessUnitLogoSrcAsync(landlord.BusinessUnitId),
                Contacts = landlord.Contacts.OrderByDescending(c => c.IsPrimary).Select(ToContactDto).ToList(),
                Signatories = landlord.Signatories.Where(s => !s.IsPrimaryContact).OrderBy(s => s.Order)
                    .Select(s => new LandlordSignatoryDto { Name = s.Name, Email = s.Email, Mobile = s.Mobile }).ToList(),
                FieldConfigs = configs.Select(c => new LandlordFieldConfigDto
                {
                    FieldKey = c.FieldKey, DisplayLabel = c.DisplayLabel, HelpText = c.HelpText,
                    MinLength = c.MinLength, MaxLength = c.MaxLength, Section = c.Section, GroupName = c.GroupName,
                    Sequence = c.Sequence, FieldKind = c.FieldKind, LandlordControl = c.LandlordControl,
                    IsVisible = c.IsVisible, IsRequired = c.IsRequired,
                    ControllingFieldKey = c.ControllingFieldKey, ControllingValue = c.ControllingValue
                }).ToList(),
                Tabs = tabs.Select(t => new LandlordTabDto
                {
                    Section = t.Section, GroupName = t.GroupName, Title = t.Title, Sequence = t.Sequence, IsVisible = t.IsVisible
                }).ToList(),
                Documents = documents.Select(ToDocDto).ToList(),
                DocumentTypes = DocTypes.Select(d => new LandlordDocumentTypeDto
                {
                    Key = (int)d.Type, Code = d.Type.ToString(), Name = d.Name, IsRequired = d.Required, Sequence = d.Seq
                }).ToList()
            });
        });

        group.MapPost("/form/save", async (HttpContext ctx, SaveLandlordRequest req,
            LandlordAccessTokenService tokens, LandlordService landlords) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            var (ok, err, _) = await landlords.SaveAsync(token.LandlordId, ToUpdate(req), submit: false, Actor, token.Id, ClientIp(ctx));
            return Results.Ok(new SaveLandlordResponse { Success = ok, Errors = ok ? [] : [err ?? "Could not save your changes."] });
        });

        group.MapPost("/submit", async (HttpContext ctx, SubmitLandlordRequest req, IConfiguration config,
            LandlordAccessTokenService tokens, LandlordService landlords) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            if (!req.Acknowledged)
            {
                return Results.Ok(new SubmitLandlordResponse { Success = false, Error = "You must confirm the declaration before submitting." });
            }
            // The form is saved on each step, so submit re-sends the CURRENT persisted contacts/signatories and
            // flips submit=true (which runs server-side validation + the review/notify/PDF flow).
            var landlord = await landlords.GetForSelfServiceAsync(token.LandlordId);
            if (landlord is null) return Results.Unauthorized();
            var update = new LandlordSelfUpdate
            {
                SigningRule = landlord.SigningRule,
                Contacts = landlord.Contacts.Select(ToContactInput).ToList(),
                Signatories = landlord.Signatories.Where(s => !s.IsPrimaryContact)
                    .OrderBy(s => s.Order).Select(s => new SignatoryInput { Name = s.Name, Email = s.Email, Mobile = s.Mobile }).ToList()
            };
            var baseUrl = config["Notifications:AdminBaseUrl"] ?? config["SelfService:InternalUrl"];
            var (ok, err, _) = await landlords.SaveAsync(token.LandlordId, update, submit: true, Actor, token.Id, ClientIp(ctx), baseUrl);
            return Results.Ok(new SubmitLandlordResponse { Success = ok, Error = ok ? null : err });
        });

        // ---- Documents ----

        group.MapGet("/documents", async (HttpContext ctx, LandlordAccessTokenService tokens, LandlordDocumentService docs) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            var list = await LandlordDocsAsync(docs, token.LandlordId);
            return Results.Ok(new LandlordDocumentUploadResponse { Success = true, Documents = list.Select(ToDocDto).ToList() });
        });

        group.MapPost("/documents", async (HttpContext ctx, LandlordAccessTokenService tokens, LandlordDocumentService docs) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            if (!ctx.Request.HasFormContentType) return Results.BadRequest(new LandlordDocumentUploadResponse { Success = false, Error = "Expected a multipart form." });

            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files["file"];
            var documentType = form["documentType"].ToString();
            if (file is null || file.Length == 0) return Results.BadRequest(new LandlordDocumentUploadResponse { Success = false, Error = "No file provided." });
            if (file.Length > MaxDocumentBytes) return Results.BadRequest(new LandlordDocumentUploadResponse { Success = false, Error = "File exceeds the 10 MB size limit." });
            if (!Enum.TryParse<LandlordDocumentType>(documentType, out var type) || !DocTypes.Any(d => d.Type == type))
            {
                return Results.BadRequest(new LandlordDocumentUploadResponse { Success = false, Error = "Invalid document type." });
            }

            byte[] bytes;
            await using (var stream = file.OpenReadStream())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            var (ok, err) = await docs.UploadBytesAsync(token.LandlordId, type, file.FileName, file.ContentType, bytes, Actor);
            var current = await LandlordDocsAsync(docs, token.LandlordId);
            return Results.Ok(new LandlordDocumentUploadResponse { Success = ok, Error = err, Documents = current.Select(ToDocDto).ToList() });
        }).RequireRateLimiting("upload");

        group.MapGet("/documents/{id:int}", async (int id, HttpContext ctx, LandlordAccessTokenService tokens, LandlordDocumentService docs) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            var owned = await LandlordDocsAsync(docs, token.LandlordId);
            if (owned.All(d => d.Id != id)) return Results.NotFound();
            var file = await docs.GetFileAsync(id);
            return file is null ? Results.NotFound() : Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
        });

        group.MapPost("/documents/{id:int}/delete", async (int id, HttpContext ctx, LandlordAccessTokenService tokens, LandlordDocumentService docs) =>
        {
            var (link, session) = ReadSession(ctx);
            var token = await tokens.ValidateSessionAsync(link, session);
            if (token is null) return Results.Unauthorized();
            var owned = await LandlordDocsAsync(docs, token.LandlordId);
            if (owned.Any(d => d.Id == id)) { await docs.DeleteAsync(id); }
            var current = await LandlordDocsAsync(docs, token.LandlordId);
            return Results.Ok(new LandlordDocumentUploadResponse { Success = true, Documents = current.Select(ToDocDto).ToList() });
        });
    }

    private static (string Link, string Session) ReadSession(HttpContext ctx) =>
        (ctx.Request.Headers[LinkHeader].ToString(), ctx.Request.Headers[SessionHeader].ToString());

    private static string? ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString();

    private static async Task<List<LandlordDocument>> LandlordDocsAsync(LandlordDocumentService docs, int landlordId) =>
        (await docs.GetDocumentsAsync(landlordId))
            .Where(d => d.DocumentType is not (LandlordDocumentType.RegistrationSummary or LandlordDocumentType.SignedDocument))
            .ToList();

    private static LandlordSelfUpdate ToUpdate(SaveLandlordRequest req) => new()
    {
        SigningRule = Enum.TryParse<SigningRule>(req.SigningRule, out var sr) ? sr : SigningRule.All,
        Contacts = req.Contacts.Select(c => new ContactInput
        {
            Id = c.Id, IsPrimary = c.IsPrimary,
            ContactType = Enum.TryParse<ContactType>(c.ContactType, out var ct) ? ct : ContactType.Owner,
            FullName = c.FullName, Designation = c.Designation, Mobile = c.Mobile, Email = c.Email,
            EmiratesId = c.EmiratesId, EmiratesIdName = c.EmiratesIdName, EmiratesIdExpiry = c.EmiratesIdExpiry
        }).ToList(),
        Signatories = req.Signatories.Select(s => new SignatoryInput { Name = s.Name, Email = s.Email, Mobile = s.Mobile }).ToList()
    };

    private static LandlordContactDto ToContactDto(LandlordContact c) => new()
    {
        Id = c.Id, IsPrimary = c.IsPrimary, ContactType = c.ContactType.ToString(),
        FullName = c.FullName ?? "", Designation = c.Designation, Mobile = c.Mobile, Email = c.Email ?? "",
        EmiratesId = c.EmiratesId, EmiratesIdName = c.EmiratesIdName, EmiratesIdExpiry = c.EmiratesIdExpiry
    };

    private static ContactInput ToContactInput(LandlordContact c) => new()
    {
        Id = c.Id, IsPrimary = c.IsPrimary, ContactType = c.ContactType, FullName = c.FullName ?? "",
        Designation = c.Designation, Mobile = c.Mobile, Email = c.Email ?? "",
        EmiratesId = c.EmiratesId, EmiratesIdName = c.EmiratesIdName, EmiratesIdExpiry = c.EmiratesIdExpiry
    };

    private static LandlordDocumentDto ToDocDto(LandlordDocument d) => new()
    {
        Id = d.Id, DocumentType = d.DocumentType.ToString(), FileName = d.FileName,
        ContentType = d.ContentType, UploadedAt = d.UploadedAt, FileSizeBytes = d.FileSizeBytes
    };
}
