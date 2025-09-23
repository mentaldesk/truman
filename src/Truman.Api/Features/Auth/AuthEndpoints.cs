using Truman.Api.Features.Email;
namespace Truman.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/{provider}/login", (string provider, HttpContext context) =>
        {
            var scheme = provider.ToLowerInvariant() switch
            {
                "facebook" => "Facebook",
                "google" => "Google",
                _ => throw new ArgumentException("Invalid provider", nameof(provider))
            };
            
            // Get the frontend configuration
            var frontendConfig = context.RequestServices.GetRequiredService<IOptions<FrontendConfiguration>>().Value;
            
            // Store the frontend return URL for after authentication
            var returnUrl = context.Request.Query["returnUrl"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || 
                (!returnUrl.StartsWith(frontendConfig.BaseUrl)))
            {
                returnUrl = frontendConfig.BaseUrl;
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

        app.MapGet("/api/auth/start/magic", async (
            string email,
            IMagicLinkService magicLinkService,
            IEmailService emailService,
            HttpContext context) =>
        {
            try
            {
                // Generate a new magic link code
                var code = await magicLinkService.GenerateMagicLinkAsync(email);
                
                // Get the frontend configuration
                var frontendConfig = context.RequestServices.GetRequiredService<IOptions<FrontendConfiguration>>().Value;
                
                // Create the magic link URL that points to the frontend verification page
                var magicLinkUrl = $"{frontendConfig.BaseUrl}/login/verify?code={code}";
                
                // Send the magic link email
                await emailService.SendMagicLinkEmailAsync(email, magicLinkUrl);
                
                return Results.Ok(new { message = "Magic link sent successfully" });
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return Results.BadRequest(new { error = "Failed to send magic link" });
            }
        });

        app.MapGet("/api/auth/validate/magic", async (
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
