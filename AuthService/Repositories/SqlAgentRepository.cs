using Dapper;
using Microsoft.Data.SqlClient;

namespace AuthService.Repositories;

public sealed class SqlAgentRepository
{
    private readonly string _connectionString;

    public SqlAgentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> AuthenticateAsync(string login, string password)
    {
        var agent = await FindAsync(login);
        if (agent == null || agent.Password != (password ?? string.Empty))
        {
            throw new InvalidOperationException("Username or password incorrect");
        }

        return string.Join('|', agent.Login, "SALESMAN;USER", agent.Avatar, agent.Products);
    }

    public async Task<string> GetByLoginAsync(string login)
    {
        var agent = await FindAsync(login);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with login '{login}' was not found");
        }

        return string.Join('|', agent.Login, agent.Avatar, agent.Products);
    }

    private async Task<AgentRow?> FindAsync(string login)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<AgentRow>(
            """
            select Login, Password, isnull(Avatar, N'') as Avatar, Products
            from Agent
            where Login = @Login
            """,
            new { Login = login });
    }

    private sealed class AgentRow
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Products { get; set; } = string.Empty;
    }
}
