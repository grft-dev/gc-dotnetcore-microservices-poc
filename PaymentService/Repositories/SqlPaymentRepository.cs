using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PaymentService.Repositories;

public sealed class SqlPaymentRepository
{
    private readonly string _connectionString;

    public SqlPaymentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> GetBalanceAsync(string policyNumber)
    {
        await using var conn = new SqlConnection(_connectionString);
        var account = await conn.QueryFirstOrDefaultAsync<AccountRow>(
            """
            select PolicyNumber, AccountNumber
            from PolicyAccount
            where PolicyNumber = @PolicyNumber
            """,
            new { PolicyNumber = policyNumber });
        if (account == null)
        {
            throw new InvalidOperationException(
                $"Policy Account not found. Looking for account with number: {policyNumber}");
        }

        var entries = await conn.QueryAsync<EntryRow>(
            """
            select Kind, Amount, EffectiveDate
            from AccountEntry
            where PolicyNumber = @PolicyNumber and EffectiveDate <= @Now
            order by CreationDate, Id
            """,
            new { PolicyNumber = policyNumber, Now = DateTime.Now });
        var balance = 0m;
        foreach (var entry in entries)
        {
            balance = entry.Kind switch
            {
                "In" => balance + entry.Amount,
                _ => balance - entry.Amount
            };
        }

        return string.Join('|', account.PolicyNumber, account.AccountNumber, Money(balance));
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class AccountRow
    {
        public string PolicyNumber { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
    }

    private sealed class EntryRow
    {
        public string Kind { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
