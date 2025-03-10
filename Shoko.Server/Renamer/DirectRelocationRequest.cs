using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents a request to directly relocate a file.
/// </summary>
public record DirectRelocateRequest
{
    /// <summary>
    /// The managed folder where the file should be relocated to.
    /// </summary>
    public IManagedFolder? ManagedFolder { get; set; } = null;

    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/> where the file
    /// should be relocated to.
    /// </summary>
    public string? RelativePath { get; set; } = null;

    /// <summary>
    /// Indicates whether empty directories should be deleted after
    /// relocating the file.
    /// </summary>
    public bool DeleteEmptyDirectories { get; set; } = true;

    /// <summary>
    /// Indicates that we can relocate a video file that lives inside a
    /// drop destination managed folder that's not also a drop source.
    /// </summary>
    public bool AllowRelocationInsideDestination { get; set; } = true;
}
