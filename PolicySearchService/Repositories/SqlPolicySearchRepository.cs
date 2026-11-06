using Dapper;
using Microsoft.Data.SqlClient;

namespace PolicySearchService.Repositories;

public sealed class SqlPolicySearchRepository
{
    private readonly string _connectionString;

    public SqlPolicySearchRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string[]> SearchAsync(string? queryText)
    {
        var escaped = (queryText ?? string.Empty)
            .Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<SearchRow>(
            """
            select top 10 PolicyNumber, PolicyHolder, ProductCode, Status
            from PolicySearch
            where PolicyNumber like @Query or PolicyHolder like @Query
            order by PolicyNumber
            """,
            new { Query = $"%{escaped}%" });
        return rows.Select(r => string.Join('|', r.PolicyNumber, r.PolicyHolder, r.ProductCode, r.Status)).ToArray();
    }

    private sealed class SearchRow
    {
        public string PolicyNumber { get; set; } = string.Empty;
        public string PolicyHolder { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
