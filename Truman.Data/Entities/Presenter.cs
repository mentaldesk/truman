using System.ComponentModel.DataAnnotations;

namespace Truman.Data.Entities;

public class Presenter
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string PresenterStyle { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Label { get; set; } = string.Empty;
} 