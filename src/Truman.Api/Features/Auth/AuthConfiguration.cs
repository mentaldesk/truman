namespace Truman.Api.Features.Auth;

public class AuthConfiguration
{
    public JwtSettings Jwt { get; set; } = new();
    public FacebookSettings Facebook { get; set; } = new();
    public GoogleSettings Google { get; set; } = new();
}

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}

public class FacebookSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}

public class GoogleSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
} 