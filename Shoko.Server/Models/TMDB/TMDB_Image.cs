using System.IO;
using System.Net.Http;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Image : IImageMetadata
{
    #region Properties

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
    public ImageEntityType ImageType { get; set; }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }

    /// <inheritdoc/>
    public bool IsLocked
        => false;

    /// <inheritdoc/>
    public bool IsAvailable
        => !string.IsNullOrEmpty(LocalPath) || !string.IsNullOrEmpty(RemoteURL);

    /// <inheritdoc/>
    public double AspectRatio
        => Width / Height;

    /// <inheritdoc/>
    public int Width { get; set; }

    /// <inheritdoc/>
    public int Height { get; set; }

    /// <inheritdoc/>
    public TitleLanguage Language { get; set; }

    /// <inheritdoc/>
    public string? LanguageCode
    {
        get => Language == TitleLanguage.None ? null : Language.GetString();
        set => Language = string.IsNullOrEmpty(value) ? TitleLanguage.None : value.GetTitleLanguage();
    }

    /// <inheritdoc/>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? RemoteURL
        => string.IsNullOrEmpty(RemoteFileName) || string.IsNullOrEmpty(TMDBHelper.ImageServerUrl) ? null : $"{TMDBHelper.ImageServerUrl}/original/{RemoteFileName}";

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string? RelativePath
        => string.IsNullOrEmpty(RemoteFileName) ? null : Path.Join("TMDB", ImageType.ToString(), RemoteFileName);

    /// <inheritdoc/>
    public string? LocalPath
        => ImageUtils.ResolvePath(RelativePath);

    /// <summary>
    /// Average user rating across all user votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public double UserRating { get; set; }

    /// <summary>
    /// User votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public int UserVotes { get; set; }

    #endregion

    #region Constructors

    public TMDB_Image() { }

    public TMDB_Image(string filePath, ImageEntityType type)
    {
        RemoteFileName = filePath?.Trim() ?? string.Empty;
        if (RemoteFileName.EndsWith(".svg"))
            RemoteFileName = RemoteFileName[..^4] + ".png";
        ImageType = type;
    }

    #endregion

    #region Methods

    public void Populate(ImageData data, ForeignEntityType foreignType, int foreignId)
    {
        Populate(data);
        Populate(foreignType, foreignId);
    }

    public void Populate(ForeignEntityType foreignType, int foreignId)
    {
        switch (foreignType)
        {
            case ForeignEntityType.Movie:
                TmdbMovieID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Episode:
                TmdbEpisodeID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Season:
                TmdbSeasonID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Show:
                TmdbShowID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Collection:
                TmdbCollectionID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Network:
                TmdbNetworkID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Company:
                TmdbCompanyID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Person:
                TmdbPersonID = foreignId;
                ForeignType |= foreignType;
                break;
        }
    }

    private void Populate(ImageData data)
    {
        Width = data.Width;
        Height = data.Height;
        LanguageCode = string.IsNullOrEmpty(data.Iso_639_1) ? null : data.Iso_639_1;
        UserRating = data.VoteAverage;
        UserVotes = data.VoteCount;
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

    public Stream? GetStream()
    {
        var localPath = LocalPath;
        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            return new FileStream(localPath, FileMode.Open, FileAccess.Read);

        var remoteURL = RemoteURL;
        if (!string.IsNullOrEmpty(remoteURL))
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/v4");
                return client.GetStreamAsync(remoteURL).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { }
        }

        return null;
    }

    #endregion
}
