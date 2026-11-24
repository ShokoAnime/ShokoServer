using Newtonsoft.Json;

namespace Shoko.Server.Settings;

public class RelocationSettings
{
    /// <summary>
    /// Indicates that we should relocate a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    [JsonIgnore]
    public bool RelocateOnImport => MoveOnImport || RenameOnImport;

    /// <summary>
    /// Indicates that we should rename a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool RenameOnImport { get; set; } = false;

    /// <summary>
    /// Indicates that we should move a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool MoveOnImport { get; set; } = false;

    /// <summary>
    /// Indicates that we should relocate a video file that lives inside a
    /// drop destination managed folder that's not also a drop source on import.
    /// </summary>
    public bool AllowRelocationInsideDestinationOnImport { get; set; } = true;

    /// <summary>
    /// Indicates that relocating a video file onto the path of another copy of
    /// the same video replaces it rather than leaving the file where it is. A
    /// symbolic link is never followed: a link to the file, or a hard link of
    /// it, is removed, and a link at the destination is replaced by the file.
    /// Off by default.
    /// </summary>
    public bool ReplaceSameVideoAtDestination { get; set; } = false;
}
