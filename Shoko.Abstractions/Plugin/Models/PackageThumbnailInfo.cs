using System.IO;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
/// Information about a package or plugin thumbnail.
/// </summary>
public sealed class PackageThumbnailInfo
{
    /// <summary>
    /// The mime type of the thumbnail image.
    /// </summary>
    [JsonPropertyName("mime")]
    [JsonProperty("mime")]
    public required string MimeType { get; init; }

    /// <summary>
    /// The width of the thumbnail image.
    /// </summary>
    [JsonPropertyName("width")]
    [JsonProperty("width")]
    public required uint Width { get; init; }

    /// <summary>
    /// The height of the thumbnail image.
    /// </summary>
    [JsonPropertyName("height")]
    [JsonProperty("height")]
    public required uint Height { get; init; }

    /// <summary>
    /// The path to the thumbnail image.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonProperty("path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Get the thumbnail image stream.
    /// </summary>
    public Stream GetStream(IApplicationPaths applicationPaths) => File.OpenRead(
        FilePath
            .Replace("%PluginsPath%", applicationPaths.PluginsPath)
            .Replace("%ApplicationPaths%", applicationPaths.ApplicationPath)
    );
}
