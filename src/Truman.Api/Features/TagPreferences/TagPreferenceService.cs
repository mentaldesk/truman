using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.TagPreferences;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.TagPreferences;

public class TagPreferenceService : ITagPreferenceService
{
    private readonly TrumanDbContext _dbContext;

    public TagPreferenceService(TrumanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TagPreferenceResponse> SetTagPreferenceAsync(string userEmail, string tag, int weight)
    {
        // Get or create user profile
        var userProfile = await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);

        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile not found for email: {userEmail}");
        }

        // Check if tag preference already exists
        var existingPreference = userProfile.TagPreferences
            .FirstOrDefault(tp => tp.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

        if (existingPreference != null)
        {
            // Update existing preference
            existingPreference.Weight = weight;
        }
        else
        {
            // For new favorites, default to weight 1 if not specified
            var finalWeight = weight > 0 && weight == 1 ? 1 : weight;
            
            // Create new preference
            var newPreference = new UserTagPreference
            {
                UserProfileId = userProfile.Id,
                Tag = tag,
                Weight = finalWeight
            };
            _dbContext.UserTagPreferences.Add(newPreference);
        }

        await _dbContext.SaveChangesAsync();

        return new TagPreferenceResponse
        {
            Tag = tag,
            Weight = weight
        };
    }

    public async Task<bool> RemoveTagPreferenceAsync(string userEmail, string tag)
    {
        var userProfile = await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);

        if (userProfile == null)
        {
            return false;
        }

        var preferenceToRemove = userProfile.TagPreferences
            .FirstOrDefault(tp => tp.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

        if (preferenceToRemove == null)
        {
            return false;
        }

        _dbContext.UserTagPreferences.Remove(preferenceToRemove);
        await _dbContext.SaveChangesAsync();
        
        return true;
    }

    public async Task<List<TagPreferenceResponse>> GetUserTagPreferencesAsync(string userEmail)
    {
        var userProfile = await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);

        if (userProfile == null)
        {
            return new List<TagPreferenceResponse>();
        }

        return userProfile.TagPreferences
            .Select(tp => new TagPreferenceResponse
            {
                Tag = tp.Tag,
                Weight = tp.Weight
            })
            .ToList();
    }

    public async Task<TagPreferenceResponse> BumpTagPriorityAsync(string userEmail, string tag)
    {
        var userProfile = await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);

        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile not found for email: {userEmail}");
        }

        var preference = userProfile.TagPreferences
            .FirstOrDefault(tp => tp.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

        if (preference == null)
        {
            throw new InvalidOperationException($"Tag preference '{tag}' not found for user");
        }

        if (preference.Weight == 0)
        {
            throw new InvalidOperationException($"Cannot bump priority of banned tag '{tag}'");
        }

        // Bump the tag to the next priority group
        preference.Weight++;
        await _dbContext.SaveChangesAsync();

        return new TagPreferenceResponse
        {
            Tag = preference.Tag,
            Weight = preference.Weight
        };
    }

    public async Task<Dictionary<int, List<string>>> GetTagsByWeightGroupAsync(string userEmail)
    {
        var userProfile = await _dbContext.UserProfiles
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Email == userEmail);

        if (userProfile == null)
        {
            return new Dictionary<int, List<string>>();
        }

        return userProfile.TagPreferences
            .Where(tp => tp.Weight > 0) // Only include favorites, not banned tags
            .GroupBy(tp => tp.Weight)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Select(tp => tp.Tag).ToList());
    }
}
