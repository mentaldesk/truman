using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Truman.Api.Features.Articles;
using Truman.Api.Features.Profile;
using System.Security.Claims;

namespace Truman.Api.Features.Articles;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/articles/relevant", async (
            RelevantArticlesRequest request,
            IRelevantArticlesService relevantArticlesService,
            IProfileService profileService,
            HttpContext http) =>
        {
            try
            {
                var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(email))
                    return Results.Unauthorized();
                
                var userProfile = await profileService.GetUserProfileAsync(email);
                if (userProfile == null)
                    return Results.NotFound();
                
                var articles = await relevantArticlesService.GetRelevantArticlesAsync(request, userProfile);
                return Results.Ok(articles);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
} 