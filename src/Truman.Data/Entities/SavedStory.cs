namespace Truman.Data.Entities;

public class SavedStory
{
    public int Id { get; set; }

    public int UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;

    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
