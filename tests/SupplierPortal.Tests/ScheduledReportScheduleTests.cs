using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

public class ScheduledReportScheduleTests
{
    private static readonly DateTime Base = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Daily_advances_one_day_when_just_fired()
    {
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Daily, Base);
        Assert.Equal(Base.AddDays(1), next);
    }

    [Fact]
    public void Weekly_advances_seven_days()
    {
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Weekly, Base);
        Assert.Equal(Base.AddDays(7), next);
    }

    [Fact]
    public void Monthly_advances_one_month_preserving_time()
    {
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Monthly, Base);
        Assert.Equal(new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Quarterly_advances_three_months()
    {
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Quarterly, Base);
        Assert.Equal(new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Yearly_advances_one_year()
    {
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Yearly, Base);
        Assert.Equal(new DateTime(2027, 1, 10, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Skips_missed_occurrences_to_first_future_run()
    {
        // Scheduled daily starting 10 days ago; after a downtime the next run should be the first
        // occurrence strictly after "now", not a burst of ten past runs.
        var now = Base.AddDays(10).AddHours(2);
        var next = ScheduledReportService.AdvancePast(Base, ReportFrequency.Daily, now);
        Assert.True(next > now);
        Assert.Equal(new DateTime(2026, 1, 21, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void TimeZone_round_trips_local_and_utc()
    {
        ReportTimeZone.Configure("Asia/Dubai");           // UTC+4, no DST
        var local = new DateTime(2026, 3, 1, 9, 0, 0);    // 09:00 Dubai
        var utc = ReportTimeZone.ToUtc(local);
        Assert.Equal(new DateTime(2026, 3, 1, 5, 0, 0), utc); // 05:00 UTC
        Assert.Equal(local, ReportTimeZone.ToLocal(utc));
    }
}
