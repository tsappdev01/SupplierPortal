using SupplierPortal.Web.Models;
using Xunit;

namespace SupplierPortal.Tests;

public class ReportDefinitionTests
{
    [Fact]
    public void Columns_round_trip_in_order()
    {
        var d = new ReportDefinition();
        d.SetColumns(["SupplierNumber", "SupplierName", "Status"]);
        Assert.Equal(["SupplierNumber", "SupplierName", "Status"], d.GetColumns());
    }

    [Fact]
    public void Empty_filters_serialise_to_null()
    {
        var d = new ReportDefinition();
        d.SetFilters(new ReportFilterModel());
        Assert.Null(d.FiltersJson);
        Assert.Empty(d.GetFilters().Conditions);
    }

    [Fact]
    public void Filters_round_trip_with_match_and_conditions()
    {
        var d = new ReportDefinition();
        d.SetFilters(new ReportFilterModel
        {
            Match = FilterMatch.Or,
            Conditions =
            [
                new ReportFilterCondition { Column = "Status", Operator = FilterOperator.Equals, Value = "Active" },
                new ReportFilterCondition { Column = "SupplierName", Operator = FilterOperator.Contains, Value = "LLC" },
            ]
        });

        var back = d.GetFilters();
        Assert.Equal(FilterMatch.Or, back.Match);
        Assert.Equal(2, back.Conditions.Count);
        Assert.Equal("Status", back.Conditions[0].Column);
        Assert.Equal(FilterOperator.Contains, back.Conditions[1].Operator);
        Assert.Equal("LLC", back.Conditions[1].Value);
    }

    [Fact]
    public void Malformed_json_is_tolerated()
    {
        var d = new ReportDefinition { ColumnsJson = "not-json", FiltersJson = "also-not-json" };
        Assert.Empty(d.GetColumns());
        Assert.Empty(d.GetFilters().Conditions);
    }
}
