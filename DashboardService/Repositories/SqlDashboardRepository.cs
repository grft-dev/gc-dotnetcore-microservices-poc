using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace DashboardService.Repositories;

public sealed class SqlDashboardRepository
{
    private readonly string _connectionString;

    public SqlDashboardRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string[]> GetAgentsSalesAsync(
        string? agentLogin,
        string? productCode,
        string? salesDateFrom,
        string? salesDateTo)
    {
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<AgentRow>(
            """
            select AgentLogin, count(*) as PoliciesCount, sum(Premium) as PremiumAmount
            from SalesFact
            where (@Agent = N'' or AgentLogin = @Agent)
              and (@Product = N'' or ProductCode = @Product)
              and (@From is null or SalesDate >= @From)
              and (@To is null or SalesDate <= @To)
            group by AgentLogin
            order by AgentLogin
            """,
            Filter(agentLogin, productCode, salesDateFrom, salesDateTo));
        return rows.Select(r => $"{r.AgentLogin}|{r.PoliciesCount}|{Money(r.PremiumAmount)}").ToArray();
    }

    public async Task<string[]> GetTotalSalesAsync(string? productCode, string? salesDateFrom, string? salesDateTo)
    {
        await using var conn = new SqlConnection(_connectionString);
        var rows = (await conn.QueryAsync<ProductRow>(
            """
            select ProductCode, count(*) as PoliciesCount, sum(Premium) as PremiumAmount
            from SalesFact
            where (@Product = N'' or ProductCode = @Product)
              and (@From is null or SalesDate >= @From)
              and (@To is null or SalesDate <= @To)
            group by ProductCode
            order by ProductCode
            """,
            Filter(string.Empty, productCode, salesDateFrom, salesDateTo))).ToList();
        var lines = new List<string>
        {
            $"total|{rows.Sum(r => r.PoliciesCount)}|{Money(rows.Sum(r => r.PremiumAmount))}"
        };
        lines.AddRange(rows.Select(r => $"{r.ProductCode}|{r.PoliciesCount}|{Money(r.PremiumAmount)}"));
        return lines.ToArray();
    }

    public async Task<string[]> GetSalesTrendsAsync(
        string? productCode,
        string? salesDateFrom,
        string? salesDateTo,
        string? unit)
    {
        if (unit is not ("Day" or "Week" or "Month" or "Year"))
        {
            throw new InvalidOperationException($"Invalid value of unit {unit}");
        }

        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<PeriodRow>(
            """
            select PeriodStart, count(*) as PoliciesCount, sum(Premium) as PremiumAmount
            from (
                select
                    case @Unit
                        when N'Day' then SalesDate
                        when N'Week' then dateadd(day, -(datediff(day, convert(date, '19000101', 112), SalesDate) % 7), SalesDate)
                        when N'Month' then datefromparts(year(SalesDate), month(SalesDate), 1)
                        when N'Year' then datefromparts(year(SalesDate), 1, 1)
                    end as PeriodStart,
                    Premium
                from SalesFact
                where (@Product = N'' or ProductCode = @Product)
                  and (@From is null or SalesDate >= @From)
                  and (@To is null or SalesDate <= @To)
            ) grouped
            group by PeriodStart
            order by PeriodStart
            """,
            new
            {
                Unit = unit,
                Product = productCode ?? string.Empty,
                From = ParseOptionalDate(salesDateFrom),
                To = ParseOptionalDate(salesDateTo)
            });
        return rows.Select(r => $"{Date(r.PeriodStart)}|{r.PoliciesCount}|{Money(r.PremiumAmount)}").ToArray();
    }

    private static object Filter(string? agent, string? product, string? from, string? to) =>
        new
        {
            Agent = agent ?? string.Empty,
            Product = product ?? string.Empty,
            From = ParseOptionalDate(from),
            To = ParseOptionalDate(to)
        };

    private static DateTime? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException($"Invalid date '{value}'.");
        }

        return date;
    }

    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class AgentRow
    {
        public string AgentLogin { get; set; } = string.Empty;
        public int PoliciesCount { get; set; }
        public decimal PremiumAmount { get; set; }
    }

    private sealed class ProductRow
    {
        public string ProductCode { get; set; } = string.Empty;
        public int PoliciesCount { get; set; }
        public decimal PremiumAmount { get; set; }
    }

    private sealed class PeriodRow
    {
        public DateTime PeriodStart { get; set; }
        public int PoliciesCount { get; set; }
        public decimal PremiumAmount { get; set; }
    }
}
