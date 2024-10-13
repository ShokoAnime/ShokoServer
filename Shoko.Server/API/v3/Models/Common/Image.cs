using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    /// The image's ID.
    /// /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
    /// </summary>
    [Required]
    public ImageType Type { get; set; }

    /// <summary>
    /// AniDB, TMDB, etc.
    /// </summary>
    [Required]
    public ImageSource Source { get; set; }

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
    /// Indicates this is the preferred image for the <see cref="Type"/> for the
    /// selected entity.
    /// </summary>
    public bool Preferred { get; set; }

    /// <summary>
    /// Indicates the images is disabled. You must explicitly ask for these, for
    /// hopefully obvious reasons.
    /// </summary>
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
    /// Series info for the image, currently only set when sending a random
    /// image.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageSeriesInfo? Series { get; set; } = null;

    public Image(IImageMetadata imageMetadata)
    {
        ID = imageMetadata.ID;
        Type = imageMetadata.ImageType.ToV3Dto();
        Source = imageMetadata.Source.ToV3Dto();

        Preferred = imageMetadata.IsPreferred;
        Disabled = !imageMetadata.IsEnabled;
        LanguageCode = imageMetadata.LanguageCode;

        // we need to set _something_ for the clients that determine
        // if an image exists by checking if a relative path is set,
        // so we set the id.
        RelativeFilepath = imageMetadata.IsLocalAvailable ? $"/{ID}" : null;
        if (imageMetadata is TMDB_Image tmdbImage)
        {
            Width = tmdbImage.Width;
            Height = tmdbImage.Height;
        }
        else if (imageMetadata.IsLocalAvailable && Utils.SettingsProvider.GetSettings().LoadImageMetadata)
        {
            Width = imageMetadata.Width;
            Height = imageMetadata.Height;
        }
    }

    public Image(int id, ImageEntityType imageEntityType, DataSourceType dataSource, bool preferred = false, bool disabled = false)
    {
        ID = id;
        Type = imageEntityType.ToV3Dto();
        Source = dataSource.ToV3Dto();

        Preferred = preferred;
        Disabled = disabled;
        switch (dataSource)
        {
            case DataSourceType.User:
                if (imageEntityType == ImageEntityType.Art)
                {
                    var user = RepoFactory.JMMUser.GetByID(id);
                    if (user != null && user.HasAvatarImage)
                    {
                        var imageMetadata = user.AvatarImageMetadata;
                        // we need to set _something_ for the clients that determine
                        // if an image exists by checking if a relative path is set,
                        // so we set the id.
                        RelativeFilepath = $"/{id}";
                        Width = imageMetadata.Width;
                        Height = imageMetadata.Height;
                    }
                }
                break;

            // We can now grab the metadata from the database(!)
            case DataSourceType.TMDB:
                var tmdbImage = RepoFactory.TMDB_Image.GetByID(id);
                if (tmdbImage != null)
                {
                    LanguageCode = tmdbImage.LanguageCode;
                    var relativePath = tmdbImage.RelativePath;
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        RelativeFilepath = relativePath.Replace("\\", "/");
                        if (RelativeFilepath[0] != '/')
                            RelativeFilepath = "/" + RelativeFilepath;
                    }
                    Width = tmdbImage.Width;
                    Height = tmdbImage.Height;
                }
                break;

            default:
                var metadata = ImageUtils.GetImageMetadata(dataSource, imageEntityType, id);
                if (metadata is not null && metadata.IsLocalAvailable)
                {
                    RelativeFilepath = metadata.LocalPath!.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
                    if (RelativeFilepath[0] != '/')
                        RelativeFilepath = "/" + RelativeFilepath;
                    // This causes serious IO lag on some systems. Enable at own risk.
                    if (Utils.SettingsProvider.GetSettings().LoadImageMetadata)
                    {
                        Width = metadata.Width;
                        Height = metadata.Height;
                    }
                }
                break;
        }
    }

    private static readonly List<DataSourceType> _bannerImageSources =
    [
        DataSourceType.TMDB,
    ];

    private static readonly List<DataSourceType> _posterImageSources =
    [
        DataSourceType.AniDB,
        DataSourceType.TMDB,
    ];

    private static readonly List<DataSourceType> _thumbImageSources =
    [
        DataSourceType.TMDB,
    ];

    private static readonly List<DataSourceType> _backdropImageSources =
    [
        DataSourceType.TMDB,
    ];

    private static readonly List<DataSourceType> _characterImageSources =
    [
        DataSourceType.AniDB,
    ];

    private static readonly List<DataSourceType> _staffImageSources =
    [
        DataSourceType.AniDB,
    ];

    internal static DataSourceType GetRandomImageSource(ImageType imageType)
    {
        var sourceList = imageType switch
        {
            ImageType.Poster => _posterImageSources,
            ImageType.Banner => _bannerImageSources,
            ImageType.Thumb => _thumbImageSources,
            ImageType.Backdrop => _backdropImageSources,
            ImageType.Character => _characterImageSources,
            ImageType.Staff => _staffImageSources,
            _ => [],
        };

        return sourceList.GetRandomElement();
    }

    /// <summary>
    /// Image source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageSource
    {
        /// <summary>
        /// AniDB.
        /// </summary>
        AniDB = 1,

        /// <summary>
        /// The Movie DataBase (TMDB).
        /// </summary>
        TMDB = 3,

        /// <summary>
        /// User provided data.
        /// </summary>
        User = 99,

        /// <summary>
        /// Shoko.
        /// </summary>
        Shoko = 100,
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
        Poster = 1,

        /// <summary>
        /// A long/wide banner image, usually with text.
        /// </summary>
        Banner = 2,

        /// <summary>
        /// Thumbnail image.
        /// </summary>
        Thumbnail = 3,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Thumb = Thumbnail,

        /// <summary>
        /// Backdrop / background images. Usually doesn't contain any text, but
        /// it might.
        /// </summary>
        Backdrop = 4,

        /// <summary>
        /// Temp. synonym until it's safe to remove it.
        /// </summary>
        Fanart = Backdrop,

        /// <summary>
        /// Character image. May be a close up portrait of the character, or a
        /// full-body view of the character.
        /// </summary>
        Character = 5,

        /// <summary>
        /// Staff image. May be a close up portrait of the person, or a
        /// full-body view of the person.
        /// </summary>
        Staff = 6,

        /// <summary>
        /// Clear-text logo.
        /// </summary>
        Logo = 7,

        /// <summary>
        /// User avatar.
        /// </summary>
        Avatar = 99,
    }

    public class ImageSeriesInfo
    {
        /// <summary>
        /// The shoko series id.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The preferred series name for the user.
        /// </summary>
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
            [Range(0, int.MaxValue)]
            public int ID { get; set; }

            /// <summary>
            /// The image source.
            /// </summary>
            /// <value></value>
            [Required]
            public ImageSource Source { get; set; }
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
            Image.ImageType.Avatar => ImageEntityType.Art,
            Image.ImageType.Banner => ImageEntityType.Banner,
            Image.ImageType.Character => ImageEntityType.Character,
            Image.ImageType.Backdrop => ImageEntityType.Backdrop,
            Image.ImageType.Poster => ImageEntityType.Poster,
            Image.ImageType.Staff => ImageEntityType.Person,
            Image.ImageType.Thumb => ImageEntityType.Thumbnail,
            Image.ImageType.Logo => ImageEntityType.Logo,
            _ => ImageEntityType.None,
        };

    public static Image.ImageType ToV3Dto(this ImageEntityType type)
        => type switch
        {
            ImageEntityType.Art => Image.ImageType.Avatar,
            ImageEntityType.Banner => Image.ImageType.Banner,
            ImageEntityType.Character => Image.ImageType.Character,
            ImageEntityType.Backdrop => Image.ImageType.Backdrop,
            ImageEntityType.Poster => Image.ImageType.Poster,
            ImageEntityType.Person => Image.ImageType.Staff,
            ImageEntityType.Thumbnail => Image.ImageType.Thumb,
            ImageEntityType.Logo => Image.ImageType.Logo,
            _ => Image.ImageType.Staff,
        };

    public static DataSourceType ToServer(this Image.ImageSource source)
        => Enum.Parse<DataSourceType>(source.ToString());

    public static Image.ImageSource ToV3Dto(this DataSourceType source)
        => Enum.Parse<Image.ImageSource>(source.ToString());

    public static Image.ImageSource ToV3Dto(this DataSourceEnum source)
        => Enum.Parse<Image.ImageSource>(source.ToString());
}
