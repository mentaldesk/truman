using Truman.Api.Features.Email;
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

        app.MapGet("/auth/start/magic", async (
            string email,
            IMagicLinkService magicLinkService,
            IEmailService emailService,
            HttpContext context) =>
        {
            try
            {
                // Generate a new magic link code
                var code = await magicLinkService.GenerateMagicLinkAsync(email);
                
                // Create the magic link URL that points to the frontend verification page
                var magicLinkUrl = $"http://localhost:5174/login/verify?code={code}";
                
                // Send the magic link email
                await emailService.SendMagicLinkEmailAsync(email, magicLinkUrl);
                
                return Results.Ok(new { message = "Magic link sent successfully" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Failed to send magic link" });
            }
        });

        app.MapGet("/auth/validate/magic", async (
            string code,
            IMagicLinkService magicLinkService,
            ITokenService tokenService,
            HttpContext context) =>
        {
            var record = await magicLinkService.ValidateMagicLinkAsync(code);
            
            if (record == null)
            {
                return Results.BadRequest(new { error = "Invalid or expired magic link" });
            }
            
            // Generate JWT token
            var token = tokenService.GenerateToken(record.Email);
            
            return Results.Text(token);
        });
    }
} 