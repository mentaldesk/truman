using System.Security.Cryptography;

namespace Truman.Api.Features.Auth;

public record MagicLinkRecord(string Code, string Email, DateTime ExpiresAt);

public interface IMagicLinkService
{
    Task<string> GenerateMagicLinkAsync(string email);
    Task<MagicLinkRecord?> ValidateMagicLinkAsync(string code);
}

public class MagicLinkService : IMagicLinkService
{
    private static readonly Dictionary<string, MagicLinkRecord> _magicLinks = new();
    
    public Task<string> GenerateMagicLinkAsync(string email)
    {
        // Generate a cryptographically secure random code
        var codeBytes = new byte[32];
        RandomNumberGenerator.Fill(codeBytes);
        var code = Convert.ToBase64String(codeBytes)
            .Replace("/", "_")  // Make it URL safe
            .Replace("+", "-")
            .Replace("=", "");
            
        var record = new MagicLinkRecord(
            Code: code,
            Email: email,
            ExpiresAt: DateTime.UtcNow.AddMinutes(5)
        );
        
        // Store the record
        _magicLinks[code] = record;
        
        return Task.FromResult(code);
    }
    
    public Task<MagicLinkRecord?> ValidateMagicLinkAsync(string code)
    {
        // Try to get the record and validate it
        if (_magicLinks.TryGetValue(code, out var record))
        {
            if (record.ExpiresAt > DateTime.UtcNow)
            {
                // Remove the used code
                _magicLinks.Remove(code);
                return Task.FromResult<MagicLinkRecord?>(record);
            }
            
            // Remove expired code
            _magicLinks.Remove(code);
        }
        
        return Task.FromResult<MagicLinkRecord?>(null);
    }
} 