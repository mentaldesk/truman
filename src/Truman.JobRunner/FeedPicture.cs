using System.Xml.Linq;

namespace Truman.JobRunner;

public static class FeedPicture
{
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";

    public static string? Find(XElement? item)
    {
        if (item is null)
        {
            return null;
        }

        var media = item.Elements().Concat(item.Elements(Media + "group").Elements());
        var candidates = media
            .Where(e => e.Name == Media + "thumbnail" || (e.Name == Media + "content" && IsImage(e)))
            .Concat(item.Elements("enclosure").Where(e => IsImageType((string?)e.Attribute("type"))));

        return candidates
            .Select(e => (Url: ((string?)e.Attribute("url"))?.Trim(), Area: Area(e)))
            .Where(c => IsWebAddress(c.Url))
            .OrderByDescending(c => c.Area)
            .Select(c => c.Url)
            .FirstOrDefault();
    }

    private static bool IsImage(XElement content)
    {
        var medium = (string?)content.Attribute("medium");
        var type = (string?)content.Attribute("type");
        return medium is null && type is null
            || string.Equals(medium, "image", StringComparison.OrdinalIgnoreCase)
            || IsImageType(type);
    }

    private static bool IsImageType(string? type) =>
        type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    private static long Area(XElement e)
    {
        var width = (long?)ParseDimension(e.Attribute("width"));
        var height = (long?)ParseDimension(e.Attribute("height"));
        return (width ?? height ?? 0) * (height ?? width ?? 0);
    }

    private static int? ParseDimension(XAttribute? attribute) =>
        int.TryParse((string?)attribute, out var value) && value > 0 ? value : null;

    private static bool IsWebAddress(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
