namespace Truman.JobRunner;

static class MetaDataExtensions
{
    public static T? ReadValue<T>(this IReadOnlyDictionary<string, object>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var value))
        {
            return default;
        }
        if (value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
}