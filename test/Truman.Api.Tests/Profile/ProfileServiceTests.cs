using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Profile;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Profile;

[Collection("Database collection")] // Uses DatabaseFixture (non-parallel to avoid migration races)
public class ProfileServiceTests
{
    private readonly TrumanDbContext _context;
    private readonly ProfileService _service;

    public ProfileServiceTests(DatabaseFixture fixture)
    {
        _context = new TrumanDbContext(fixture.DbOptions);
        _service = new ProfileService(_context);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenEmailIsNull()
    {
        var result = await _service.GetUserProfileAsync(null!);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenEmailIsEmpty()
    {
        var result = await _service.GetUserProfileAsync("");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenUserNotFound()
    {
        var result = await _service.GetUserProfileAsync("missing@example.com");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsProfile_WithTagPreferences()
    {
        var email = "user1@example.com";

        // Seed data if not already
        if (!await _context.UserProfiles.AnyAsync(u => u.Email == email))
        {
            var user = new UserProfile { Email = email, Mood = 7 };
            _context.UserProfiles.Add(user);
            await _context.SaveChangesAsync();

            _context.UserTagPreferences.AddRange(
                new UserTagPreference { UserProfileId = user.Id, Tag = "ai", Weight = 3 },
                new UserTagPreference { UserProfileId = user.Id, Tag = "cloud", Weight = 1 }
            );
            await _context.SaveChangesAsync();
        }

        var profile = await _service.GetUserProfileAsync(email);
        Assert.NotNull(profile);
        Assert.Equal(email, profile!.Email);
        Assert.NotNull(profile.TagPreferences);
        Assert.True(profile.TagPreferences.Count >= 2); // At least two we inserted
    }
}
