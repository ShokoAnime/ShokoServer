using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Server.API.v3.Models.ImageManagement;

/// <summary>
///   A slimmer cross-reference representation, used to tell which links an
///   entity sees a listed image through. The ID is the one the
///   <c>/api/v3/Image/Management/CrossReference/{xrefID}</c> routes take.
/// </summary>
public class ImageCrossReferenceSlim
{
    /// <summary>
    ///   The local cross-reference identifier.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    ///   The metadata source of the entity owning the link.
    /// </summary>
    [Required]
    public MetadataSource EntitySource { get; set; }

    /// <summary>
    ///   The metadata entity type of the entity owning the link.
    /// </summary>
    [Required]
    public MetadataEntityType EntityType { get; set; }

    /// <summary>
    ///   The ID of the entity owning the link, the file ID for a video.
    /// </summary>
    [Required]
    public string EntityID { get; set; }

    /// <summary>
    ///   Indicates the image is enabled through this link.
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; }

    /// <summary>
    ///   Indicates the image is desired through this link.
    /// </summary>
    [Required]
    public bool IsDesired { get; set; }

    /// <summary>
    ///   Indicates this link is the preferred one of its type for the entity
    ///   owning it.
    /// </summary>
    [Required]
    public bool IsPreferred { get; set; }

    /// <summary>
    ///   Creates the slim representation of a cross-reference.
    /// </summary>
    /// <param name="xref">The cross-reference.</param>
    public ImageCrossReferenceSlim(IImageCrossReference xref)
    {
        ID = xref.ID;
        EntitySource = xref.EntityID.Source;
        EntityType = xref.EntityID.EntityType;
        EntityID = ImageCrossReference.GetApiEntityID(xref);
        IsEnabled = xref.IsEnabled;
        IsDesired = xref.IsDesired;
        IsPreferred = xref.IsPreferred;
    }
}
