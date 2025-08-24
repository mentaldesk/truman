using Microsoft.Extensions.Configuration;

namespace Truman.Data;

public static class WebApplicationBuilderExtensions
{
    public static string GetPostgresConnectionString(this IConfiguration configuration)
    {
        var postgresHost = configuration["POSTGRES_HOST"] ?? "localhost";
        var postgresUser = configuration["POSTGRES_USER"];
        var postgresPassword = configuration["POSTGRES_PASSWORD"];
        var postgresDb = configuration["POSTGRES_DB"];

        return $"Host={postgresHost};Database={postgresDb};Username={postgresUser};Password={postgresPassword}";
    }
} 