using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents a request to directly relocate a file.
/// </summary>
public record DirectRelocateRequest
{
    /// <summary>
    /// The import folder where the file should be relocated to.
    /// </summary>
    public IImportFolder? ImportFolder = null;

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/> where the file
    /// should be relocated to.
    /// </summary>
    public string? RelativePath = null;

    /// <summary>
    /// Indicates whether empty directories should be deleted after
    /// relocating the file.
    /// </summary>
    public bool DeleteEmptyDirectories = true;

    /// <summary>
    /// Indicates that we can relocate a video file that lives inside a
    /// drop destination import folder that's not also a drop source.
    /// </summary>
    public bool AllowRelocationInsideDestination { get; set; } = true;
}
