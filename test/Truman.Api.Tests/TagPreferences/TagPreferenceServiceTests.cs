using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.TagPreferences;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.TagPreferences;

[Collection("Database collection")] // Share container / DB, disable parallel to simplify
public class TagPreferenceServiceTests
{
    private readonly TrumanDbContext _context;
    private readonly TagPreferenceService _service;

    public TagPreferenceServiceTests(DatabaseFixture fixture)
    {
        _context = new TrumanDbContext(fixture.DbOptions);
        _service = new TagPreferenceService(_context);
    }

    private async Task<UserProfile> CreateUserAsync(string email)
    {
        var existing = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
        if (existing != null) return existing;
        var user = new UserProfile { Email = email };
        _context.UserProfiles.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task SetTagPreferenceAsync_Throws_WhenUserMissing()
    {
        var email = $"missing_{Guid.NewGuid()}@example.com";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SetTagPreferenceAsync(email, "ai", 2));
    }

    [Fact]
    public async Task SetTagPreferenceAsync_CreatesNewPreference()
    {
        var email = $"user_newpref_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        var resp = await _service.SetTagPreferenceAsync(email, "ai", 2);

        Assert.Equal("ai", resp.Tag);
        Assert.Equal(2, resp.Weight);

        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "ai");
        Assert.Equal(2, stored.Weight);
    }

    [Fact]
    public async Task SetTagPreferenceAsync_CreatesNewPreference_DefaultWeight1()
    {
        var email = $"user_weight1_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        var resp = await _service.SetTagPreferenceAsync(email, "cloud", 1);

        Assert.Equal(1, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "cloud");
        Assert.Equal(1, stored.Weight);
    }

    [Fact]
    public async Task SetTagPreferenceAsync_UpdatesExistingPreference()
    {
        var email = $"user_update_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 1 });
        await _context.SaveChangesAsync();

        var resp = await _service.SetTagPreferenceAsync(email, "ai", 5);

        Assert.Equal(5, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "ai");
        Assert.Equal(5, stored.Weight);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_ReturnsFalse_WhenUserMissing()
    {
        var email = $"nouser_{Guid.NewGuid()}@example.com";
        var result = await _service.RemoveTagPreferenceAsync(email, "ai");
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_ReturnsFalse_WhenTagMissing()
    {
        var email = $"user_notag_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);
        var result = await _service.RemoveTagPreferenceAsync(email, "ai");
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_RemovesTag()
    {
        var email = $"user_remove_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 2 });
        await _context.SaveChangesAsync();

        var result = await _service.RemoveTagPreferenceAsync(email, "ai");
        Assert.True(result);
        Assert.False(await _context.UserTagPreferences.AnyAsync(p => p.UserProfileId == user.Id && p.Tag == "ai"));
    }

    [Fact]
    public async Task GetUserTagPreferencesAsync_ReturnsEmpty_WhenUserMissing()
    {
        var email = $"missing_{Guid.NewGuid()}@example.com";
        var list = await _service.GetUserTagPreferencesAsync(email);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetUserTagPreferencesAsync_ReturnsPreferences()
    {
        var email = $"user_list_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.AddRange(
            new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "cloud", Weight = 1 }
        );
        await _context.SaveChangesAsync();

        var list = await _service.GetUserTagPreferencesAsync(email);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.Tag == "ai" && p.Weight == 3);
        Assert.Contains(list, p => p.Tag == "cloud" && p.Weight == 1);
    }

    [Fact]
    public async Task BumpTagPriorityAsync_Throws_WhenUserMissing()
    {
        var email = $"nouser_{Guid.NewGuid()}@example.com";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BumpTagPriorityAsync(email, "ai"));
    }

    [Fact]
    public async Task BumpTagPriorityAsync_Throws_WhenTagMissing()
    {
        var email = $"user_notag_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BumpTagPriorityAsync(email, "ai"));
    }

    [Fact]
    public async Task BumpTagPriorityAsync_Throws_WhenTagIsBanned()
    {
        var email = $"user_banned_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 0 });
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BumpTagPriorityAsync(email, "ai"));
        Assert.Contains("banned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BumpTagPriorityAsync_IncrementsWeight()
    {
        var email = $"user_bump_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 2 });
        await _context.SaveChangesAsync();

        var resp = await _service.BumpTagPriorityAsync(email, "ai");
        Assert.Equal(3, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfileId == user.Id && p.Tag == "ai");
        Assert.Equal(3, stored.Weight);
    }

    [Fact]
    public async Task GetTagsByWeightGroupAsync_ReturnsEmpty_WhenUserMissing()
    {
        var email = $"nouser_{Guid.NewGuid()}@example.com";
        var dict = await _service.GetTagsByWeightGroupAsync(email);
        Assert.Empty(dict);
    }

    [Fact]
    public async Task GetTagsByWeightGroupAsync_GroupsAndExcludesBanned()
    {
        var email = $"user_groups_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.AddRange(
            new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "cloud", Weight = 1 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "security", Weight = 2 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "bannedtag", Weight = 0 }
        );
        await _context.SaveChangesAsync();

        var dict = await _service.GetTagsByWeightGroupAsync(email);
        Assert.Equal(3, dict.Count); // weights 1,2,3
        Assert.DoesNotContain(0, dict.Keys);
        Assert.Equal(["cloud"], dict[1].OrderBy(x => x));
        Assert.Equal(["security"], dict[2].OrderBy(x => x));
        Assert.Equal(["ai"], dict[3].OrderBy(x => x));
    }
}

