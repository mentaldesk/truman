using Truman.Api.Features.TagPreferences;

namespace Truman.Api.Features.TagPreferences;

public interface ITagPreferenceService
{
    Task<TagPreferenceResponse> SetTagPreferenceAsync(string userEmail, string tag, int weight);
    Task<bool> RemoveTagPreferenceAsync(string userEmail, string tag);
    Task<List<TagPreferenceResponse>> GetUserTagPreferencesAsync(string userEmail);
    Task<TagPreferenceResponse> PromoteTagAsync(string userEmail, string tag);
    Task<TagPreferenceResponse> DemoteTagAsync(string userEmail, string tag);
    Task<Dictionary<int, List<string>>> GetTagsByWeightGroupAsync(string userEmail);
}
