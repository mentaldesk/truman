using System;

namespace Truman.Data.Entities;

public class RssItem
{
    public int Id { get; set; }
    public string Link { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? PubDate { get; set; }
    public DateTimeOffset? TimeAnalysed { get; set; }
}
