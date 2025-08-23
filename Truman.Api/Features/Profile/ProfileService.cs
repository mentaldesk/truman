using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Profile;

public interface IProfileService
{
    Task<UserProfile?> GetUserProfileAsync(string userEmail);
}

public class ProfileService : IProfileService
{
    private readonly TrumanDbContext _dbContext;

    public ProfileService(TrumanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfile?> GetUserProfileAsync(string userEmail)
    {
        if (string.IsNullOrEmpty(userEmail))
        {
            return null;
        }

        return await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);
    }
}
