using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Server.API.v3.Models.ImageManagement;

/// <summary>
/// A slimmer image representation used when embedding an image inside
/// another DTO (e.g. a cross-reference). Omits legacy fields and fields
/// that originate from the cross-reference itself (type, rating, preferred,
/// desired).
/// </summary>
public class ImageSlim
{
    /// <summary>
    ///   The image's universally/globally unique identifier (UUID/GUID).
    /// </summary>
    [Required]
    public Guid UID { get; set; }

    /// <summary>
    /// Primary image's universally/globally unique identifier (UUID/GUID) in
    /// the linked image list.
    /// </summary>
    [Required]
    public Guid PrimaryUID { get; set; }

    /// <summary>
    ///   Extra image IDs in the linked image list, except the primary image.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Guid>? LinkedUIDs { get; set; }

    /// <summary>
    /// The image source.
    /// </summary>
    [Required]
    public DataSource Source { get; set; }

    /// <summary>
    /// The image's resource identifier.
    /// </summary>
    [Required]
    public string ResourceID { get; set; }

    /// <summary>
    /// The image's content type.
    /// </summary>
    [Required]
    public string ContentType { get; set; }

    /// <summary>
    /// Indicates the image is available locally and can be served through the
    /// API.
    /// </summary>
    [Required]
    public bool Available { get; set; }

    /// <summary>
    /// Indicates the images is disabled. You must explicitly ask for these, for
    /// hopefully obvious reasons.
    /// </summary>
    [Required]
    public bool Disabled { get; set; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// Or null if the image doesn't contain any language specifics.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Country code for the language used for the text in the image, if any.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Width of the image.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image.
    /// </summary>
    public int? Height { get; set; }

    public ImageSlim(IImage imageMetadata, bool showLinkedIDs = false)
    {
        UID = imageMetadata.ID;
        PrimaryUID = imageMetadata.PrimaryID;
        if (showLinkedIDs)
            LinkedUIDs = imageMetadata.LinkedIDs;
        Source = imageMetadata.Source;
        ResourceID = imageMetadata.ResourceID;
        ContentType = imageMetadata.ContentType;
        Available = imageMetadata.IsAvailable;
        Disabled = !imageMetadata.IsEnabled;
        LanguageCode = imageMetadata.LanguageCode;
        CountryCode = imageMetadata.CountryCode;
        Width = imageMetadata.Width;
        Height = imageMetadata.Height;
    }
}
