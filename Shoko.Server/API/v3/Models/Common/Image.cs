using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Server.Extensions;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    ///  The image's universally/globally unique identifier (UUID/GUID).
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
    ///   The image type. Will always be <see cref="ImageEntityType.None"/> when
    ///   the image is directly retrieved from image manager. Will be set to any
    ///   other type when retrieved from a cross-reference or from an entity.
    /// </summary>
    [Required]
    public ImageEntityType Type { get; set; }

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
    /// Indicates this is the preferred image for the <see cref="Type"/> for the
    /// selected entity.
    /// </summary>
    [Required]
    public bool Preferred { get; set; }

    /// <summary>
    /// Indicates the image is desired for the selected entity.
    /// </summary>
    [Required]
    public bool Desired { get; set; }

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

    /// <summary>
    /// Community rating for the image, if available.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Rating? CommunityRating { get; set; }

    /// <summary>
    /// Series info for the image, currently only set when sending a random
    /// image.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageSeriesInfo? Series { get; set; }

    public Image(IImage imageMetadata, bool showLinkedIDs = false, bool? preferredOverride = null)
    {
        UID = imageMetadata.ID;
        PrimaryUID = imageMetadata.PrimaryID;
        if (showLinkedIDs)
            LinkedUIDs = imageMetadata.LinkedIDs;
        Type = imageMetadata.Type;
        Source = imageMetadata.Source;
        ResourceID = imageMetadata.ResourceID;
        ContentType = imageMetadata.ContentType;
        Available = imageMetadata.IsAvailable;
        Disabled = !imageMetadata.IsEnabled;
        Preferred = preferredOverride ?? imageMetadata.IsPreferred;
        Desired = imageMetadata.IsDesired;
        LanguageCode = imageMetadata.LanguageCode;
        CountryCode = imageMetadata.CountryCode;
        Width = imageMetadata.Width;
        Height = imageMetadata.Height;
        if (imageMetadata.HasRating)
            CommunityRating = new()
            {
                Value = imageMetadata.Rating.Value,
                Votes = imageMetadata.RatingVotes.Value,
                MaxValue = 10,
                Type = "User",
                Source = imageMetadata.Source.ToString(),
            };
    }

    private static readonly List<DataSource> _bannerImageSources =
    [
        DataSource.TMDB,
    ];

    private static readonly List<DataSource> _posterImageSources =
    [
        DataSource.AniDB,
        DataSource.TMDB,
    ];

    private static readonly List<DataSource> _backdropImageSources =
    [
        DataSource.TMDB,
    ];

    internal static DataSource GetRandomImageSource(ImageEntityType imageType)
    {
        var sourceList = imageType switch
        {
            ImageEntityType.Primary => _posterImageSources,
            ImageEntityType.Banner => _bannerImageSources,
            ImageEntityType.Backdrop => _backdropImageSources,
            _ => [],
        };

        return sourceList.GetRandomElement();
    }

    public class ImageSeriesInfo
    {
        /// <summary>
        /// The shoko series id.
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// The preferred series name for the user.
        /// </summary>
        [Required]
        public string Name { get; set; }

        public ImageSeriesInfo(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        public class DefaultImageBody
        {
            /// <summary>
            /// The image's universally/globally unique identifier (UUID/GUID).
            /// Also see <seealso cref="Image.UID"/>.
            /// </summary>
            public Guid ID { get; set; }
        }

        public class EnableImageBody
        {
            /// <summary>
            /// Indicates that the image should be enabled.
            /// </summary>
            [Required]
            public bool Enabled { get; set; }
        }
    }
}
