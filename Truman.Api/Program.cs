using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Truman.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication.OAuth;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(options =>
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ??
                                                                               throw new InvalidOperationException(
                                                                                   "JWT Key not found")))
        };
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ??
                        throw new InvalidOperationException("Facebook AppId not found");
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ??
                            throw new InvalidOperationException("Facebook AppSecret not found");
        options.SignInScheme = "Cookies";
        options.SaveTokens = true;
        
        // Use the default callback path that Facebook expects
        options.CallbackPath = "/signin-facebook";
        
        options.Events = new OAuthEvents
        {
            OnTicketReceived = async context =>
            {
                // Get the return URL from the auth properties
                var returnUrl = context.Properties != null && context.Properties.Items.TryGetValue("returnUrl", out var url) ? url : "http://localhost:5174";
                
                // Generate JWT token
                var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
                if (context.Principal != null)
                {
                    var claims = context.Principal.Claims.ToList();
                    // Add the authentication method claim
                    claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod", "facebook"));
                    var token = tokenService.GenerateToken(claims);
                
                    // Sign-out of cookie auth
                    await context.HttpContext.SignOutAsync("Cookies");
                
                    // Redirect to frontend with token
                    context.HandleResponse();
                    context.Response.Redirect($"{returnUrl}?token={token}");
                }
            }
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? 
            throw new InvalidOperationException("Google ClientId not found");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? 
            throw new InvalidOperationException("Google ClientSecret not found");
        options.SignInScheme = "Cookies";
        options.SaveTokens = true;
        
        // Use the default callback path that Google expects
        options.CallbackPath = "/signin-google";
        
        options.Events = new OAuthEvents
        {
            OnTicketReceived = async context =>
            {
                // Get the return URL from the auth properties
                var returnUrl = context.Properties != null && context.Properties.Items.TryGetValue("returnUrl", out var url) ? url : "http://localhost:5174";
                
                // Generate JWT token
                var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
                if (context.Principal != null)
                {
                    var claims = context.Principal.Claims.ToList();
                    // Add the authentication method claim
                    claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod", "google"));
                    var token = tokenService.GenerateToken(claims);
                
                    // Sign-out of cookie auth
                    await context.HttpContext.SignOutAsync("Cookies");
                
                    // Redirect to frontend with token
                    context.HandleResponse();
                    context.Response.Redirect($"{returnUrl}?token={token}");
                }
            }
        };
    });
// .AddMicrosoftAccount(options =>
// {
//     options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? throw new InvalidOperationException("Microsoft ClientId not found");
//     options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? throw new InvalidOperationException("Microsoft ClientSecret not found");
// });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Auth endpoints
app.MapGet("/auth/{provider}/login", async (string provider, HttpContext context) =>
{
    var scheme = provider.ToLowerInvariant() switch
    {
        "facebook" => "Facebook",
        "google" => "Google",
        "microsoft" => "Microsoft",
        _ => throw new ArgumentException("Invalid provider", nameof(provider))
    };
    
    // Store the frontend return URL for after authentication
    var returnUrl = context.Request.Query["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl) || 
        (!returnUrl.StartsWith("http://localhost:5174") && !returnUrl.StartsWith("http://localhost:3000")))
    {
        returnUrl = "http://localhost:5174";
    }
    
    var properties = new AuthenticationProperties
    {
        Items =
        {
            { "returnUrl", returnUrl } // This is where we'll redirect after processing the callback
        }
    };
    
    return Results.Challenge(properties, [scheme]);
});

var target = Environment.GetEnvironmentVariable("TARGET") ?? "World";
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";

app.MapGet("/", () => $"Hello {target}!");

app.Run(url);