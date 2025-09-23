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
        
        // Base connection string
        var conn = $"Host={postgresHost};Database={postgresDb};Username={postgresUser};Password={postgresPassword}";
        
        // Optional SSL settings for managed providers like Supabase
        var sslMode = configuration["POSTGRES_SSL_MODE"]; // e.g., Require, VerifyFull
        if (!string.IsNullOrWhiteSpace(sslMode))
        {
            conn += $";Ssl Mode={sslMode}";
        }
        var trust = configuration["POSTGRES_TRUST_SERVER_CERT"];
        if (!string.IsNullOrWhiteSpace(trust))
        {
            conn += $";Trust Server Certificate={trust}"; // true/false
        }
        
        return conn;
    }
}
