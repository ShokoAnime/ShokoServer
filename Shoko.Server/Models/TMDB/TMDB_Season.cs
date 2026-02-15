using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Season Database Model.
/// </summary>
public class TMDB_Season : TMDB_Base<int>, IEntityMetadata, IMetadata<int>, ITmdbSeason
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id
    /// </summary>
    public override int Id => TmdbSeasonID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_SeasonID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// The default poster path. Used to determine the default poster for the show.
    /// </summary>
    public string PosterPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the season, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of episodes within the season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of episodes within the season that are hidden.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor for NHibernate to work correctly while hydrating the rows
    /// from the database.
    /// </summary>
    public TMDB_Season() { }

    /// <summary>
    /// Constructor to create a new season in the provider.
    /// </summary>
    /// <param name="seasonId">The TMDB Season id.</param>
    public TMDB_Season(int seasonId)
    {
        TmdbSeasonID = seasonId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="show">The raw TMDB Tv Show object.</param>
    /// <param name="season">The raw TMDB Tv Season object.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(TvShow show, TvSeason season)
    {
        var translation = season.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(PosterPath, season.PosterPath, v => PosterPath = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data.Name) ? translation.Data.Name : season.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : season.Overview, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, season.SeasonNumber, v => SeasonNumber = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all season titles if they're
    /// already cached from a previous call to <seealso cref="GetAllTitles"/>.
    /// </param>
    /// <returns>The preferred season title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true, bool force = false)
    {
        var titles = GetAllTitles(force);

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new(ForeignEntityType.Season, TmdbSeasonID, EnglishTitle, "en", "US");

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Season, TmdbSeasonID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the season, so we won't have to hit
    /// the database twice to get all titles _and_ the preferred title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? _allTitles = null;

    /// <summary>
    /// Get all titles for the season.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all season titles if they're
    /// already cached from a previous call. </param>
    /// <returns>All titles for the season.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? _allTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Season, TmdbSeasonID)
        : _allTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Season, TmdbSeasonID);

    public TMDB_Overview? GetPreferredOverview(bool useFallback = true, bool force = false)
    {
        var overviews = GetAllOverviews(force);

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Season, TmdbSeasonID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the season, so we won't have to
    /// hit the database twice to get all overviews _and_ the preferred
    /// overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allOverviews = null;

    /// <summary>
    /// Get all overviews for the season.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all season overviews if they're
    /// already cached from a previous call.</param>
    /// <returns>All overviews for the season.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? _allOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Season, TmdbSeasonID)
        : _allOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Season, TmdbSeasonID);

    public TMDB_Image? DefaultPoster => RepoFactory.TMDB_Image.GetByRemoteFileName(PosterPath)?.GetImageMetadata(true, ImageEntityType.Poster);

    /// <summary>
    /// Get all images for the season, or all images for the given
    /// <paramref name="entityType"/> provided for the season.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the season.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbSeasonIDAndType(TmdbSeasonID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbSeasonID(TmdbSeasonID);

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    public IReadOnlyList<TMDB_Season_Cast> Cast =>
        RepoFactory.TMDB_Episode_Cast.GetByTmdbSeasonID(TmdbSeasonID)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Cast()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    IsGuestRole = firstEpisode.IsGuestRole,
                    CharacterName = firstEpisode.CharacterName,
                    Ordering = firstEpisode.Ordering,
                    EpisodeCount = episodes.Count,
                };
            })
            .OrderBy(crew => crew.Ordering)
            .OrderBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all crew members that have worked on this season.
    /// </summary>
    /// <returns>All crew members that have worked on this season.</returns>
    public IReadOnlyList<TMDB_Season_Crew> Crew =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbSeasonID(TmdbSeasonID)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                return new TMDB_Season_Crew()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    TmdbSeasonID = firstEpisode.TmdbSeasonID,
                    Department = firstEpisode.Department,
                    Job = firstEpisode.Job,
                    EpisodeCount = episodes.Count,
                };
            })
            .OrderBy(crew => crew.Department)
            .OrderBy(crew => crew.Job)
            .OrderBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all yearly seasons the show was released in.
    /// </summary>
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => TmdbEpisodes.Select(e => e?.AiredAt).WhereNotNullOrDefault().Distinct().ToList() is { Count: > 0 } airsAt
                ? [.. airsAt.Min().GetYearlySeasons(airsAt.Max())]
                : [];

    /// <summary>
    /// Get the TMDB show associated with the season, or null if the show have
    /// been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB show, or null.</returns>
    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all local TMDB episodes associated with the season, or an empty list
    /// if the season have been purged from the local database for whatever
    /// reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        RepoFactory.TMDB_Episode.GetByTmdbSeasonID(TmdbSeasonID);

    #endregion

    #region IEntityMetadata Implementation

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Season;

    DataSource IEntityMetadata.DataSource => DataSource.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => TmdbSeasonID;

    string IMetadata<string>.ID => TmdbSeasonID.ToString();

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => GetPreferredTitle()?.Value ?? EnglishTitle;

    ITitle IWithTitles.DefaultTitle => new TitleStub()
    {
        Language = TitleLanguage.EnglishAmerican,
        CountryCode = "US",
        LanguageCode = "en",
        Value = EnglishTitle,
        Source = DataSource.TMDB,
    };

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => new TextStub()
    {
        Language = TitleLanguage.EnglishAmerican,
        CountryCode = "US",
        LanguageCode = "en",
        Value = EnglishOverview,
        Source = DataSource.TMDB,
    };

    IText? IWithDescriptions.PreferredDescription => GetPreferredOverview();

    IReadOnlyList<IText> IWithDescriptions.Descriptions => GetAllOverviews();

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType) => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType) => GetImages(entityType);

    #endregion

    #region ISeason Implementation

    int ISeason.SeriesID => TmdbShowID;

    IImage? ISeason.DefaultPoster => DefaultPoster;

    ISeries? ISeason.Series => TmdbShow;

    IReadOnlyList<IEpisode> ISeason.Episodes => TmdbEpisodes;

    #endregion

    #region ITmdbSeason Implementation

    string ITmdbSeason.OrderingID => TmdbShowID.ToString();

    ITmdbShow? ITmdbSeason.Series => TmdbShow;

    ITmdbShowOrderingInformation? ITmdbSeason.CurrentShowOrdering => TmdbShow;

    IReadOnlyList<ITmdbEpisode> ITmdbSeason.Episodes => TmdbEpisodes;

    IReadOnlyList<ITmdbSeasonCrossReference> ITmdbSeason.TmdbSeasonCrossReferences =>
        TmdbEpisodes
            .SelectMany(e => e.CrossReferences)
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    IReadOnlyList<ITmdbEpisodeCrossReference> ITmdbSeason.TmdbEpisodeCrossReferences =>
        TmdbEpisodes
            .SelectMany(e => e.CrossReferences)
            .ToList();

    #endregion
}
