using ShiroBot.Model.Common;

namespace ShiroBot.MilkyAdapter;

internal static class ResourceUriConverter
{
    public static bool ForceFileBase64 { get; set; }

    public static string Convert(string uri)
    {
        if (!ForceFileBase64 || !uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var path = Uri.TryCreate(uri, UriKind.Absolute, out var fileUri) && fileUri.IsFile
            ? fileUri.LocalPath
            : Uri.UnescapeDataString(uri["file://".Length..]);

        var bytes = File.ReadAllBytes(path);
        return "base64://" + System.Convert.ToBase64String(bytes);
    }

    public static IReadOnlyList<OutgoingSegment> Convert(IReadOnlyList<OutgoingSegment> segments)
    {
        if (!ForceFileBase64) return segments;

        var converted = segments.Select(ConvertSegment).ToArray();
        return converted;
    }

    private static OutgoingSegment ConvertSegment(OutgoingSegment segment)
    {
        return segment switch
        {
            ImageOutgoingSegment image => image with { Uri = Convert(image.Uri) },
            VideoOutgoingSegment video => video with
            {
                Uri = Convert(video.Uri),
                ThumbUri = video.ThumbUri is null ? null : Convert(video.ThumbUri)
            },
            RecordOutgoingSegment record => record with { Uri = Convert(record.Uri) },
            _ => segment
        };
    }
}
