namespace Truman.Api.Features.Auth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public interface ITokenService
{
    string GenerateToken(string email);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(IEnumerable<Claim> claims)
    {
        var jwtSettings = _configuration.GetSection("Authentication:Jwt").Get<JwtSettings>();
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings?.Key ?? 
                                   throw new InvalidOperationException("JWT Key not configured")));
        
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7), // Token expires in 7 days
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateToken(string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), // Generate a unique ID
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email), // Use email as the name until we have a proper name
            new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod", "magic_link")
        };

        return GenerateToken(claims);
    }
} 