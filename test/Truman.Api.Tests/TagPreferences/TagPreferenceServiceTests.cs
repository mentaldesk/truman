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
    private static readonly CancellationToken Ct = CancellationToken.None; // reuse token

    public TagPreferenceServiceTests(DatabaseFixture fixture)
    {
        _context = new TrumanDbContext(fixture.DbOptions);
        _service = new TagPreferenceService(_context);
    }

    private async Task<UserProfile> CreateUserAsync(string email)
    {
        var existing = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email, Ct);
        if (existing != null) return existing;
        var user = new UserProfile { Email = email };
        _context.UserProfiles.Add(user);
        await _context.SaveChangesAsync(Ct);
        return user;
    }

    [Fact]
    public async Task SetTagPreferenceAsync_Throws_WhenUserMissing()
    {
        // Arrange
        var email = $"missing_{Guid.NewGuid()}@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SetTagPreferenceAsync(email, "ai", 2));
    }

    [Fact]
    public async Task SetTagPreferenceAsync_CreatesNewPreference()
    {
        // Arrange
        var email = $"user_newpref_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        // Act
        var resp = await _service.SetTagPreferenceAsync(email, "ai", 2);

        // Assert
        Assert.Equal("ai", resp.Tag);
        Assert.Equal(2, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "ai", Ct);
        Assert.Equal(2, stored.Weight);
    }

    [Fact]
    public async Task SetTagPreferenceAsync_CreatesNewPreference_DefaultWeight1()
    {
        // Arrange
        var email = $"user_weight1_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        // Act
        var resp = await _service.SetTagPreferenceAsync(email, "cloud", 1);

        // Assert
        Assert.Equal(1, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "cloud", Ct);
        Assert.Equal(1, stored.Weight);
    }

    [Fact]
    public async Task SetTagPreferenceAsync_UpdatesExistingPreference()
    {
        // Arrange
        var email = $"user_update_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 1 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var resp = await _service.SetTagPreferenceAsync(email, "ai", 5);

        // Assert
        Assert.Equal(5, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfile.Email == email && p.Tag == "ai", Ct);
        Assert.Equal(5, stored.Weight);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_ReturnsFalse_WhenUserMissing()
    {
        // Arrange
        var email = $"nouser_{Guid.NewGuid()}@example.com";

        // Act
        var result = await _service.RemoveTagPreferenceAsync(email, "ai");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_ReturnsFalse_WhenTagMissing()
    {
        // Arrange
        var email = $"user_notag_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        // Act
        var result = await _service.RemoveTagPreferenceAsync(email, "ai");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagPreferenceAsync_RemovesTag()
    {
        // Arrange
        var email = $"user_remove_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 2 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var result = await _service.RemoveTagPreferenceAsync(email, "ai");

        // Assert
        Assert.True(result);
        Assert.False(await _context.UserTagPreferences.AnyAsync(p => p.UserProfileId == user.Id && p.Tag == "ai", Ct));
    }

    [Fact]
    public async Task GetUserTagPreferencesAsync_ReturnsEmpty_WhenUserMissing()
    {
        // Arrange
        var email = $"missing_{Guid.NewGuid()}@example.com";

        // Act
        var list = await _service.GetUserTagPreferencesAsync(email);

        // Assert
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetUserTagPreferencesAsync_ReturnsPreferences()
    {
        // Arrange
        var email = $"user_list_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.AddRange(
            new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "cloud", Weight = 1 }
        );
        await _context.SaveChangesAsync(Ct);

        // Act
        var list = await _service.GetUserTagPreferencesAsync(email);

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.Tag == "ai" && p.Weight == 3);
        Assert.Contains(list, p => p.Tag == "cloud" && p.Weight == 1);
    }

    [Fact]
    public async Task PromoteTagAsync_Throws_WhenUserMissing()
    {
        // Arrange
        var email = $"nouser_{Guid.NewGuid()}@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PromoteTagAsync(email, "ai"));
    }

    [Fact]
    public async Task PromoteTagAsync_Throws_WhenTagMissing()
    {
        // Arrange
        var email = $"user_notag_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PromoteTagAsync(email, "ai"));
    }

    [Fact]
    public async Task PromoteTagAsync_Throws_WhenTagIsBanned()
    {
        // Arrange
        var email = $"user_banned_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 0 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PromoteTagAsync(email, "ai"));

        // Assert
        Assert.Contains("banned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromoteTagAsync_IncrementsWeight()
    {
        // Arrange
        var email = $"user_promote_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        // Non-contiguous groups: 1, 2, 4. Promoting weight 2 should jump to 4 (next existing group)
        const string tagToPromote = "pickMe";
        _context.UserTagPreferences.AddRange(
            new UserTagPreference { UserProfileId = user.Id, Tag = "low1", Weight = 1 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "low2", Weight = 1 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "mid1", Weight = 2 },
            new UserTagPreference { UserProfileId = user.Id, Tag = tagToPromote, Weight = 2 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "mid2", Weight = 2 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "high1", Weight = 4 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "high2", Weight = 4 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "high3", Weight = 5 }
        );
        await _context.SaveChangesAsync(Ct);

        // Act
        var resp = await _service.PromoteTagAsync(email, tagToPromote);

        // Assert
        Assert.Equal(4, resp.Weight); // Should jump to next existing weight group (4)
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfileId == user.Id && p.Tag == tagToPromote, Ct);
        Assert.Equal(4, stored.Weight);
    }

    [Fact]
    public async Task DemoteTagAsync_Throws_WhenUserMissing()
    {
        // Arrange
        var email = $"nouser_{Guid.NewGuid()}@example.com";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DemoteTagAsync(email, "ai"));
    }

    [Fact]
    public async Task DemoteTagAsync_Throws_WhenTagMissing()
    {
        // Arrange
        var email = $"user_notag_{Guid.NewGuid()}@example.com";
        await CreateUserAsync(email);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DemoteTagAsync(email, "ai"));
    }

    [Fact]
    public async Task DemoteTagAsync_Throws_WhenTagIsBanned()
    {
        // Arrange
        var email = $"user_banned_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 0 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DemoteTagAsync(email, "ai"));

        // Assert
        Assert.Contains("banned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DemoteTagAsync_Throws_WhenTagWeightIs1()
    {
        // Arrange
        var email = $"user_weight1_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 1 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DemoteTagAsync(email, "ai"));

        // Assert
        Assert.Contains("below weight 1", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DemoteTagAsync_DecrementsWeight()
    {
        // Arrange
        var email = $"user_demote_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.Add(new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 });
        await _context.SaveChangesAsync(Ct);

        // Act
        var resp = await _service.DemoteTagAsync(email, "ai");

        // Assert
        Assert.Equal(2, resp.Weight);
        var stored = await _context.UserTagPreferences.SingleAsync(p => p.UserProfileId == user.Id && p.Tag == "ai", Ct);
        Assert.Equal(2, stored.Weight);
    }

    [Fact]
    public async Task GetTagsByWeightGroupAsync_ReturnsEmpty_WhenUserMissing()
    {
        // Arrange
        var email = $"nouser_{Guid.NewGuid()}@example.com";

        // Act
        var dict = await _service.GetTagsByWeightGroupAsync(email);

        // Assert
        Assert.Empty(dict);
    }

    [Fact]
    public async Task GetTagsByWeightGroupAsync_GroupsAndExcludesBanned()
    {
        // Arrange
        var email = $"user_groups_{Guid.NewGuid()}@example.com";
        var user = await CreateUserAsync(email);
        _context.UserTagPreferences.AddRange(
            new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "cloud", Weight = 1 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "security", Weight = 2 },
            new UserTagPreference { UserProfileId = user.Id, Tag = "bannedtag", Weight = 0 }
        );
        await _context.SaveChangesAsync(Ct);

        // Act
        var dict = await _service.GetTagsByWeightGroupAsync(email);

        // Assert
        Assert.Equal(3, dict.Count); // weights 1,2,3
        Assert.DoesNotContain(0, dict.Keys);
        Assert.Equal(["cloud"], dict[1].OrderBy(x => x));
        Assert.Equal(["security"], dict[2].OrderBy(x => x));
        Assert.Equal(["ai"], dict[3].OrderBy(x => x));
    }
}
