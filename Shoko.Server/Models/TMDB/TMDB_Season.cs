using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Season Database Model.
/// </summary>
public class TMDB_Season : TMDB_Base<int>, IEntityMetadata
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
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last syncronized with the remote.
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
            UpdateProperty(EnglishTitle, translation?.Data.Name ?? season.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, translation?.Data.Overview ?? season.Overview, v => EnglishOverview = v),
            UpdateProperty(EpisodeCount, season.Episodes.Count, v => EpisodeCount = v),
            UpdateProperty(SeasonNumber, season.SeasonNumber, v => SeasonNumber = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preferrence
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
            var title = titles.FirstOrDefault(title => title.Language == preferredLanguage.Language);
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

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            var overview = overviews.FirstOrDefault(overview => overview.Language == preferredLanguage.Language);
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

    /// <summary>
    /// Get all images for the season, or all images for the given
    /// <paramref name="entityType"/> provided for the season.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the epiosde.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbSeasonIDAndType(TmdbSeasonID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbSeasonID(TmdbSeasonID);

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    public IReadOnlyList<TMDB_Season_Cast> GetCast() =>
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
    public IReadOnlyList<TMDB_Season_Crew> GetCrew() =>
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
    /// Get the TMDB show assosiated with the season, or null if the show have
    /// been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB show, or null.</returns>
    public TMDB_Show? GetTmdbShow() =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all local TMDB episodes assosiated with the season, or an empty list
    /// if the season have been purged from the local database for whatever
    /// reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public IReadOnlyList<TMDB_Episode> GetTmdbEpisodes() =>
        RepoFactory.TMDB_Episode.GetByTmdbSeasonID(TmdbSeasonID);

    /// <summary>
    /// Get all AniDB/TMDB cross-references for the season.
    /// </summary>
    /// <returns>The cross-references.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetCrossReferences() =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbSeasonID(TmdbSeasonID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Season;

    DataSourceType IEntityMetadata.DataSource => DataSourceType.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion
}
