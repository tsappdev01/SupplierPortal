using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Read side of the Land Register. Reads the read-only land view <c>dbo.Property</c> (<see cref="Premises"/>)
/// and overlays owner-assignment status from the app-owned <c>leasing.LandlordPremises</c> table.
/// Land data is never written here.
/// </summary>
public class LandRegisterService(IDbContextFactory<LeasingDbContext> dbFactory, IOptions<LeasingOptions> options)
{
    private readonly LeasingOptions _options = options.Value;

    public IReadOnlyList<string> Zones => _options.Zones;

    /// <summary>Resolves the caller's requested zone to a configured one, defaulting to the first.</summary>
    public string ResolveZone(string? requested) =>
        _options.Zones.FirstOrDefault(z => z == requested) ?? _options.Zones.FirstOrDefault() ?? "597";

    /// <summary>
    /// Builds the Land Register for one zone: property codes, each with its buildings (premises) and the
    /// current owner assignment(s). Optional case-insensitive search over property code and building name.
    /// </summary>
    public async Task<LandRegisterView> GetRegisterAsync(string zone, string? search = null)
    {
        zone = ResolveZone(zone);
        await using var db = await dbFactory.CreateDbContextAsync();

        // Zone can't be filtered in SQL (it's derived). Every valid code is "{zone}-{remainder}", so match
        // the "{zone}-" prefix — this excludes look-alikes such as "598285-…" that merely start with the digits.
        var zonePrefix = zone + "-";
        var query = db.Premises.AsNoTracking().Where(p => p.PropertyCode.StartsWith(zonePrefix));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.PropertyCode.Contains(term) || (p.BuildingName != null && p.BuildingName.Contains(term)));
        }

        var premises = await query
            .OrderBy(p => p.PropertyCode).ThenBy(p => p.BuildingName)
            .ToListAsync();

        var premisesIds = premises.Select(p => p.Id).ToList();

        // Owner assignments for just these premises (co-ownership => possibly several per premises).
        var assignments = await (
            from lp in db.LandlordPremises.AsNoTracking()
            join l in db.Landlords.AsNoTracking() on lp.LandlordId equals l.Id
            where premisesIds.Contains(lp.PremisesId)
            select new { lp.PremisesId, l.Id, l.LegalName, l.Status })
            .ToListAsync();

        var ownersByPremises = assignments
            .GroupBy(a => a.PremisesId)
            .ToDictionary(g => g.Key, g => g
                .Select(a => new PremisesOwner(a.Id, a.LegalName, a.Status))
                .ToList());

        var groups = premises
            .GroupBy(p => p.PropertyCode)
            .Select(g => new LandRegisterProperty(
                PropertyCode: g.Key,
                Zone: zone,
                CategoryName: g.Select(p => p.CategoryName).FirstOrDefault(c => c != null),
                Buildings: g.Select(p => new LandRegisterBuilding(
                        PremisesId: p.Id,
                        BuildingName: p.BuildingName,
                        Owners: ownersByPremises.TryGetValue(p.Id, out var o) ? o : []))
                    .ToList()))
            .OrderBy(p => p.PropertyCode)
            .ToList();

        return new LandRegisterView(
            Zone: zone,
            Properties: groups,
            TotalBuildings: premises.Count,
            AssignedBuildings: premises.Count(p => ownersByPremises.ContainsKey(p.Id)));
    }

    /// <summary>Flat premises search for the owner-assignment picker: matches property code or building
    /// within a zone, optionally excluding premises already assigned to a given landlord.</summary>
    public async Task<List<Premises>> SearchPremisesAsync(string zone, string? term, int? excludeLandlordId = null, int take = 25)
    {
        zone = ResolveZone(zone);
        await using var db = await dbFactory.CreateDbContextAsync();
        var zonePrefix = zone + "-";
        var query = db.Premises.AsNoTracking().Where(p => p.PropertyCode.StartsWith(zonePrefix));

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(p => p.PropertyCode.Contains(t) || (p.BuildingName != null && p.BuildingName.Contains(t)));
        }

        if (excludeLandlordId is int lid)
        {
            var assigned = db.LandlordPremises.Where(lp => lp.LandlordId == lid).Select(lp => lp.PremisesId);
            query = query.Where(p => !assigned.Contains(p.Id));
        }

        return await query.OrderBy(p => p.PropertyCode).ThenBy(p => p.BuildingName).Take(take).ToListAsync();
    }
}

/// <summary>The Land Register for a single zone.</summary>
public record LandRegisterView(string Zone, List<LandRegisterProperty> Properties, int TotalBuildings, int AssignedBuildings)
{
    public int PropertyCount => Properties.Count;
    public int UnassignedBuildings => TotalBuildings - AssignedBuildings;
}

public record LandRegisterProperty(string PropertyCode, string Zone, string? CategoryName, List<LandRegisterBuilding> Buildings)
{
    public int AssignedCount => Buildings.Count(b => b.Owners.Count > 0);
}

public record LandRegisterBuilding(int PremisesId, string? BuildingName, List<PremisesOwner> Owners);

public record PremisesOwner(int LandlordId, string LegalName, LandlordStatus Status);
