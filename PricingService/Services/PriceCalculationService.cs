using Insurance.Graftcode;
using PricingService.Repositories;

namespace PricingService.Services;

public static class PriceCalculationService
{
    // Graftcode-facing method replacing POST /api/Pricing.
    public static string CalculatePrice(
        string productCode,
        string policyFrom,
        string policyTo,
        string selectedCovers,
        string answers) =>
        GraftAsync.Run(() => Repository().CalculateAsync(productCode, policyFrom, policyTo, selectedCovers, answers));

    private static SqlTariffRepository Repository() =>
        new(Required("PRICING_CN"));

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
