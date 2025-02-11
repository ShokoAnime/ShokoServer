using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Image : Image_Base, IImageMetadata
{
    #region Properties

    /// <inheritdoc/>
    public override int ID => TMDB_ImageID;

    /// <inheritdoc/>
    public override bool IsLocked => false;

    /// <inheritdoc/>
    public override int Width { get; set; } = 0;

    /// <inheritdoc/>
    public override int Height { get; set; } = 0;

    /// <summary>
    /// Local id for image.
    /// </summary>
    public int TMDB_ImageID { get; set; }

    /// <inheritdoc/>
    public string RemoteFileName { get; set; } = string.Empty;

    private string RemoteImageName => RemoteFileName.EndsWith(".svg") ? RemoteFileName[..^4] + ".png" : RemoteFileName;

    /// <inheritdoc/>
    public override string? RemoteURL
        => string.IsNullOrEmpty(RemoteFileName) || string.IsNullOrEmpty(TmdbMetadataService.ImageServerUrl) ? null : $"{TmdbMetadataService.ImageServerUrl}original{RemoteImageName}";

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string? RelativePath
        => string.IsNullOrEmpty(RemoteFileName) ? null : Path.Join("TMDB", ImageType.ToString(), RemoteImageName);

    /// <inheritdoc/>
    public override string? LocalPath
        => ImageUtils.ResolvePath(RelativePath);

    /// <summary>
    /// Average user rating across all user votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when acquiring and/or discarding images.
    /// </remarks>
    public double UserRating { get; set; } = 0.0;

    /// <summary>
    /// User votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when acquiring and/or discarding images.
    /// </remarks>
    public int UserVotes { get; set; } = 0;

    #endregion

    #region Constructors

    public TMDB_Image() : base(DataSourceEnum.TMDB, ImageEntityType.None, 0) { }

    public TMDB_Image(string filePath, ImageEntityType type = ImageEntityType.None) : base(DataSourceEnum.TMDB, type, 0)
    {
        RemoteFileName = filePath?.Trim() ?? string.Empty;
        IsEnabled = true;
    }

    #endregion

    #region Methods

    public bool Populate(ImageData data)
    {
        var updated = false;
        if (Width != data.Width)
        {
            Width = data.Width;
            updated = true;
        }
        if (Height != data.Height)
        {
            Height = data.Height;
            updated = true;
        }
        var languageCode = string.IsNullOrEmpty(data.Iso_639_1) ? null : data.Iso_639_1;
        if (LanguageCode != languageCode)
        {
            LanguageCode = languageCode;
            updated = true;
        }
        if (UserRating != data.VoteAverage)
        {
            UserRating = data.VoteAverage;
            updated = true;
        }
        if (UserVotes != data.VoteCount)
        {
            UserVotes = data.VoteCount;
            updated = true;
        }
        return updated;
    }

    public TMDB_Image GetImageMetadata(bool? preferred = null, ImageEntityType? imageType = null)
        => (!preferred.HasValue || preferred.Value == IsPreferred) && (!imageType.HasValue || imageType.Value == ImageType)
            ? this
            : new TMDB_Image(RemoteFileName, imageType ?? ImageType)
            {
                IsEnabled = IsEnabled,
                IsPreferred = preferred ?? IsPreferred,
                LanguageCode = LanguageCode,
                Height = Height,
                Width = Width,
                TMDB_ImageID = TMDB_ImageID,
                UserRating = UserRating,
                UserVotes = UserVotes,
                _width = Width,
                _height = Height,
            };

    #endregion
}
