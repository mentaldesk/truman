using Truman.Api.Features.TagPreferences;

namespace Truman.Api.Features.TagPreferences;

public interface ITagPreferenceService
{
    Task<TagPreferenceResponse> SetTagPreferenceAsync(string userEmail, string tag, int weight);
    Task<bool> RemoveTagPreferenceAsync(string userEmail, string tag);
    Task<List<TagPreferenceResponse>> GetUserTagPreferencesAsync(string userEmail);
    Task<TagPreferenceResponse> BumpTagPriorityAsync(string userEmail, string tag);
    Task<Dictionary<int, List<string>>> GetTagsByWeightGroupAsync(string userEmail);
}
