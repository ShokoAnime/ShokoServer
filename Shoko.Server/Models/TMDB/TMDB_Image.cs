using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
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

    /// <summary>
    /// Related TMDB Movie entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbMovieID { get; set; }

    /// <summary>
    /// Related TMDB Episode entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbEpisodeID { get; set; }

    /// <summary>
    /// Related TMDB Season entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbSeasonID { get; set; }

    /// <summary>
    /// Related TMDB Show entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbShowID { get; set; }

    /// <summary>
    /// Related TMDB Collection entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbCollectionID { get; set; }

    /// <summary>
    /// Related TMDB Network entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbNetworkID { get; set; }

    /// <summary>
    /// Related TMDB Company entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbCompanyID { get; set; }

    /// <summary>
    /// Related TMDB Person entity id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbPersonID { get; set; }

    /// <summary>
    /// Foreign type. Determines if the data is for movies or tv shows, and if
    /// the tmdb id is for a show or movie.
    /// </summary>
    public ForeignEntityType ForeignType { get; set; }

    /// <inheritdoc/>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string? RemoteURL
        => string.IsNullOrEmpty(RemoteFileName) || string.IsNullOrEmpty(TmdbMetadataService.ImageServerUrl) ? null : $"{TmdbMetadataService.ImageServerUrl}original{RemoteFileName}";

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string? RelativePath
        => string.IsNullOrEmpty(RemoteFileName) ? null : Path.Join("TMDB", ImageType.ToString(), RemoteFileName);

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

    public TMDB_Image(string filePath, ImageEntityType type) : base(DataSourceEnum.TMDB, type, 0)
    {
        RemoteFileName = filePath?.Trim() ?? string.Empty;
        if (RemoteFileName.EndsWith(".svg"))
            RemoteFileName = RemoteFileName[..^4] + ".png";
        IsEnabled = true;
    }

    #endregion

    #region Methods

    public bool Populate(ImageData data, ForeignEntityType foreignType, int foreignId)
    {
        var updated = Populate(data);
        updated |= Populate(foreignType, foreignId);
        return updated;
    }

    public bool Populate(ForeignEntityType foreignType, int foreignId)
    {
        var updated = false;
        switch (foreignType)
        {
            case ForeignEntityType.Movie:
                if (TmdbMovieID != foreignId)
                {
                    TmdbMovieID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Episode:
                if (TmdbEpisodeID != foreignId)
                {
                    TmdbEpisodeID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Season:
                if (TmdbSeasonID != foreignId)
                {
                    TmdbSeasonID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Show:
                if (TmdbShowID != foreignId)
                {
                    TmdbShowID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Collection:
                if (TmdbCollectionID != foreignId)
                {
                    TmdbCollectionID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Network:
                if (TmdbNetworkID != foreignId)
                {
                    TmdbNetworkID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Company:
                if (TmdbCompanyID != foreignId)
                {
                    TmdbCompanyID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
            case ForeignEntityType.Person:
                if (TmdbPersonID != foreignId)
                {
                    TmdbPersonID = foreignId;
                    updated = true;
                }
                if (!ForeignType.HasFlag(foreignType))
                {
                    ForeignType |= foreignType;
                    updated = true;
                }
                break;
        }
        return updated;
    }

    private bool Populate(ImageData data)
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

    public int? GetForeignID(ForeignEntityType foreignType)
        => foreignType switch
        {
            ForeignEntityType.Movie => TmdbMovieID,
            ForeignEntityType.Episode => TmdbEpisodeID,
            ForeignEntityType.Season => TmdbSeasonID,
            ForeignEntityType.Show => TmdbShowID,
            ForeignEntityType.Collection => TmdbCollectionID,
            ForeignEntityType.Network => TmdbNetworkID,
            ForeignEntityType.Company => TmdbCompanyID,
            ForeignEntityType.Person => TmdbPersonID,
            _ => null,
        };

    public IImageMetadata GetImageMetadata(bool preferred = false)
        => preferred == IsPreferred
            ? this
            : new TMDB_Image(RemoteFileName, ImageType)
            {
                IsEnabled = IsEnabled,
                IsPreferred = preferred,
                LanguageCode = LanguageCode,
                ForeignType = ForeignType,
                Height = Height,
                Width = Width,
                TMDB_ImageID = TMDB_ImageID,
                TmdbCollectionID = TmdbCollectionID,
                TmdbCompanyID = TmdbCompanyID,
                TmdbEpisodeID = TmdbEpisodeID,
                TmdbMovieID = TmdbMovieID,
                TmdbNetworkID = TmdbNetworkID,
                TmdbPersonID = TmdbPersonID,
                TmdbSeasonID = TmdbSeasonID,
                TmdbShowID = TmdbShowID,
                UserRating = UserRating,
                UserVotes = UserVotes,
                _width = Width,
                _height = Height,
            };

    #endregion
}
