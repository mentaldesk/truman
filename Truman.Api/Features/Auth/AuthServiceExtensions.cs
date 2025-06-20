using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Truman.Api.Features.Auth;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthConfiguration>(configuration.GetSection("Authentication"));
        services.AddScoped<TokenService>();
        
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = "Cookies";
            })
            .AddCookie("Cookies", options => 
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.Cookie.Name = "TrumanAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? 
                            throw new InvalidOperationException("JWT Key not found")))
                };
            })
            .AddFacebook(ConfigureFacebookOptions(configuration))
            .AddGoogle(ConfigureGoogleOptions(configuration));

        services.AddAuthorization();
        return services;
    }
    
    private static Action<FacebookOptions> ConfigureFacebookOptions(IConfiguration configuration)
    {
        return options =>
        {
            options.AppId = configuration["Authentication:Facebook:AppId"] ??
                throw new InvalidOperationException("Facebook AppId not found");
            options.AppSecret = configuration["Authentication:Facebook:AppSecret"] ??
                throw new InvalidOperationException("Facebook AppSecret not found");
            options.SignInScheme = "Cookies";
            options.SaveTokens = true;
            options.CallbackPath = "/signin-facebook";
            
            options.Events = new OAuthEvents
            {
                OnTicketReceived = async context =>
                {
                    await HandleOAuthTicket(context, "facebook");
                }
            };
        };
    }
    
    private static Action<GoogleOptions> ConfigureGoogleOptions(IConfiguration configuration)
    {
        return options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"] ??
                throw new InvalidOperationException("Google ClientId not found");
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"] ??
                throw new InvalidOperationException("Google ClientSecret not found");
            options.SignInScheme = "Cookies";
            options.SaveTokens = true;
            options.CallbackPath = "/signin-google";
            
            options.Events = new OAuthEvents
            {
                OnTicketReceived = async context =>
                {
                    await HandleOAuthTicket(context, "google");
                }
            };
        };
    }
    
    private static async Task HandleOAuthTicket(TicketReceivedContext context, string provider)
    {
        // Get the return URL from the auth properties
        var returnUrl = context.Properties?.Items.TryGetValue("returnUrl", out var url) == true
            ? url
            : "http://localhost:5174";

        // Generate JWT token
        var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
        if (context.Principal != null)
        {
            var claims = context.Principal.Claims.ToList();
            // Add the authentication method claim
            claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod", provider));
            var token = tokenService.GenerateToken(claims);

            // Sign-out of cookie auth
            await context.HttpContext.SignOutAsync("Cookies");

            // Redirect to frontend with token
            context.HandleResponse();
            context.Response.Redirect($"{returnUrl}?token={token}");
        }
    }
} 