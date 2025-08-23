using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Truman.Api.Features.Articles;
using System.Security.Claims;

namespace Truman.Api.Features.Articles;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/articles/relevant", async (
            RelevantArticlesRequest request,
            IRelevantArticlesService relevantArticlesService,
            HttpContext http) =>
        {
            try
            {
                var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
                var articles = await relevantArticlesService.GetRelevantArticlesAsync(request, email);
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