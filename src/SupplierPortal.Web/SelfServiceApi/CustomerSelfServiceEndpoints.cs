using SupplierPortal.Contracts;
using SupplierPortal.Web.Models.Customer;
using SupplierPortal.Web.Services;
using SupplierPortal.Web.Services.SelfService;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>Customer self-service OAuth API (mirrors <see cref="SelfServiceEndpoints"/> for the supplier).
/// Exposes the existing customer self-service flow — served by the internal-mode Local gateways — to the thin
/// DMZ Partner Connect app over the same portal client-credentials trust. The customer's native link token +
/// session token are carried in the <c>X-Customer-Link</c> / <c>X-Customer-Session</c> headers and validated
/// by the gateway on every data call. Field values travel as FieldKey-&gt;value dictionaries so the DMZ app
/// needs no EF model.</summary>
public static class CustomerSelfServiceEndpoints
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;
    private const string LinkHeader = "X-Customer-Link";
    private const string SessionHeader = "X-Customer-Session";

    public static void MapCustomerSelfServiceApi(this WebApplication app)
    {
        // Requires a valid portal (client-credentials) token, same policy as the supplier API.
        var group = app.MapGroup("/api/selfservice/customer")
            .RequireAuthorization(SelfServiceEndpoints.PortalPolicy);

        // ---- OTP / session (link token in the body) ----

        group.MapPost("/otp/request", async (
            OtpRequestRequest req, CustomerAccessTokenService accessTokens, EmailBrandingService branding,
            BrandLogoInliner logoInliner) =>
        {
            var t = await accessTokens.GetValidTokenAsync(req.LinkToken ?? "");
            if (t is null)
            {
                return Results.Ok(new OtpRequestResponse
                {
                    Success = false,
                    Error = "This link is invalid or has expired. Please contact the requester for a new link."
                });
            }

            var (success, error) = await accessTokens.GenerateAndSendOtpAsync(t.Id);
            var logo = await logoInliner.InlineIfUrlAsync(
                await branding.GetBusinessUnitUploadedLogoSrcAsync(t.Customer?.BusinessUnitId));

            return Results.Ok(new OtpRequestResponse
            {
                Success = success,
                Error = error,
                MaskedEmail = SupplierAccessTokenService.MaskEmail(CustomerAccessTokenService.EmailFor(t.Customer)),
                ExpiresInMinutes = 10,
                LogoUrl = logo
            });
        })
        .RequireRateLimiting("otp");

        group.MapGet("/link-info", async (
            string token, CustomerAccessTokenService accessTokens, EmailBrandingService branding,
            BrandLogoInliner logoInliner) =>
        {
            var link = await accessTokens.GetTokenForDisplayAsync(token ?? "");
            if (link is null)
            {
                return Results.Ok(new LinkInfoResponse { Success = false });
            }
            var buId = link.Customer?.BusinessUnitId;
            return Results.Ok(new LinkInfoResponse
            {
                Success = true,
                BusinessUnitName = await branding.GetBusinessUnitNameAsync(buId),
                BusinessUnitEmail = await branding.GetBusinessUnitEmailAsync(buId),
                LogoUrl = await logoInliner.InlineIfUrlAsync(await branding.GetBusinessUnitUploadedLogoSrcAsync(buId))
            });
        })
        .RequireRateLimiting("otp");

        group.MapPost("/otp/verify", async (
            OtpVerifyRequest req, CustomerAccessTokenService accessTokens) =>
        {
            var t = await accessTokens.GetValidTokenAsync(req.LinkToken ?? "");
            if (t is null)
            {
                return Results.Ok(new CustomerOtpVerifyResponse { Success = false, Error = "This link is invalid or has expired." });
            }

            var (success, error, session) = await accessTokens.VerifyOtpAsync(t.Id, req.Code ?? "");
            if (!success || session is null)
            {
                return Results.Ok(new CustomerOtpVerifyResponse { Success = false, Error = error });
            }

            return Results.Ok(new CustomerOtpVerifyResponse
            {
                Success = true,
                SessionToken = session,
                CustomerName = t.Customer?.LegalName,
                ContactEmail = CustomerAccessTokenService.EmailFor(t.Customer)
            });
        })
        .RequireRateLimiting("otp");

        // ---- Data endpoints: require BOTH the portal token AND a valid customer link+session ----

        group.MapGet("/form", async (HttpContext ctx, ICustomerSelfServiceDataGateway data, BrandLogoInliner logoInliner) =>
        {
            var (link, session) = ReadSession(ctx);
            var form = await data.LoadFormAsync(link, session);
            return form is null ? Results.Unauthorized() : Results.Ok(await ToBootstrapAsync(form, logoInliner));
        });

        group.MapPut("/form", async (
            HttpContext ctx, SaveCustomerRequest req, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            var form = await data.LoadFormAsync(link, session);
            if (form is null)
            {
                return Results.Unauthorized();
            }
            ApplyValues(form.Customer, req.AccountValues, req.ContactValues);
            var result = await data.SaveAsync(link, session, form.Customer, complete: false);
            return Results.Ok(new SaveCustomerResponse
            {
                Success = result.Success,
                Errors = result.Success ? [] : [result.Error ?? "Could not save your changes."]
            });
        });

        group.MapPost("/submit", async (
            HttpContext ctx, SubmitCustomerRequest req, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            var form = await data.LoadFormAsync(link, session);
            if (form is null)
            {
                return Results.Unauthorized();
            }

            var customer = form.Customer;
            customer.DisclaimerAcknowledged = req.Acknowledged;
            customer.DisclaimerAcknowledgedAt = DateTime.UtcNow;
            customer.DisclaimerAcknowledgedBy = req.SignatureName;
            customer.DisclaimerSignature = req.SignatureName;
            if (!string.IsNullOrWhiteSpace(req.SignatureImageBase64))
            {
                try { customer.DisclaimerSignatureImage = Convert.FromBase64String(req.SignatureImageBase64); }
                catch (FormatException) { /* ignore a malformed signature image */ }
            }

            var result = await data.SaveAsync(link, session, customer, complete: true);
            return Results.Ok(new SubmitCustomerResponse
            {
                Success = result.Success,
                Error = result.Success ? null : result.Error
            });
        });

        // ---- Documents ----

        group.MapGet("/documents", async (HttpContext ctx, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            var docs = await data.ListDocumentsAsync(link, session);
            return Results.Ok(new CustomerDocumentUploadResponse { Success = true, Documents = docs.Select(ToDto).ToList() });
        });

        group.MapPost("/documents", async (HttpContext ctx, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            if (!ctx.Request.HasFormContentType)
            {
                return Results.BadRequest(new CustomerDocumentUploadResponse { Success = false, Error = "Expected a multipart form." });
            }

            var httpForm = await ctx.Request.ReadFormAsync();
            var file = httpForm.Files["file"];
            var documentType = httpForm["documentType"].ToString();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new CustomerDocumentUploadResponse { Success = false, Error = "No file provided." });
            }
            if (file.Length > MaxDocumentBytes)
            {
                return Results.BadRequest(new CustomerDocumentUploadResponse { Success = false, Error = "File exceeds the 10 MB size limit." });
            }
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return Results.BadRequest(new CustomerDocumentUploadResponse { Success = false, Error = "Missing document type." });
            }

            byte[] bytes;
            await using (var stream = file.OpenReadStream())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            var result = await data.UploadDocumentAsync(link, session, documentType, file.FileName, file.ContentType ?? "application/octet-stream", bytes);
            var current = await data.ListDocumentsAsync(link, session);
            return Results.Ok(new CustomerDocumentUploadResponse
            {
                Success = result.Success,
                Error = result.Error,
                Documents = current.Select(ToDto).ToList()
            });
        })
        .RequireRateLimiting("upload");

        group.MapGet("/documents/{id:int}", async (int id, HttpContext ctx, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            var file = await data.GetDocumentFileAsync(link, session, id);
            if (file is null)
            {
                return Results.NotFound();
            }
            var bytes = Convert.FromBase64String(file.BytesBase64);
            return Results.File(bytes, file.ContentType, file.FileName);
        });

        group.MapDelete("/documents/{id:int}", async (int id, HttpContext ctx, ICustomerSelfServiceDataGateway data) =>
        {
            var (link, session) = ReadSession(ctx);
            var result = await data.DeleteDocumentAsync(link, session, id);
            var current = await data.ListDocumentsAsync(link, session);
            return Results.Ok(new CustomerDocumentUploadResponse
            {
                Success = result.Success,
                Error = result.Error,
                Documents = current.Select(ToDto).ToList()
            });
        });
    }

    private static (string Link, string Session) ReadSession(HttpContext ctx) =>
        (ctx.Request.Headers[LinkHeader].ToString(), ctx.Request.Headers[SessionHeader].ToString());

    /// <summary>Writes the incoming FieldKey-&gt;value dictionaries onto the Customer (Account) and its
    /// Contact (Contact), using the same reflection accessor the internal form uses.</summary>
    private static void ApplyValues(Customer customer, Dictionary<string, string?> account, Dictionary<string, string?> contact)
    {
        var contactEntity = customer.Contact ??= new CustomerContact { CustomerId = customer.Id };
        foreach (var (key, value) in account)
        {
            CustomerFieldAccessor.SetString(customer, key, value);
        }
        foreach (var (key, value) in contact)
        {
            CustomerFieldAccessor.SetString(contactEntity, key, value);
        }
        customer.Contact = contactEntity;
    }

    private static async Task<CustomerFormBootstrapDto> ToBootstrapAsync(SsCustomerFormData form, BrandLogoInliner logoInliner)
    {
        var customer = form.Customer;
        var contact = customer.Contact ?? new CustomerContact { CustomerId = customer.Id };

        var dto = new CustomerFormBootstrapDto
        {
            CustomerId = customer.Id,
            CustomerCode = customer.CustomerCode,
            LegalName = customer.LegalName,
            Status = customer.Status,
            LogoUrl = await logoInliner.InlineIfUrlAsync(form.LogoSrc),
            FieldConfigs = form.Config.Select(c => new CustomerFieldConfigDto
            {
                FieldKey = c.FieldKey,
                DisplayLabel = c.DisplayLabel,
                Section = c.Section,
                GroupName = c.GroupName,
                Sequence = c.Sequence,
                FieldKind = c.FieldKind,
                CustomerControl = c.CustomerControl,
                IsVisible = c.IsVisible,
                IsRequired = c.IsRequired,
                ControllingFieldKey = c.ControllingFieldKey,
                ControllingValue = c.ControllingValue
            }).ToList(),
            Tabs = form.Tabs.Select(t => new CustomerTabDto
            {
                Section = t.Section,
                GroupName = t.GroupName,
                Title = t.Title,
                Sequence = t.Sequence,
                IsVisible = t.IsVisible
            }).ToList(),
            Documents = form.Documents.Select(ToDto).ToList(),
            DocumentTypes = form.DocTypes.Where(t => t.IsActive).OrderBy(t => t.Sequence).Select(t => t.Name).ToList()
        };

        foreach (var c in form.Config.Where(c => c.Section == "Account"))
        {
            dto.AccountValues[c.FieldKey] = CustomerFieldAccessor.GetString(customer, c.FieldKey);
        }
        foreach (var c in form.Config.Where(c => c.Section == "Contact"))
        {
            dto.ContactValues[c.FieldKey] = CustomerFieldAccessor.GetString(contact, c.FieldKey);
        }
        return dto;
    }

    private static CustomerDocumentDto ToDto(CustomerDocument d) => new()
    {
        Id = d.Id,
        DocumentType = d.DocumentType,
        FileName = d.FileName,
        ContentType = d.ContentType,
        UploadedAt = d.UploadedAt,
        FileSizeBytes = d.Content?.LongLength ?? 0   // 0 when the file lives in blob storage
    };
}
