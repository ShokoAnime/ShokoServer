
#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation.Input;

/// <summary>
///   Settings for the relocation service's APIv3 REST API.
/// </summary>
public class UpdateRelocationSettingsBody
{
    /// <summary>
    /// Indicates that we should rename a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool? RenameOnImport { get; set; }

    /// <summary>
    /// Indicates that we should move a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    public bool? MoveOnImport { get; set; }

    /// <summary>
    /// Indicates that we should relocate a video file that lives inside a
    /// drop destination managed folder that's not also a drop source on import.
    /// </summary>
    public bool? AllowRelocationInsideDestinationOnImport { get; set; }
}
