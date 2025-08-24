namespace Truman.Api.Features.TagPreferences;

public class TagPreferenceDto
{
    public string Tag { get; set; } = string.Empty;
    public int Weight { get; set; } // 0 = banned, 1+ = favorite (higher = more important)
}

public class SetTagPreferenceRequest
{
    public string Tag { get; set; } = string.Empty;
    public int Weight { get; set; } // 0 = banned, 1+ = favorite (higher = more important)
}

public class TagPreferenceResponse
{
    public string Tag { get; set; } = string.Empty;
    public int Weight { get; set; }
}
