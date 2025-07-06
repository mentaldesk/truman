namespace Truman.Api.Features.Profile;

public class UserProfileDto
{
    public int Mood { get; set; } = 5;
    public List<string> SelectedValues { get; set; } = new();
} 