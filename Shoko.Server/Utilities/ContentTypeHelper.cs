using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Shoko.Server.Utilities;

/// <summary>
///   A helper class for content type detection.
/// </summary>
public static class ContentTypeHelper
{
    private static readonly ImmutableDictionary<string, string> _extensionToMimeDict;

    private static readonly ImmutableDictionary<string, string> _mimeTypeToExtensionDict;

    /// <summary>
    ///   The content type for an unknown file.
    /// </summary>
    public const string UnknownMimeType = "application/octet-stream";

    static ContentTypeHelper()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Image formats.
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".webp", "image/webp" },
            { ".bmp", "image/bmp" },
            { ".tiff", "image/tiff" },

            // Video formats.
            { ".3g2", "video/3gpp2" },
            { ".3gp", "video/3gpp" },
            { ".asf", "video/x-ms-asf" },
            { ".avi", "video/x-msvideo" },
            { ".av1", "video/AV1" },
            { ".divx", "video/divx" },
            { ".f4v", "video/x-f4v" },
            { ".flv", "video/x-flv" },
            { ".h265", "video/h265" },
            { ".h264", "video/h264" },
            { ".m1v", "video/mpeg" },
            { ".m2ts", "video/mp2t" },
            { ".m2v", "video/mpeg" },
            { ".m4v", "video/x-m4v" },
            { ".mk3d", "video/x-matroska" },
            { ".mkv", "video/x-matroska" },
            { ".mov", "video/quicktime" },
            { ".mp4", "video/mp4" },
            { ".mpg", "video/mpeg" },
            { ".mpeg", "video/mpeg" },
            { ".mts", "video/mp2t" },
            { ".mxf", "application/mxf" },
            { ".nsv", "video/x-nsv" },
            { ".nut", "video/x-nut" },
            { ".ogm", "video/ogg" },
            { ".ogv", "video/ogg" },
            { ".ogg", "video/ogg" },
            { ".qt", "video/quicktime" },
            { ".rm", "application/vnd.rn-realmedia" },
            { ".roq", "video/quicktime" },
            { ".swf", "application/x-shockwave-flash" },
            { ".tp", "video/mp2t" },
            { ".trp", "video/mp2t" },
            { ".ts", "video/mp2t" },
            { ".vob", "video/dvd" },
            { ".wmv", "video/x-ms-wmv" },
            { ".webm", "video/webm" },
            { ".xvid", "video/xvid" },

            // Audio formats.
            { ".aac", "audio/aac" },
            { ".aif", "audio/x-aiff" },
            { ".aiff", "audio/x-aiff" },
            { ".amr", "audio/amr" },
            { ".flac", "audio/flac" },
            { ".m4a", "audio/mp4" },
            { ".mid", "audio/midi" },
            { ".midi", "audio/midi" },
            { ".mp3", "audio/mpeg" },
            { ".mpga", "audio/mpeg" },
            { ".oga", "audio/ogg" },
            { ".opus", "audio/opus" },
            { ".pcm", "audio/x-pcm" },
            { ".wav", "audio/wav" },
            { ".weba", "audio/webm" },
            { ".wma", "audio/x-ms-wma" },

            // Subtitle formats.
            { ".srt", "application/x-subrip" },
            { ".vtt", "text/vtt" },
            { ".ass", "text/x-ass" },
            { ".ssa", "text/x-ssa" },
            { ".ttml", "application/ttml+xml" },
            { ".idx", "application/x-vobsub-idx" },
            { ".sub", "image/x-vobsub-sub" },

            // Misc. other formats.
            { ".json", "application/json" },
            { ".xml", "application/xml" },
            { ".txt", "text/plain" },
            { ".html", "text/html" },
        };

        _extensionToMimeDict = map.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        _mimeTypeToExtensionDict = map
            .OrderBy(kvp => kvp.Key.Length)
            .DistinctBy(x => x.Value)
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetContentType(string? fileOrExtension)
    {
        if (string.IsNullOrWhiteSpace(fileOrExtension))
            return UnknownMimeType;

        var input = Path.GetExtension(fileOrExtension.Trim());
        if (string.IsNullOrEmpty(input))
            return UnknownMimeType;

        if (!input.StartsWith('.'))
            input = "." + input;

        return _extensionToMimeDict.TryGetValue(input, out var contentType) ? contentType : UnknownMimeType;
    }

    public static bool TryGetContentType(string? fileOrExtension, [NotNullWhen(true)] out string? contentType)
    {
        contentType = GetContentType(fileOrExtension);
        if (contentType is not UnknownMimeType)
            return true;

        contentType = null;
        return false;
    }

    public static string? GetExtensionForMimeType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        contentType = contentType.Trim().ToLowerInvariant();
        return _mimeTypeToExtensionDict.TryGetValue(contentType, out var ext) ? ext : null;
    }

    public static bool TryGetExtensionForMimeType(string? contentType, [NotNullWhen(true)] out string? extension)
    {
        extension = GetExtensionForMimeType(contentType);
        return extension is not null;
    }
}
