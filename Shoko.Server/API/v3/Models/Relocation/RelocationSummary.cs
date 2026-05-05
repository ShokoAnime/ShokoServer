using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation;

public class RelocationSummary
{
    /// <summary>
    /// Indicates that we should rename a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    [Required]
    public bool RenameOnImport { get; set; }

    /// <summary>
    /// Indicates that we should move a video file on import, and after metadata
    /// updates when the metadata related to the file may have changed.
    /// </summary>
    [Required]
    public bool MoveOnImport { get; set; }

    /// <summary>
    /// Indicates that we should relocate a video file that lives inside a
    /// drop destination managed folder that's not also a drop source on import.
    /// </summary>
    [Required]
    public bool AllowRelocationInsideDestinationOnImport { get; set; }

    /// <summary>
    ///   Gets the number of available hash providers to pick from.
    /// </summary>
    [Required]
    public int ProviderCount { get; set; }
}
