using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for batch updating multiple images.
/// </summary>
public class BatchUpdateImageBody
{
    /// <summary>
    ///   The UUIDs of the images to update.
    /// </summary>
    [Required]
    public List<Guid> ImageIDs { get; set; } = [];

    /// <summary>
    ///   The update data to apply to each image.
    /// </summary>
    [Required]
    public UpdateImageBody Update { get; set; } = new();
}
