using Microsoft.AspNetCore.Authentication;

namespace Truman.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
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
    }
} 