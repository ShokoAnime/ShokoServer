using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for batch updating multiple image cross-references.
/// </summary>
public class BatchUpdateImageCrossReferenceBody
{
    /// <summary>
    ///   The IDs of the cross-references to update.
    /// </summary>
    [Required]
    public List<int> CrossReferenceIDs { get; set; } = [];

    /// <summary>
    ///   The update data to apply to each cross-reference.
    /// </summary>
    [Required]
    public UpdateImageCrossReferenceBody Update { get; set; } = new();
}
