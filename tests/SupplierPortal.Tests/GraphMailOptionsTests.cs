using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

public class GraphMailOptionsTests
{
    // EmailDispatcher uses IsConfigured as the single signal to prefer Microsoft Graph; it must be
    // true only when every value the client-credentials flow needs (tenant, client, secret, sender)
    // is present, so a partially-filled Email:Graph section never accidentally routes mail to Graph.
    [Fact]
    public void IsConfigured_true_only_when_all_four_values_are_set()
    {
        var full = new GraphMailOptions
        {
            TenantId = "t", ClientId = "c", ClientSecret = "s", SenderUpn = "noreply@x.com"
        };
        Assert.True(full.IsConfigured);

        Assert.False(new GraphMailOptions().IsConfigured); // all blank (default) = disabled
        Assert.False(new GraphMailOptions { TenantId = "t", ClientId = "c", SenderUpn = "noreply@x.com" }.IsConfigured); // no secret
        Assert.False(new GraphMailOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" }.IsConfigured); // no sender
        Assert.False(new GraphMailOptions { TenantId = "  ", ClientId = "c", ClientSecret = "s", SenderUpn = "x" }.IsConfigured); // whitespace tenant
    }
}
