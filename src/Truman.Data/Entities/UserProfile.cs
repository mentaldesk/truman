using System.ComponentModel.DataAnnotations;

namespace Truman.Data.Entities;

public class UserProfile
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string Email { get; set; } = string.Empty;
    public int Mood { get; set; } = 5;
    public string SelectedValues { get; set; } = "[]";
    public DateTimeOffset? FeedSeenAt { get; set; }
    public DateTimeOffset? NewSince { get; set; }
    public ICollection<UserTagPreference> TagPreferences { get; set; } = [];
} 