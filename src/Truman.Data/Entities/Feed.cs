namespace Truman.Data.Entities;

public class Feed
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
