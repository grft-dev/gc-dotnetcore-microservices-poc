using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using PricingService.Domain;

namespace PricingService.Repositories;

public sealed class SqlTariffRepository
{
    private readonly string _connectionString;

    public SqlTariffRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> CalculateAsync(
        string productCode,
        string policyFrom,
        string policyTo,
        string selectedCovers,
        string answers)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new InvalidOperationException("ProductCode must not be empty.");
        }

        var from = ParseDate(policyFrom);
        var to = ParseDate(policyTo);
        await using var conn = new SqlConnection(_connectionString);
        var ruleCount = await conn.ExecuteScalarAsync<int>(
            "select count(1) from TariffBaseRule where ProductCode = @ProductCode",
            new { ProductCode = productCode });
        if (ruleCount == 0)
        {
            throw new InvalidOperationException($"Tariff {productCode} not found.");
        }

        var baseRules = (await conn.QueryAsync<BaseRule>(
            """
            select CoverCode, ApplyIfFormula, BasePriceFormula
            from TariffBaseRule
            where ProductCode = @ProductCode
            order by SortOrder
            """,
            new { ProductCode = productCode })).ToList();
        var markups = (await conn.QueryAsync<MarkupRule>(
            """
            select ApplyIfFormula, ParamValue
            from TariffMarkupRule
            where ProductCode = @ProductCode
            order by SortOrder
            """,
            new { ProductCode = productCode })).ToList();

        var prices = TariffCalculator.Calculate(
            from,
            to,
            TariffCalculator.ParseCovers(selectedCovers),
            TariffCalculator.ParseAnswers(answers),
            baseRules,
            markups);
        return TariffCalculator.Format(prices);
    }

    private static DateTime ParseDate(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException($"Invalid date '{value}'.");
        }

        return date;
    }
}
