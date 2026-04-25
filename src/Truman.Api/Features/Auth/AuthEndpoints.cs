using Truman.Api.Features.Email;
namespace Truman.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/{provider}/login", (string provider, HttpContext context) =>
        {
            var scheme = provider.ToLowerInvariant() switch
            {
                "facebook" => "Facebook",
                "google" => "Google",
                _ => throw new ArgumentException("Invalid provider", nameof(provider))
            };

            var returnUrl = context.Request.GetValidatedReturnUrl(context.Request.Query["returnUrl"].ToString());

            var properties = new AuthenticationProperties
            {
                Items =
                {
                    { "returnUrl", returnUrl }
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
                var code = await magicLinkService.GenerateMagicLinkAsync(email);
                var baseUrl = context.Request.GetBaseUrl();
                var magicLinkUrl = $"{baseUrl}/login/verify?code={code}";

                await emailService.SendMagicLinkEmailAsync(email, magicLinkUrl);

                return Results.Ok(new { message = "Magic link sent successfully" });
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
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

            var token = tokenService.GenerateToken(record.Email);

            return Results.Text(token);
        });
    }
}
