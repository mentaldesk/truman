using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Auth;

public record MagicLinkRecord(string Code, string Email, DateTime ExpiresAt);

public interface IMagicLinkService
{
    Task<string> GenerateMagicLinkAsync(string email);
    Task<MagicLinkRecord?> ValidateMagicLinkAsync(string code);
}

public class MagicLinkService : IMagicLinkService
{
    private readonly TrumanDbContext _dbContext;

    public MagicLinkService(TrumanDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<string> GenerateMagicLinkAsync(string email)
    {
        // Generate a cryptographically secure random code
        var codeBytes = new byte[32];
        RandomNumberGenerator.Fill(codeBytes);
        var code = Convert.ToBase64String(codeBytes)
            .Replace("/", "_")  // Make it URL safe
            .Replace("+", "-")
            .Replace("=", "");
            
        var magicLink = new MagicLink
        {
            Code = code,
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        
        _dbContext.MagicLinks.Add(magicLink);
        await _dbContext.SaveChangesAsync();
        
        return code;
    }
    
    public async Task<MagicLinkRecord?> ValidateMagicLinkAsync(string code)
    {
        var magicLink = await _dbContext.MagicLinks
            .FirstOrDefaultAsync(m => m.Code == code);

        if (magicLink == null)
        {
            return null;
        }

        // Check if the link is expired
        if (magicLink.ExpiresAt <= DateTime.UtcNow)
        {
            _dbContext.MagicLinks.Remove(magicLink);
            await _dbContext.SaveChangesAsync();
            return null;
        }

        // Remove the used code
        _dbContext.MagicLinks.Remove(magicLink);
        await _dbContext.SaveChangesAsync();

        return new MagicLinkRecord(
            Code: magicLink.Code,
            Email: magicLink.Email,
            ExpiresAt: magicLink.ExpiresAt
        );
    }
} 