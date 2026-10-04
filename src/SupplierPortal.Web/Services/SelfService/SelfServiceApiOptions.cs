namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Configuration for the DMZ↔internal Self-Service API channel, bound from the
/// "SelfServiceApi" section.</summary>
public class SelfServiceApiOptions
{
    public const string SectionName = "SelfServiceApi";

    /// <summary>Base URL of the internal Self-Service API, used by the DMZ HTTP-client gateways
    /// (e.g. https://bp-api.dipark.com). Required when Deployment:Mode = Dmz.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Shared secret the DMZ presents in the <c>X-SelfService-Key</c> header and the internal
    /// API validates. In production prefer mutual TLS in addition to (or instead of) this key.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Header name carrying the API key.</summary>
    public const string ApiKeyHeader = "X-SelfService-Key";
}
