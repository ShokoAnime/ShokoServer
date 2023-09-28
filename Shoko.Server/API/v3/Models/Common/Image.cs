using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    /// AniDB, TvDB, TMDB, etc.
    /// </summary>
    [Required]
    public ImageSource Source { get; set; }

    /// <summary>
    /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
    /// </summary>
    [Required]
    public ImageType Type { get; set; }

    /// <summary>
    /// The image's ID.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// The relative path from the base image directory. A client with access to the server's filesystem can map
    /// these for quick access and no need for caching
    /// </summary>
    public string? RelativeFilepath { get; set; }

    /// <summary>
    /// Is it marked as default. Only one default is possible for a given <see cref="Image.Type"/>.
    /// </summary>
    public bool Preferred { get; set; }

    /// <summary>
    /// Width of the image.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Is it marked as disabled. You must explicitly ask for these, for obvious reasons.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Series info for the image, currently only set when sending a random
    /// image.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageSeriesInfo? Series { get; set; } = null;

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
                    var relativePath = tmdbImage.RelativePath;
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        RelativeFilepath = relativePath.Replace("\\", "/");
                        if (!RelativeFilepath.StartsWith("/"))
                            RelativeFilepath = "/" + RelativeFilepath;
                    }
                    Width = tmdbImage.Width;
                    Height = tmdbImage.Height;
                }
                break;

            default:
                var imagePath = ImageUtils.GetLocalPath(dataSource, imageEntityType, id);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    RelativeFilepath = imagePath.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
                    if (!RelativeFilepath.StartsWith("/"))
                        RelativeFilepath = "/" + RelativeFilepath;
                    // This causes serious IO lag on some systems. Enable at own risk.
                    if (Utils.SettingsProvider.GetSettings().LoadImageMetadata)
                    {
                        var info = new MagickImageInfo(imagePath);
                        Width = info.Width;
                        Height = info.Height;
                    }
                }
                break;
        }
    }

    private static readonly List<DataSourceType> BannerImageSources = new() { DataSourceType.TvDB };

    private static readonly List<DataSourceType> PosterImageSources = new()
    {
        DataSourceType.AniDB,
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    };

    // There is only one thumbnail provider atm.
    private static readonly List<DataSourceType> ThumbImageSources = new() { DataSourceType.TvDB };

    // TMDB is too unreliable atm, so we will only use TvDB for now.
    private static readonly List<DataSourceType> FanartImageSources = new()
    {
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    };

    private static readonly List<DataSourceType> CharacterImageSources = new()
    {
        // DataSourceType.AniDB,
        DataSourceType.Shoko
    };

    private static readonly List<DataSourceType> StaffImageSources = new()
    {
        // DataSourceType.AniDB,
        DataSourceType.Shoko
    };

    internal static DataSourceType GetRandomImageSource(ImageType imageType)
    {
        var sourceList = imageType switch
        {
            ImageType.Poster => PosterImageSources,
            ImageType.Banner => BannerImageSources,
            ImageType.Thumb => ThumbImageSources,
            ImageType.Fanart => FanartImageSources,
            ImageType.Character => CharacterImageSources,
            _ => StaffImageSources,
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
        ///
        /// </summary>
        AniDB = 1,

        /// <summary>
        ///
        /// </summary>
        TvDB = 2,

        /// <summary>
        ///
        /// </summary>
        TMDB = 3,

        /// <summary>
        /// User provided data.
        /// </summary>
        User = 99,

        /// <summary>
        ///
        /// </summary>
        Shoko = 100
    }

    /// <summary>
    /// Image type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageType
    {
        /// <summary>
        ///
        /// </summary>
        Poster = 1,

        /// <summary>
        ///
        /// </summary>
        Banner = 2,

        /// <summary>
        ///
        /// </summary>
        Thumb = 3,

        /// <summary>
        ///
        /// </summary>
        Fanart = 4,

        /// <summary>
        ///
        /// </summary>
        Character = 5,

        /// <summary>
        ///
        /// </summary>
        Staff = 6,

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
            public int ID { get; set; }

            /// <summary>
            /// The image source.
            /// </summary>
            /// <value></value>
            [Required]
            public ImageSource Source { get; set; }
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
            Image.ImageType.Fanart => ImageEntityType.Backdrop,
            Image.ImageType.Poster => ImageEntityType.Poster,
            Image.ImageType.Staff => ImageEntityType.Person,
            Image.ImageType.Thumb => ImageEntityType.Thumbnail,
            _ => ImageEntityType.None,
        };

    public static Image.ImageType ToV3Dto(this ImageEntityType type)
        => type switch
        {
            ImageEntityType.Art => Image.ImageType.Avatar,
            ImageEntityType.Banner => Image.ImageType.Banner,
            ImageEntityType.Character => Image.ImageType.Character,
            ImageEntityType.Backdrop => Image.ImageType.Fanart,
            ImageEntityType.Poster => Image.ImageType.Poster,
            ImageEntityType.Person => Image.ImageType.Staff,
            ImageEntityType.Thumbnail => Image.ImageType.Thumb,
            _ => Image.ImageType.Staff,
        };

    public static DataSourceType ToServer(this Image.ImageSource source)
        => Enum.Parse<DataSourceType>(source.ToString());

    public static Image.ImageSource ToV3Dto(this DataSourceType source)
        => Enum.Parse<Image.ImageSource>(source.ToString());
}
