using Insurance.Graftcode;
using PolicySearchService.Repositories;

namespace PolicySearchService.Services;

public static class PolicySearch
{
    // Graftcode-facing method replacing GET /api/PolicySearch.
    public static string[] SearchPolicies(string queryText) =>
        GraftAsync.Run(() => Repository().SearchAsync(queryText));

    private static SqlPolicySearchRepository Repository() =>
        new(Required("POLICY_SEARCH_CN"));

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
