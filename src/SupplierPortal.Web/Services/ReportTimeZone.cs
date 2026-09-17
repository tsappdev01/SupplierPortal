namespace SupplierPortal.Web.Services;

/// <summary>Converts between the administrator's local wall-clock time and UTC for report schedules.
/// The portal serves the UAE, so the default zone is Gulf Standard Time (UTC+4, no daylight saving);
/// override it with the <c>Reports:TimeZone</c> configuration key (IANA "Asia/Dubai" or Windows
/// "Arabian Standard Time" both work on .NET 8). Schedules are stored in UTC and displayed/entered in
/// this local zone.</summary>
public static class ReportTimeZone
{
    private static TimeZoneInfo _tz = Resolve("Asia/Dubai");

    /// <summary>Sets the schedule time zone from configuration at startup. Falls back to Gulf Standard
    /// Time if the id is blank or not found.</summary>
    public static void Configure(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            _tz = Resolve(timeZoneId);
        }
    }

    public static string DisplayName => _tz.Id;

    /// <summary>Interprets a local (unspecified-kind) date/time in the configured zone and returns its UTC.</summary>
    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz);

    /// <summary>Converts a UTC date/time to local wall-clock in the configured zone.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

    private static TimeZoneInfo Resolve(string id)
    {
        // Try the requested id, then the cross-platform aliases, then a fixed +4 offset.
        foreach (var candidate in new[] { id, "Asia/Dubai", "Arabian Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("GST", TimeSpan.FromHours(4), "Gulf Standard Time", "Gulf Standard Time");
    }
}
