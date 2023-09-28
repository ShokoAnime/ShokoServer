using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Server;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Season
{
    #region Properties

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

    public TMDB_Season() { }

    public TMDB_Season(int seasonId)
    {
        TmdbSeasonID = seasonId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public void Populate(TvShow show, TvSeason season, TranslationsContainer translations)
    {
        var translation = translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        TmdbSeasonID = season.Id!.Value;
        TmdbShowID = show.Id;
        EnglishTitle = translation?.Data.Name ?? season.Name;
        EnglishOverview = translation?.Data.Overview ?? season.Overview;
        EpisodeCount = season.Episodes.Count;
        SeasonNumber = season.SeasonNumber;
        LastUpdatedAt = DateTime.Now;
    }

    public TMDB_Title? GetPreferredTitle(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        // Fallback.
        return useFallback ? new(ForeignEntityType.Season, TmdbSeasonID, EnglishTitle, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Title> GetAllTitles()
    {
        // TODO: Implement this logic once the repositories are added.

        return new List<TMDB_Title>();
    }

    public TMDB_Overview? GetPreferredOverview(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Season, TmdbSeasonID, EnglishOverview, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllOverviews()
    {
        // TODO: Implement this logic once the repositories are added.

        return new List<TMDB_Overview>();
    }

    #endregion
}
