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
    /// The image's locally  ID.
    /// </summary>
    [Required, Obsolete("Use UUID instead.")]
    public int ID { get; set; }

    /// <summary>
    ///  The image's universally/globally unique identifier (UUID/GUID).
    /// </summary>
    [Required]
    public Guid UID { get; set; }

    /// <summary>
    /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
    /// </summary>
    [Required]
    public ImageType Type { get; set; }

    /// <summary>
    /// AniDB, TMDB, etc.
    /// </summary>
    [Required]
    public DataSource Source { get; set; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// Or null if the image doesn't contain any language specifics.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// The relative path from the base image directory. A client with access to the server's filesystem can map
    /// these for quick access and no need for caching
    /// </summary>
    public string? RelativeFilepath { get; set; }

    /// <summary>
    /// Indicates the image is available locally and can be served through the
    /// API.
    /// </summary>
    [Required]
    public bool Available { get; set; }

    /// <summary>
    /// Indicates this is the preferred image for the <see cref="Type"/> for the
    /// selected entity.
    /// </summary>
    [Required]
    public bool Preferred { get; set; }

    /// <summary>
    /// Indicates the images is disabled. You must explicitly ask for these, for
    /// hopefully obvious reasons.
    /// </summary>
    [Required]
    public bool Disabled { get; set; }

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

    public Image(IImage imageMetadata, bool? preferredOverride = null)
    {
        ID = imageMetadata.LocalID;
        UID = imageMetadata.ID;
        Type = imageMetadata.Type.ToV3Dto();
        Source = imageMetadata.Source;

        Available = imageMetadata.IsEnabled;
        Preferred = preferredOverride ?? imageMetadata.IsPreferred;
        Disabled = !imageMetadata.IsEnabled;
        LanguageCode = imageMetadata.LanguageCode;
        if (imageMetadata.HasRating)
            CommunityRating = new()
            {
                Value = imageMetadata.Rating.Value,
                Votes = imageMetadata.RatingVotes.Value,
                MaxValue = 10,
                Type = "User",
                Source = imageMetadata.Source.ToString(),
            };
        Width = imageMetadata.Width;
        Height = imageMetadata.Height;

        // we need to set _something_ for the clients that determine
        // if an image exists by checking if a relative path is set,
        // so we set the id.
        RelativeFilepath = imageMetadata.IsAvailable ? $"1" : null;
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

    internal static DataSource GetRandomImageSource(ImageType imageType)
    {
        var sourceList = imageType switch
        {
            ImageType.Poster => _posterImageSources,
            ImageType.Banner => _bannerImageSources,
            ImageType.Backdrop => _backdropImageSources,
            _ => [],
        };

        return sourceList.GetRandomElement();
    }

    /// <summary>
    /// Image type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageType
    {
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
    public static ImageEntityType ToServer(this Image.ImageType type)
        => type switch
        {
            Image.ImageType.Primary => ImageEntityType.Primary,
            Image.ImageType.Poster => ImageEntityType.Primary,
            Image.ImageType.Character => ImageEntityType.Primary,
            Image.ImageType.Creator => ImageEntityType.Primary,
            Image.ImageType.Staff => ImageEntityType.Primary,
            Image.ImageType.Avatar => ImageEntityType.Primary,
            Image.ImageType.Banner => ImageEntityType.Banner,
            Image.ImageType.Backdrop => ImageEntityType.Backdrop,
            Image.ImageType.Thumbnail => ImageEntityType.Backdrop,
            Image.ImageType.Thumb => ImageEntityType.Backdrop,
            Image.ImageType.Fanart => ImageEntityType.Backdrop,
            Image.ImageType.Logo => ImageEntityType.Logo,
            Image.ImageType.Disc => ImageEntityType.Disc,
            _ => ImageEntityType.None,
        };

    public static Image.ImageType ToV3Dto(this ImageEntityType type)
        => type switch
        {
            ImageEntityType.Primary => Image.ImageType.Primary,
            ImageEntityType.Backdrop => Image.ImageType.Backdrop,
            ImageEntityType.Banner => Image.ImageType.Banner,
            ImageEntityType.Logo => Image.ImageType.Logo,
            ImageEntityType.Disc => Image.ImageType.Disc,
            _ => Image.ImageType.Staff,
        };
}
