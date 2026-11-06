using Insurance.Graftcode;
using PaymentService.Repositories;

namespace PaymentService.Services;

public static class PaymentAccountService
{
    // Graftcode-facing method replacing GET /api/Payment/accounts/{policyNumber}.
    public static string GetAccountBalance(string policyNumber) =>
        GraftAsync.Run(() => Repository().GetBalanceAsync(policyNumber));

    private static SqlPaymentRepository Repository() =>
        new(Required("PAYMENT_CN"));

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
