using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

public class RenamerSettings
{
    /// <summary>
    /// Dictionary of enabled renamers.
    /// </summary>
    [Visibility(Visibility = DisplayVisibility.ReadOnly)]
    public Dictionary<string, bool> EnabledRenamers { get; set; } = [];

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
    /// Name of the default renamer in use.
    /// </summary>
    [Visibility(Visibility = DisplayVisibility.ReadOnly)]
    public string DefaultRenamer { get; set; } = "Default";
}
