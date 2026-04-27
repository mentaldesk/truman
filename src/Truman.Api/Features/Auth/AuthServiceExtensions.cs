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
            .AddConditionalSocialAuth(configuration);

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy =>
                policy.RequireAuthenticatedUser().RequireRole("admin"));
        });
        return services;
    }

    private static AuthenticationBuilder AddConditionalSocialAuth(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        var socialEnabled = configuration.GetValue("Authentication:Social:Enabled", defaultValue: true);
        if (!socialEnabled) return builder;

        return builder
            .AddFacebook(ConfigureFacebookOptions(configuration))
            .AddGoogle(ConfigureGoogleOptions(configuration));
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
            var googleSettings = configuration.GetSection("Authentication:Google").Get<GoogleSettings>();
            options.ClientId = googleSettings?.ClientId ?? throw new InvalidOperationException("Google ClientId is not configured");
            options.ClientSecret = googleSettings?.ClientSecret ?? throw new InvalidOperationException("Google ClientSecret is not configured");
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
        var returnUrl = context.Properties?.Items.TryGetValue("returnUrl", out var url) == true
            ? context.HttpContext.Request.GetValidatedReturnUrl(url)
            : context.HttpContext.Request.GetBaseUrl();

        var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
        if (context.Principal != null)
        {
            var claims = context.Principal.Claims.ToList();
            claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod", provider));
            var token = tokenService.GenerateToken(claims);

            await context.HttpContext.SignOutAsync("Cookies");

            context.HandleResponse();
            context.Response.Redirect($"{returnUrl}?token={token}");
        }
    }
}
