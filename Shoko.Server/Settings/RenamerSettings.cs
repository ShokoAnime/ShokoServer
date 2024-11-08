using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class RenamerSettings
{
    public Dictionary<string, bool> EnabledRenamers { get; set; } = [];

    /// <summary>
    /// Indicates that we can rename a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool RenameOnImport { get; set; } = false;

    /// <summary>
    /// Indicates that we can move a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool MoveOnImport { get; set; } = false;

    /// <summary>
    /// Indicates that we can relocate a video file that lives inside a
    /// drop destination import folder that's not also a drop source on import.
    /// </summary>
    public bool AllowRelocationInsideDestinationOnImport { get; set; } = true;

    public string DefaultRenamer { get; set; } = "Default";
}
