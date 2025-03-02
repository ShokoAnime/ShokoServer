#nullable enable
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Image : Image_Base
{
    #region Properties

    /// <inheritdoc/>
    public override int ID => TMDB_ImageID;

    /// <inheritdoc/>
    public override bool IsLocked => false;

    /// <inheritdoc/>
    public override int Width { get; set; }

    /// <inheritdoc/>
    public override int Height { get; set; }

    /// <summary>
    /// Local id for image.
    /// </summary>
    public int TMDB_ImageID { get; set; }

    private string _remoteFileName = string.Empty;

    /// <summary>
    /// Remotely provided filename, if available.
    /// </summary>
    public string RemoteFileName
    {
        get => _remoteFileName;
        set
        {
            _remoteFileName = value;
            if (!string.IsNullOrEmpty(_remoteFileName) && _remoteFileName[0] != '/')
                _remoteFileName = '/' + _remoteFileName;
            _relativePath = null;
        }
    }

    private string RemoteImageName => _remoteFileName.EndsWith(".svg") ? _remoteFileName[..^4] + ".png" : _remoteFileName;

    /// <inheritdoc/>
    public override string RemoteURL => $"{TmdbMetadataService.ImageServerUrl}original{RemoteImageName}";

    private string? _relativePath;

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    [NotMapped]
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
    public override string LocalPath => ImageUtils.ResolvePath(RelativePath);

    /// <summary>
    /// Average user rating across all user votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when acquiring and/or discarding images.
    /// </remarks>
    public double UserRating { get; set; }

    /// <summary>
    /// User votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when acquiring and/or discarding images.
    /// </remarks>
    public int UserVotes { get; set; }

    #endregion

    #region Constructors

    public TMDB_Image()
    {
        Source = DataSourceEnum.TMDB;
        ImageType = ImageEntityType.None;
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
        if (Math.Abs(UserRating - data.VoteAverage) > 0.0001)
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
            : new TMDB_Image
            {
                RemoteFileName = RemoteFileName,
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
                ImageType = imageType ?? ImageType
            };

    #endregion
}
