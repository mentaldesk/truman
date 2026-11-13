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

        app.MapGet("/api/articles/saved", async (
            string? presenter,
            IRelevantArticlesService relevantArticlesService,
            IProfileService profileService,
            HttpContext http) =>
        {
            var userProfile = await CurrentUserAsync(profileService, http);
            if (userProfile == null)
                return Results.Unauthorized();

            var request = new RelevantArticlesRequest { Presenter = presenter ?? string.Empty };
            return Results.Ok(await relevantArticlesService.GetSavedArticlesAsync(request, userProfile));
        }).RequireAuthorization();

        app.MapPut("/api/articles/{id:int}/saved", async (
            int id,
            ISavedStoriesService savedStoriesService,
            IProfileService profileService,
            HttpContext http) =>
        {
            var userProfile = await CurrentUserAsync(profileService, http);
            if (userProfile == null)
                return Results.Unauthorized();

            return await savedStoriesService.SaveAsync(userProfile, id)
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization();

        app.MapDelete("/api/articles/{id:int}/saved", async (
            int id,
            ISavedStoriesService savedStoriesService,
            IProfileService profileService,
            HttpContext http) =>
        {
            var userProfile = await CurrentUserAsync(profileService, http);
            if (userProfile == null)
                return Results.Unauthorized();

            await savedStoriesService.UnsaveAsync(userProfile, id);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static async Task<Truman.Data.Entities.UserProfile?> CurrentUserAsync(IProfileService profileService, HttpContext http)
    {
        var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
        return string.IsNullOrEmpty(email) ? null : await profileService.GetUserProfileAsync(email);
    }
} 