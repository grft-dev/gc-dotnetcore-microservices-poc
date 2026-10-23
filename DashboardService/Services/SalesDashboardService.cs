using Insurance.Graftcode;
using DashboardService.Repositories;

namespace DashboardService.Services;

public static class SalesDashboardService
{
    // Graftcode-facing method replacing POST /api/Dashboard/agents-sales.
    public static string[] GetAgentsSales(
        string agentLogin,
        string productCode,
        string salesDateFrom,
        string salesDateTo) =>
        GraftAsync.Run(() => Repository().GetAgentsSalesAsync(agentLogin, productCode, salesDateFrom, salesDateTo));

    // Graftcode-facing method replacing POST /api/Dashboard/total-sales.
    public static string[] GetTotalSales(string productCode, string salesDateFrom, string salesDateTo) =>
        GraftAsync.Run(() => Repository().GetTotalSalesAsync(productCode, salesDateFrom, salesDateTo));

    // Graftcode-facing method replacing POST /api/Dashboard/sales-trends.
    public static string[] GetSalesTrends(
        string productCode,
        string salesDateFrom,
        string salesDateTo,
        string unit) =>
        GraftAsync.Run(() => Repository().GetSalesTrendsAsync(productCode, salesDateFrom, salesDateTo, unit));

    private static SqlDashboardRepository Repository() =>
        new(Required("DASHBOARD_CN"));

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {key} environment variable.");
        }

        return value;
    }
}
