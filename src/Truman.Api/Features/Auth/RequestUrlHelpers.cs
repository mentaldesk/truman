namespace Truman.Api.Features.Auth;

public static class RequestUrlHelpers
{
    public static string GetBaseUrl(this HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host.Value}";
    }

    public static string GetValidatedReturnUrl(this HttpRequest request, string? returnUrl)
    {
        var baseUrl = request.GetBaseUrl();

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return baseUrl;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.GetLeftPart(UriPartial.Path) + absoluteUri.Query;
        }

        if (returnUrl.StartsWith('/'))
        {
            return $"{baseUrl}{returnUrl}";
        }

        return baseUrl;
    }
}
