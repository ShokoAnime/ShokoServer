using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Server.API.Converters;
using Shoko.Server.Extensions;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    /// The image's local ID. Remains available for backwards compatibility for
    /// now.
    /// </summary>
    [Required, Obsolete("Use UID instead.")]
    public int ID { get; set; }

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
    ///   The image type. Will always be <see cref="LegacyImageType.None"/> when
    ///   the image is directly retrieved from image manager. Will be set to any
    ///   other type when retrieved from a cross-reference or from an entity.
    /// </summary>
    [Required]
    public LegacyImageType Type { get; set; }

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
    public ImageSeriesInfo? Series { get; set; } = null;

    public Image(IImage imageMetadata, bool showLinkedIDs = false, bool? preferredOverride = null)
    {
        UID = imageMetadata.ID;
        ID = imageMetadata.LocalID;
        PrimaryUID = imageMetadata.PrimaryID;
        if (showLinkedIDs)
            LinkedUIDs = imageMetadata.LinkedIDs;
        Type = imageMetadata.Type.ToLegacyDto();
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

    internal static DataSource GetRandomImageSource(LegacyImageType imageType)
    {
        var sourceList = imageType switch
        {
            LegacyImageType.Poster => _posterImageSources,
            LegacyImageType.Banner => _bannerImageSources,
            LegacyImageType.Backdrop => _backdropImageSources,
            _ => [],
        };

        return sourceList.GetRandomElement();
    }

    /// <summary>
    /// Legacy image type. Kept for backwards compatibility on existing API
    /// routes. New endpoints should use <see cref="ImageEntityType"/> directly.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LegacyImageType
    {
        None = 0,

        /// <summary>
        /// The standard poster image. May or may not contain text.
        /// </summary>
        Primary = 1,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Poster = 2,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Character = 3,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Creator = 4,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Staff = 5,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Avatar = 6,

        /// <summary>
        /// A long/wide banner image, usually with text.
        /// </summary>
        Banner = 7,

        /// <summary>
        /// Backdrop / background images. Usually doesn't contain any text, but
        /// it might.
        /// </summary>
        Backdrop = 8,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Thumbnail = 9,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Thumb = 10,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Fanart = 11,

        /// <summary>
        /// Clear-text logo.
        /// </summary>
        Logo = 12,

        Disc = 13,
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
            /// The ID. A stringified int since we send the ID as a string
            /// from the API. Also see <seealso cref="Image.ID"/>.
            /// </summary>
            /// <value></value>
            [Required]
            [JsonConverter(typeof(AutoStringConverter))]
            public string ID { get; set; } = string.Empty;

            /// <summary>
            /// The image source.
            /// </summary>
            /// <value></value>
            [Obsolete("No longer necessary now that image IDs are universal.")]
            [JsonConverter(typeof(StringEnumConverter))]
            public DataSource Source { get; set; } = DataSource.None;
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

public static class ImageExtensions
{
    public static ImageEntityType ToServer(this Image.LegacyImageType type)
        => type switch
        {
            Image.LegacyImageType.Primary => ImageEntityType.Primary,
            Image.LegacyImageType.Poster => ImageEntityType.Primary,
            Image.LegacyImageType.Character => ImageEntityType.Primary,
            Image.LegacyImageType.Creator => ImageEntityType.Primary,
            Image.LegacyImageType.Staff => ImageEntityType.Primary,
            Image.LegacyImageType.Avatar => ImageEntityType.Primary,
            Image.LegacyImageType.Banner => ImageEntityType.Banner,
            Image.LegacyImageType.Backdrop => ImageEntityType.Backdrop,
            Image.LegacyImageType.Thumbnail => ImageEntityType.Backdrop,
            Image.LegacyImageType.Thumb => ImageEntityType.Backdrop,
            Image.LegacyImageType.Fanart => ImageEntityType.Backdrop,
            Image.LegacyImageType.Logo => ImageEntityType.Logo,
            Image.LegacyImageType.Disc => ImageEntityType.Disc,
            _ => ImageEntityType.None,
        };

    public static Image.LegacyImageType ToLegacyDto(this ImageEntityType type)
        => type switch
        {
            ImageEntityType.Primary => Image.LegacyImageType.Primary,
            ImageEntityType.Backdrop => Image.LegacyImageType.Backdrop,
            ImageEntityType.Banner => Image.LegacyImageType.Banner,
            ImageEntityType.Logo => Image.LegacyImageType.Logo,
            ImageEntityType.Disc => Image.LegacyImageType.Disc,
            _ => Image.LegacyImageType.None,
        };
}
