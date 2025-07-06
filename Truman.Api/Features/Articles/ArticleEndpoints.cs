using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Truman.Api.Features.Articles;

namespace Truman.Api.Features.Articles;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/articles/relevant", async (
            RelevantArticlesRequest request,
            IRelevantArticlesService relevantArticlesService) =>
        {
            try
            {
                var articles = await relevantArticlesService.GetRelevantArticlesAsync(request);
                return Results.Ok(articles);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
} 