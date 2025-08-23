using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

public interface IRelevantArticlesService
{
    Task<RelevantArticlesResponse> GetRelevantArticlesAsync(RelevantArticlesRequest request, UserProfile userProfile);
} 