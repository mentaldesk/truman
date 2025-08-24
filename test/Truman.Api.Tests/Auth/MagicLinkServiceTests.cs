using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Auth;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Tests.Auth;

[Collection("Database collection")] // Uses shared Postgres testcontainer
public class MagicLinkServiceTests
{
    private readonly TrumanDbContext _context;
    private readonly MagicLinkService _service;

    public MagicLinkServiceTests(DatabaseFixture fixture)
    {
        _context = new TrumanDbContext(fixture.DbOptions);
        _service = new MagicLinkService(_context);
    }

    [Fact]
    public async Task GenerateMagicLinkAsync_PersistsRecord_AndReturnsSameCode()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        var before = DateTime.UtcNow;
        var code = await _service.GenerateMagicLinkAsync(email);
        var after = DateTime.UtcNow;

        var record = await _context.MagicLinks.SingleAsync(m => m.Code == code);
        Assert.Equal(email, record.Email);
        Assert.True(record.ExpiresAt > before.AddMinutes(4.5)); // about 5 minutes
        Assert.True(record.ExpiresAt <= after.AddMinutes(5.5));
    }

    [Fact]
    public async Task GenerateMagicLinkAsync_CodeIsUrlSafe()
    {
        var code = await _service.GenerateMagicLinkAsync($"user_{Guid.NewGuid()}@example.com");
        // Ensure it contains only URL safe base64 variant chars (A-Z a-z 0-9 _ -)
        Assert.Matches("^[A-Za-z0-9_-]+$", code);
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.ValidateMagicLinkAsync("nonexistent_code");
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_ReturnsRecord_AndDeletesLink_WhenValid()
    {
        var email = $"valid_{Guid.NewGuid()}@example.com";
        var code = await _service.GenerateMagicLinkAsync(email);

        var result = await _service.ValidateMagicLinkAsync(code);
        Assert.NotNull(result);
        Assert.Equal(code, result!.Code);
        Assert.Equal(email, result.Email);

        // Should be deleted from DB
        var stillExists = await _context.MagicLinks.AnyAsync(m => m.Code == code);
        Assert.False(stillExists);
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_ReturnsNull_AfterAlreadyUsed()
    {
        var email = $"reuse_{Guid.NewGuid()}@example.com";
        var code = await _service.GenerateMagicLinkAsync(email);
        var first = await _service.ValidateMagicLinkAsync(code);
        Assert.NotNull(first);
        var second = await _service.ValidateMagicLinkAsync(code);
        Assert.Null(second);
    }

    [Fact]
    public async Task ValidateMagicLinkAsync_ReturnsNull_AndDeletes_WhenExpired()
    {
        var email = $"expired_{Guid.NewGuid()}@example.com";
        var code = "EXPIRED_CODE_" + Guid.NewGuid().ToString("N");
        var expired = new MagicLink
        {
            Code = code,
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-6)
        };
        _context.MagicLinks.Add(expired);
        await _context.SaveChangesAsync();

        var result = await _service.ValidateMagicLinkAsync(code);
        Assert.Null(result);
        Assert.False(await _context.MagicLinks.AnyAsync(m => m.Code == code));
    }

    [Fact]
    public async Task GenerateMagicLinkAsync_CreatesUniqueCodes()
    {
        var email = $"multi_{Guid.NewGuid()}@example.com";
        var codes = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var code = await _service.GenerateMagicLinkAsync(email);
            codes.Add(code);
        }
        Assert.Equal(3, codes.Count);
    }
}
