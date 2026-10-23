using Insurance.Graftcode;
using AuthService.Repositories;

namespace AuthService.Services;

public static class AgentAuthService
{
    // Graftcode-facing method replacing POST /api/User.
    public static string Authenticate(string login, string password) =>
        GraftAsync.Run(() => Repository().AuthenticateAsync(login, password));

    // Graftcode-facing method replacing GET /api/User.
    public static string GetAgentByLogin(string login) =>
        GraftAsync.Run(() => Repository().GetByLoginAsync(login));

    private static SqlAgentRepository Repository() =>
        new(Required("AUTH_CN"));

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
