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
                MaskedEmail = SupplierAccessTokenService.MaskEmail(t.Customer is null ? null : CustomerAccessTokenService.EmailFor(t.Customer)),
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
                BusinessUnitEmail = await branding.GetPortalSenderEmailAsync(buId, Models.PortalType.Customer),
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
                ContactEmail = t.Customer is null ? null : CustomerAccessTokenService.EmailFor(t.Customer)
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

        // Save uses POST (not PUT): the DMZ→internal path returns HTTP 405 for PUT/DELETE (App Service / WAF /
        // reverse-proxy method restriction), which silently dropped per-step saves. POST is not restricted.
        group.MapPost("/form/save", async (
            HttpContext ctx, SaveCustomerRequest req, ICustomerSelfServiceDataGateway data, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("CustomerSelfServiceSave");
            var (link, session) = ReadSession(ctx);
            var form = await data.LoadFormAsync(link, session);
            if (form is null)
            {
                log.LogWarning("Customer save: session/link invalid — no form loaded.");
                return Results.Unauthorized();
            }
            log.LogInformation(
                "Customer save IN: customerId={Id}, accountKeys={A}, contactKeys={C}, in.CommercialName='{CN}', in.NumberOfEntities='{NE}'",
                form.Customer.Id, req.AccountValues.Count, req.ContactValues.Count,
                req.AccountValues.GetValueOrDefault("CommercialName"), req.AccountValues.GetValueOrDefault("NumberOfEntities"));
            ApplyValues(form, req.AccountValues, req.ContactValues);
            ApplyVatGroup(form.Customer, req.VatGroupEntities);
            var result = await data.SaveAsync(link, session, form.Customer, complete: false);
            log.LogInformation(
                "Customer save OUT: success={S}, error='{E}', persisted.CommercialName='{CN}', persisted.NumberOfEntities='{NE}'",
                result.Success, result.Error, result.Saved?.CommercialName, result.Saved?.NumberOfEntities);
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

            // Server-side re-validation (defence in depth — the client validates too): a submission must be
            // signed and have every required visible field and required document before it can complete.
            if (!req.Acknowledged || string.IsNullOrWhiteSpace(req.SignatureName))
            {
                return Results.Ok(new SubmitCustomerResponse { Success = false, Error = "The declaration must be signed before submitting." });
            }
            var problems = ValidateForSubmit(form);
            if (problems.Count > 0)
            {
                return Results.Ok(new SubmitCustomerResponse { Success = false, Error = string.Join(" ", problems) });
            }

            var customer = form.Customer;
            customer.DisclaimerAcknowledged = true;
            customer.DisclaimerAcknowledgedAt = DateTime.UtcNow;
            customer.DisclaimerAcknowledgedBy = req.SignatureName;
            customer.DisclaimerSignature = req.SignatureName;
            customer.DisclaimerText = DeclarationText;
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

        group.MapPost("/documents/{id:int}/delete", async (int id, HttpContext ctx, ICustomerSelfServiceDataGateway data) =>
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

    /// <summary>Writes the incoming FieldKey-&gt;value dictionaries onto the Customer (Account) and its Contact,
    /// but ONLY for fields the configuration marks visible + customer-editable (never Readonly/removed and never
    /// a scaffolding/workflow field such as Status, IsDraft, BaanRef, CustomerId, CustomerCode, CreatedBy — none
    /// of which are field-config entries). This prevents a crafted self-service payload from mass-assigning
    /// locked or workflow fields via reflection.</summary>
    private static void ApplyValues(SsCustomerFormData form, Dictionary<string, string?> account, Dictionary<string, string?> contact)
    {
        var customer = form.Customer;
        var contactEntity = customer.Contact ??= new CustomerContact { CustomerId = customer.Id };

        var editableAccount = EditableKeys(form.Config, "Account");
        // The combined Country control writes CountryName alongside the editable CountryCode.
        if (editableAccount.Contains("CountryCode")) { editableAccount.Add("CountryName"); }
        var editableContact = EditableKeys(form.Config, "Contact");

        foreach (var (key, value) in account)
        {
            if (editableAccount.Contains(key)) { CustomerFieldAccessor.SetString(customer, key, value); }
        }
        foreach (var (key, value) in contact)
        {
            if (editableContact.Contains(key)) { CustomerFieldAccessor.SetString(contactEntity, key, value); }
        }
        customer.Contact = contactEntity;
    }

    /// <summary>Replaces the customer's in-memory VAT-group entity list from the incoming DTOs. Persistence
    /// (and clearing when VAT Structure is not "VAT Group") is handled by <c>CustomerService.SaveAsync</c>.</summary>
    private static void ApplyVatGroup(Customer customer, List<CustomerVatGroupEntityDto>? entities)
    {
        customer.VatGroupEntities = (entities ?? [])
            .Select((e, i) => new CustomerVatGroupEntity
            {
                CustomerId = customer.Id,
                BusinessLegalName = e.BusinessLegalName,
                Address = e.Address,
                EInvoicingWave = e.EInvoicingWave,
                TrnNumber = e.TrnNumber,
                TinNumber = e.TinNumber,
                BusinessLocationType = e.BusinessLocationType,
                SortOrder = i
            }).ToList();
    }

    private static CustomerVatGroupEntityDto ToVatDto(CustomerVatGroupEntity e) => new()
    {
        Id = e.Id,
        BusinessLegalName = e.BusinessLegalName,
        Address = e.Address,
        EInvoicingWave = e.EInvoicingWave,
        TrnNumber = e.TrnNumber,
        TinNumber = e.TinNumber,
        BusinessLocationType = e.BusinessLocationType,
        SortOrder = e.SortOrder
    };

    /// <summary>The declaration wording recorded against a completed submission (mirrors the DMZ form text).</summary>
    private const string DeclarationText =
        "Customer Declaration\n\nCustomer Data Gathering Portal\n\n" +
        "Declaration (For true and correct information provided):\n\n" +
        "I/We hereby declare that the information and data provided in this form are true, correct, and complete " +
        "to the best of our knowledge, and are furnished in compliance with the applicable UAE regulatory and " +
        "e-invoicing requirements issued by the Ministry of Finance / Federal Tax Authority. We undertake to " +
        "promptly inform Dubai Investments of any changes to the above.\n\n" +
        "By signing below, I/We acknowledge and accept the declaration set out above.";

    /// <summary>Required visible fields (honouring conditional visibility) and required documents that must be
    /// present before a self-service submission can complete.</summary>
    private static List<string> ValidateForSubmit(SsCustomerFormData form)
    {
        var customer = form.Customer;
        var contact = customer.Contact ?? new CustomerContact();
        object Ent(CustomerFieldConfig f) => f.Section == "Account" ? (object)customer : contact;

        bool Visible(CustomerFieldConfig f)
        {
            if (string.IsNullOrWhiteSpace(f.ControllingFieldKey)) { return true; }
            var cur = CustomerFieldAccessor.GetString(Ent(f), f.ControllingFieldKey);
            return string.Equals(cur, f.ControllingValue, StringComparison.OrdinalIgnoreCase);
        }

        var problems = new List<string>();
        foreach (var f in form.Config
                     .Where(f => f.IsVisible && f.IsRequired && !f.IsRemoved)
                     .Where(Visible)
                     .Where(f => !(f.Section == "Account" && f.FieldKey == "CountryName")))
        {
            if (string.IsNullOrWhiteSpace(CustomerFieldAccessor.GetString(Ent(f), f.FieldKey)))
            {
                problems.Add($"{f.DisplayLabel} is required.");
            }
        }

        // Configurable min/max character length on editable text fields (defence in depth — the forms check too).
        foreach (var f in form.Config.Where(f => f.IsVisible && !f.IsRemoved).Where(Visible)
                     .Where(f => string.IsNullOrEmpty(f.FieldKind) || f.FieldKind == "text"))
        {
            var val = CustomerFieldAccessor.GetString(Ent(f), f.FieldKey);
            if (string.IsNullOrEmpty(val)) { continue; }
            var len = val.Trim().Length;
            if (f.MinLength is int min && min > 0 && len < min)
            {
                problems.Add($"{f.DisplayLabel} must be at least {min} character{(min == 1 ? "" : "s")}.");
            }
            if (f.MaxLength is int max && max > 0 && len > max)
            {
                problems.Add($"{f.DisplayLabel} must be at most {max} character{(max == 1 ? "" : "s")}.");
            }
        }

        // Date logic: an issue date cannot be in the future and an expiry cannot precede its issue date.
        foreach (var (section, issueKey, expiryKey, label) in IssueExpiryPairs)
        {
            var ent = section == "Account" ? (object)customer : contact;
            var iss = ParseDate(CustomerFieldAccessor.GetString(ent, issueKey));
            var exp = ParseDate(CustomerFieldAccessor.GetString(ent, expiryKey));
            if (iss is { } i && i.Date > DateTime.Today) { problems.Add($"{label} Issue Date cannot be a future date."); }
            if (iss is { } i2 && exp is { } e && e.Date < i2.Date) { problems.Add($"{label} Expiry Date cannot be before the {label} Issue Date."); }
        }

        // VAT Group: when VAT Structure = "VAT Group", require at least one entity with every column filled.
        if (string.Equals(customer.VATStructure, "VAT Group", StringComparison.OrdinalIgnoreCase))
        {
            if (customer.VatGroupEntities.Count == 0)
            {
                problems.Add("VAT Group: add at least one entity.");
            }
            else
            {
                var i = 1;
                foreach (var e in customer.VatGroupEntities.OrderBy(x => x.SortOrder))
                {
                    if (string.IsNullOrWhiteSpace(e.BusinessLegalName) || string.IsNullOrWhiteSpace(e.Address)
                        || string.IsNullOrWhiteSpace(e.EInvoicingWave) || string.IsNullOrWhiteSpace(e.TrnNumber)
                        || string.IsNullOrWhiteSpace(e.TinNumber) || string.IsNullOrWhiteSpace(e.BusinessLocationType))
                    {
                        problems.Add($"VAT Group row {i}: all columns are required.");
                    }
                    i++;
                }
            }
        }

        var have = form.Documents.Select(d => d.DocumentType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var t in form.DocTypes.Where(t => t.IsActive && t.IsRequired))
        {
            if (!have.Contains(t.Name)) { problems.Add($"{t.Name} document is required."); }
        }
        return problems;
    }

    /// <summary>Issue/expiry date-field pairs whose expiry must not precede the issue date (and whose issue
    /// date cannot be in the future). Section says which entity the keys live on.</summary>
    private static readonly (string Section, string Issue, string Expiry, string Label)[] IssueExpiryPairs =
    {
        ("Account", "TradeLicenseIssueDate", "TradeLicenseExpiryDate", "Trade License"),
        ("Contact", "PassportIssueDate", "PassportExpiryDate", "Passport"),
        ("Contact", "DualPassportIssueDate", "DualPassportExpiryDate", "Dual Passport"),
        ("Contact", "CountryGovtIDIssueDate", "CountryGovtIDExpiryDate", "Country Govt ID"),
        ("Contact", "EmiratesIDIssueDate", "EmirateIDExpiryDate", "Emirates ID"),
    };

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;

    private static HashSet<string> EditableKeys(IEnumerable<CustomerFieldConfig> config, string section) =>
        config.Where(c => c.Section == section && c.IsVisible && !c.IsRemoved
                          && !string.Equals(c.CustomerControl, CustomerFieldControls.Readonly, StringComparison.OrdinalIgnoreCase))
              .Select(c => c.FieldKey)
              .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                HelpText = c.HelpText,
                MinLength = c.MinLength,
                MaxLength = c.MaxLength,
                Section = c.Section,
                GroupName = c.GroupName,
                Sequence = c.Sequence,
                FieldKind = c.FieldKind,
                CustomerControl = c.CustomerControl,
                IsVisible = c.IsVisible,
                // The DMZ form is customer-facing, so "required" here means required-for-the-customer.
                IsRequired = c.IsRequiredForCustomer,
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
            VatGroupEntities = customer.VatGroupEntities.OrderBy(e => e.SortOrder).Select(ToVatDto).ToList(),
            Documents = form.Documents.Select(ToDto).ToList(),
            DocumentTypes = form.DocTypes.Where(t => t.IsActive).OrderBy(t => t.Sequence)
                .Select(t => new CustomerDocumentTypeDto
                {
                    Name = t.Name,
                    Requirement = t.Requirement,
                    IsRequired = t.IsRequired,
                    Sequence = t.Sequence
                }).ToList()
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
