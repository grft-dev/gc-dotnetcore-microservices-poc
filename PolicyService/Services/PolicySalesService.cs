using Insurance.Graftcode;
using PolicyService.Repositories;
using PricingService.Repositories;

namespace PolicyService.Services;

public static class PolicySalesService
{
    // Graftcode-facing method replacing POST /api/Offer.
    public static string CreateOffer(
        string productCode,
        string policyFrom,
        string policyTo,
        string selectedCovers,
        string answers,
        string agentLogin) =>
        GraftAsync.Run(async () =>
        {
            var price = await new SqlTariffRepository(Required("PRICING_CN")).CalculateAsync(
                productCode, policyFrom, policyTo, selectedCovers, answers);
            return await Repository().CreateOfferAsync(productCode, policyFrom, policyTo, price, agentLogin);
        });

    // Graftcode-facing method replacing POST /api/Policy.
    public static string CreatePolicy(
        string offerNumber,
        string firstName,
        string lastName,
        string taxId,
        string country,
        string zipCode,
        string city,
        string street) =>
        GraftAsync.Run(() => Repository().CreatePolicyAsync(
            offerNumber, firstName, lastName, taxId, country, zipCode, city, street));

    // Graftcode-facing method replacing GET /api/Policy/{policyNumber}.
    public static string GetPolicyDetails(string policyNumber) =>
        GraftAsync.Run(() => Repository().GetPolicyDetailsAsync(policyNumber));

    // Graftcode-facing method replacing DELETE /terminate.
    public static string TerminatePolicy(string policyNumber, string terminationDate) =>
        GraftAsync.Run(() => Repository().TerminateAsync(policyNumber, terminationDate));

    private static SqlPolicyRepository Repository() =>
        new(Required("POLICY_CN"));

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
