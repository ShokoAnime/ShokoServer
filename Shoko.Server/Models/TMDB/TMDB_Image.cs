using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

    private string _remoteFileName = string.Empty;

    /// <inheritdoc/>
    public string RemoteFileName
    {
        get => _remoteFileName;
        set
        {
            _remoteFileName = value;
            _relativePath = null;
        }
    }

    private string RemoteImageName => _remoteFileName.EndsWith(".svg") ? _remoteFileName[..^4] + ".png" : _remoteFileName;

    /// <inheritdoc/>
    public override string RemoteURL
        => $"{TmdbMetadataService.ImageServerUrl}original{RemoteImageName}";

    private string? _relativePath = null;

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string RelativePath
    {
        get
        {
            if (_relativePath is not null)
                return _relativePath;

            var fileExt = Path.GetExtension(RemoteImageName);
            var fileName = Path.GetFileNameWithoutExtension(_remoteFileName);
            var hashedFileName = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(fileName))).ToLower();
            return _relativePath = Path.Combine("TMDB", hashedFileName[..2], hashedFileName + fileExt);
        }
    }

    /// <inheritdoc/>
    public override string LocalPath
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
        _remoteFileName = filePath;
        if (!string.IsNullOrEmpty(_remoteFileName) && _remoteFileName[0] != '/')
            _remoteFileName = '/' + _remoteFileName;

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
        var languageCode = data.GetLanguageCode();
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

internal static class TmdbLibObjectExtensions
{
    /// <summary>
    /// The language/country code which indicates the language/country of the
    /// image is not specified.
    /// </summary>
    internal const string NotSpecifiedLanguage = "xx";

    /// <summary>
    /// Get the language code to use for the image, or null if not specified.
    /// </summary>
    /// <param name="image">The image to get the language code for.</param>
    /// <returns>The language code, or null if not specified.</returns>
    internal static string? GetLanguageCode(this ImageData image)
        => string.IsNullOrEmpty(image.Iso_639_1) || string.Equals(image.Iso_639_1, NotSpecifiedLanguage, StringComparison.InvariantCultureIgnoreCase) ? null : image.Iso_639_1;
}
