using Microsoft.EntityFrameworkCore;
using Npgsql;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Articles;

public interface ISavedStoriesService
{
    Task<bool> SaveAsync(UserProfile userProfile, int articleId);
    Task UnsaveAsync(UserProfile userProfile, int articleId);
}

public class SavedStoriesService : ISavedStoriesService
{
    private readonly TrumanDbContext _dbContext;

    public SavedStoriesService(TrumanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> SaveAsync(UserProfile userProfile, int articleId)
    {
        if (!await _dbContext.Articles.AnyAsync(a => a.Id == articleId))
            return false;

        if (await IsSavedAsync(userProfile, articleId))
            return true;

        _dbContext.SavedStories.Add(new SavedStory { UserProfileId = userProfile.Id, ArticleId = articleId });
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Saved by a concurrent request from another device.
        }
        return true;
    }

    public Task UnsaveAsync(UserProfile userProfile, int articleId) =>
        _dbContext.SavedStories
            .Where(s => s.UserProfileId == userProfile.Id && s.ArticleId == articleId)
            .ExecuteDeleteAsync();

    private Task<bool> IsSavedAsync(UserProfile userProfile, int articleId) =>
        _dbContext.SavedStories.AnyAsync(s => s.UserProfileId == userProfile.Id && s.ArticleId == articleId);
}
