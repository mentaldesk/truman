using System.Net;
using System.Text.RegularExpressions;

namespace Truman.Data;

public static partial class SourceText
{
    public static int? CountWords(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(Tag().Replace(Hidden().Replace(html, " "), " "));
        return Word().Count(text);
    }

    [GeneratedRegex(@"<!--.*?-->|<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’.\-][\p{L}\p{N}]+)*")]
    private static partial Regex Word();
}
