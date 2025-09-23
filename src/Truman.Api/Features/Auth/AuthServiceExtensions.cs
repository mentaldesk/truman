using Truman.Api.Features.Email;

namespace Truman.Api.Features.Auth;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthConfiguration>(configuration.GetSection("Authentication"));
        services.AddScoped<TokenService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IMagicLinkService, MagicLinkService>();
        
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
                var jwtSettings = configuration.GetSection("Authentication:Jwt").Get<JwtSettings>();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings?.Key ?? throw new InvalidOperationException("JWT Key is not configured"))
                    )
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
            var fbSettings = configuration.GetSection("Authentication:Facebook").Get<FacebookSettings>();
            options.AppId = fbSettings?.AppId ?? throw new InvalidOperationException("Facebook AppId is not configured");
            options.AppSecret = fbSettings?.AppSecret ?? throw new InvalidOperationException("Facebook AppSecret is not configured");
            options.SignInScheme = "Cookies";
            options.SaveTokens = true;
            options.CallbackPath = "/api/signin-facebook";
            
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
            var googleSettings = configuration.GetSection("Authentication:Google").Get<GoogleSettings>();
            options.ClientId = googleSettings?.ClientId ?? throw new InvalidOperationException("Google ClientId is not configured");
            options.ClientSecret = googleSettings?.ClientSecret ?? throw new InvalidOperationException("Google ClientSecret is not configured");
            options.SignInScheme = "Cookies";
            options.SaveTokens = true;
            options.CallbackPath = "/api/signin-google";
            
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
        // Get the frontend configuration
        var frontendConfig = context.HttpContext.RequestServices.GetRequiredService<IOptions<FrontendConfiguration>>().Value;
        
        // Get the return URL from the auth properties
        var returnUrl = context.Properties?.Items.TryGetValue("returnUrl", out var url) == true
            ? url
            : frontendConfig.BaseUrl;

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
