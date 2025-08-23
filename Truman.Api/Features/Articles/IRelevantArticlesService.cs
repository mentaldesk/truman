namespace Truman.Api.Features.Articles;

public interface IRelevantArticlesService
{
    Task<RelevantArticlesResponse> GetRelevantArticlesAsync(RelevantArticlesRequest request, string? userEmail);
} 