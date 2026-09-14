using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Data operations the supplier self-service form needs, all scoped by the opaque link token +
/// OTP session (never internal ids). The Local implementation talks to the database; the DMZ (Http)
/// implementation calls the internal Self-Service API. Together with
/// <see cref="ISupplierSelfServiceAccessGateway"/> this lets the DMZ run with no database access.</summary>
public interface ISupplierSelfServiceDataGateway
{
    /// <summary>Validates the session and returns the supplier record plus all reference data, field
    /// configuration and documents needed to render the form in one round-trip. Null if the session is
    /// invalid/expired.</summary>
    Task<SsFormData?> LoadFormAsync(string token, string session);

    /// <summary>Persists the supplier record for this session. <paramref name="complete"/> = false is an
    /// intermediate step-save; true is the final submit, which auto-marks the record Completed, notifies the
    /// requestor and the supplier, and expires the link. All side-effects run on the internal side.</summary>
    Task<SsSaveResult> SaveAsync(string token, string session, Supplier updated, bool complete);

    Task<List<SupplierDocument>> ListDocumentsAsync(string token, string session);

    Task<SsUploadResult> UploadDocumentAsync(
        string token, string session, int documentType, string fileName, string contentType, byte[] bytes);

    Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId);

    Task<SsResult> DeleteDocumentAsync(string token, string session, int documentId);
}
