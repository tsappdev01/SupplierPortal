using System.Security.Claims;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

public class PortalAccessTests
{
    private static ClaimsPrincipal WithPortals(params PortalAccess[] portals)
    {
        var id = new ClaimsIdentity("test");
        foreach (var p in portals) id.AddClaim(new Claim(AppClaims.Portal, p.ToString()));
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public void CanAccessPortal_true_only_for_granted_portals()
    {
        var user = WithPortals(PortalAccess.Supplier, PortalAccess.Landlord);
        Assert.True(user.CanAccessPortal(PortalAccess.Supplier));
        Assert.True(user.CanAccessPortal(PortalAccess.Landlord));
        Assert.False(user.CanAccessPortal(PortalAccess.Customer));
        Assert.False(user.CanAccessPortal(PortalAccess.Broker));
    }

    [Fact]
    public void IsLeasingUser_follows_landlord_grant()
    {
        Assert.True(WithPortals(PortalAccess.Landlord).IsLeasingUser());
        Assert.False(WithPortals(PortalAccess.Supplier).IsLeasingUser());
    }

    [Fact]
    public void No_portal_claims_means_no_access()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));
        Assert.False(user.CanAccessPortal(PortalAccess.Supplier));
        Assert.False(user.IsLeasingUser());
    }

    [Fact]
    public void PortalAccess_flags_combine_and_test_correctly()
    {
        var access = PortalAccess.Supplier | PortalAccess.Landlord;
        Assert.True(access.HasFlag(PortalAccess.Supplier));
        Assert.True(access.HasFlag(PortalAccess.Landlord));
        Assert.False(access.HasFlag(PortalAccess.Customer));
    }
}
