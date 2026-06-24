using System.IO;

#nullable enable
namespace Shoko.Server.Utilities
{
    internal static class ContentTypeHelper
    {
        // ponytail: central unknown mime constant to avoid repeated literals across the codebase.
        public const string UnknownMimeType = "application/octet-stream";

        // Minimal, explicit mapping to avoid an external dependency. Ponytail: keeps behavior predictable for common types.
        private static readonly System.Collections.Generic.Dictionary<string, string> _map =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".webp", "image/webp" },
            { ".bmp", "image/bmp" },
            { ".tiff", "image/tiff" },
            { ".json", "application/json" },
            { ".xml", "application/xml" },
            { ".txt", "text/plain" },
            { ".html", "text/html" },
        };

        public static string GetMimeMapping(string? fileOrExtension)
        {
            if (string.IsNullOrWhiteSpace(fileOrExtension))
                return UnknownMimeType;

            var input = fileOrExtension.Trim();
            if (input.Contains("/") || input.Contains("\\"))
                input = Path.GetExtension(input);

            if (string.IsNullOrEmpty(input))
                return UnknownMimeType;

            if (!input.StartsWith('.'))
                input = "." + input;

            return _map.TryGetValue(input, out var ct) ? ct : UnknownMimeType;
        }

        public static string? GetExtensionForMimeType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return null;

            contentType = contentType.Trim().ToLowerInvariant();
            return contentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/svg+xml" => ".svg",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tiff",
                _ => null,
            };
        }
    }
}
