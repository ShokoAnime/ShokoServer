using System.IO;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
/// Information about an image belonging to a package or plugin, such as its
/// thumbnail or its logo.
/// </summary>
public sealed class PackageImageInfo
{
    /// <summary>
    /// The mime type of the image.
    /// </summary>
    [JsonPropertyName("mime")]
    [JsonProperty("mime")]
    public required string MimeType { get; init; }

    /// <summary>
    /// The width of the image.
    /// </summary>
    [JsonPropertyName("width")]
    [JsonProperty("width")]
    public required int Width { get; init; }

    /// <summary>
    /// The height of the image.
    /// </summary>
    [JsonPropertyName("height")]
    [JsonProperty("height")]
    public required int Height { get; init; }

    /// <summary>
    /// The path to the image.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonProperty("path")]
    public required string FilePath { get; init; }

    /// <summary>
    ///   The image itself, when it is embedded in the plugin rather than a
    ///   file: served from memory, so nothing is written beside the plugin.
    ///   Not part of a manifest.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public byte[]? Data { get; init; }

    /// <summary>
    /// Get the image stream.
    /// </summary>
    public Stream? GetStream(IApplicationPaths applicationPaths)
    {
        if (Data is { Length: > 0 } data)
            return new MemoryStream(data, writable: false);

        var filePath = FilePath
            .Replace("%PluginsPath%", applicationPaths.PluginsPath)
            .Replace("%ApplicationPaths%", applicationPaths.ApplicationPath);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        return File.OpenRead(filePath);
    }
}
