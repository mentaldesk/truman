using System.ComponentModel.DataAnnotations;

namespace Truman.Data.Entities;

public class UserTagPreference
{
    public int Id { get; set; }
    
    [Required]
    public int UserProfileId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Tag { get; set; } = string.Empty;
    
    public int Weight { get; set; } // 0 = banned, 1+ = favorite (higher = more important)
    
    // Navigation properties
    public UserProfile UserProfile { get; set; } = null!;
}
