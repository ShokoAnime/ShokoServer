using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Release.Input;

/// <summary>
/// Request body for executing release deletion.
/// </summary>
public class DeleteReleasesBody
{
    /// <summary>
    /// VideoLocal_Place IDs of the files to delete. Typically populated from
    /// <see cref="ReleaseDeletionPreview.PlaceToDelete.PlaceID"/> after reviewing
    /// the preview.
    /// </summary>
    [Required]
    public required List<int> PlaceIDs { get; set; }
}
