using System.ComponentModel.DataAnnotations;

namespace Truman.Data.Entities;

public class UserProfile
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string Email { get; set; } = string.Empty;
    public int Mood { get; set; } = 5;
    // Store the stack rank as a JSON array of strings
    public string SelectedValues { get; set; } = "[]";
} 