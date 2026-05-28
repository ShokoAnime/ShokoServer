using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for setting the primary image link.
/// </summary>
public class SetPrimaryImageBody
{
    /// <summary>
    ///   The UUID of the image to set as the primary image.
    /// </summary>
    [Required]
    public Guid PrimaryImageID { get; set; }
}
