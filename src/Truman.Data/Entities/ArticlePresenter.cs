using System.ComponentModel.DataAnnotations;

namespace Truman.Data.Entities;

public class ArticlePresenter
{
    public int Id { get; set; }
    
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    
    public int PresenterId { get; set; }
    public Presenter Presenter { get; set; } = null!;
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Tldr { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
} 