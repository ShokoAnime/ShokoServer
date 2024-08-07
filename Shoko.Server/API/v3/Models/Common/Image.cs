using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
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
    /// AniDB, TvDB, TMDB, etc.
    /// </summary>
    [Required]
    public ImageSource Source { get; set; }

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

        // we need to set _something_ for the clients that determine
        // if an image exists by checking if a relative path is set,
        // so we set the id.
        RelativeFilepath = imageMetadata.IsLocalAvailable ? $"/{ID}" : null;
        if (imageMetadata.IsLocalAvailable || Utils.SettingsProvider.GetSettings().LoadImageMetadata)
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
        DataSourceType.TvDB,
    ];

    private static readonly List<DataSourceType> _posterImageSources =
    [
        DataSourceType.AniDB,
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    ];

    private static readonly List<DataSourceType> _thumbImageSources =
    [
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    ];

    private static readonly List<DataSourceType> _backdropImageSources =
    [
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    ];

    private static readonly List<DataSourceType> _characterImageSources =
    [
        DataSourceType.AniDB,
        DataSourceType.Shoko
    ];

    private static readonly List<DataSourceType> _staffImageSources =
    [
        DataSourceType.AniDB,
        DataSourceType.Shoko
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
        Backdrop = 4,

        /// <summary>
        ///
        /// </summary>
        Fanart = Backdrop,

        /// <summary>
        ///
        /// </summary>
        Character = 5,

        /// <summary>
        ///
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
