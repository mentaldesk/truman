namespace Truman.Api.Features.Auth;

public static class AdminEmails
{
    public static bool IsAdmin(IConfiguration configuration, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var raw = configuration["ADMIN_EMAILS"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => string.Equals(entry, email, StringComparison.OrdinalIgnoreCase));
    }
}
