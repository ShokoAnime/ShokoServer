using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Release.Input;

/// <summary>
/// Request body for computing a release override deletion preview.
/// </summary>
public class ReleaseOverrideBody
{
    /// <summary>
    /// The <see cref="Models.Release.ReleaseOverride.OverrideFile.PlaceID"/> values of
    /// files to <em>keep</em>. Every other file for the series will be included in
    /// the deletion preview. The selection must collectively cover every episode
    /// that has at least one file, otherwise the request is rejected.
    /// </summary>
    [Required]
    public required List<int> SelectedPlaceIDs { get; set; }
}
