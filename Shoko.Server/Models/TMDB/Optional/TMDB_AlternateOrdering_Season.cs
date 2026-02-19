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
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Season : TMDB_Base<string>, ITmdbSeason
{
    #region Properties

    public override string Id => TmdbEpisodeGroupID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_SeasonID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// Episode Group Season name.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Number of episodes within the alternate ordering season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of episodes within the season that are hidden.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Indicates the alternate ordering season is locked.
    /// </summary>
    /// <remarks>
    /// Exactly what this 'locked' status indicates is yet to be determined.
    /// </remarks>
    public bool IsLocked { get; set; } = true;

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

    public TMDB_AlternateOrdering_Season() { }

    public TMDB_AlternateOrdering_Season(string episodeGroupId)
    {
        TmdbEpisodeGroupID = episodeGroupId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(TvGroup episodeGroup, string collectionId, int showId, int seasonNumber)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbEpisodeGroupCollectionID, collectionId, v => TmdbEpisodeGroupCollectionID = v),
            UpdateProperty(EnglishTitle, episodeGroup.Name!, v => EnglishTitle = v),
            UpdateProperty(SeasonNumber, seasonNumber, v => SeasonNumber = v),
            UpdateProperty(IsLocked, episodeGroup.Locked, v => IsLocked = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get all cast members that have worked on this season.
    /// </summary>
    /// <returns>All cast members that have worked on this season.</returns>
    public IReadOnlyList<TMDB_Season_Cast> Cast =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(episode => episode.TmdbEpisode?.Cast ?? [])
            .WhereNotNull()
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
        TmdbAlternateOrderingEpisodes
            .SelectMany(episode => episode.TmdbEpisode?.Crew ?? [])
            .WhereNotNull()
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
        => TmdbAlternateOrderingEpisodes.Select(e => e.TmdbEpisode?.AiredAt).WhereNotNullOrDefault().Distinct().ToList() is { Count: > 0 } airsAt
                ? [.. airsAt.Min().GetYearlySeasons(airsAt.Max())]
                : [];

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public TMDB_AlternateOrdering? TmdbAlternateOrdering =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(TmdbEpisodeGroupCollectionID);

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisodes =>
        RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbEpisodeGroupID(TmdbEpisodeGroupID);

    #endregion

    #region IMetadata Implementation

    string IMetadata<string>.ID => TmdbEpisodeGroupID.ToString();

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => EnglishTitle;

    ITitle IWithTitles.DefaultTitle => new TitleStub()
    {
        Language = TitleLanguage.EnglishAmerican,
        CountryCode = "US",
        LanguageCode = "en",
        Value = EnglishTitle,
        Source = DataSource.TMDB,
    };

    ITitle? IWithTitles.PreferredTitle => Utils.SettingsProvider.GetSettings().Language.SeriesTitleLanguageOrder.Contains("en-US")
        ? new TitleStub()
        {
            Language = TitleLanguage.EnglishAmerican,
            CountryCode = "US",
            LanguageCode = "en",
            Value = EnglishTitle,
            Source = DataSource.TMDB,
        }
        : null;

    IReadOnlyList<ITitle> IWithTitles.Titles =>
    [
        new TitleStub()
        {
            Language = TitleLanguage.EnglishAmerican,
            CountryCode = "US",
            LanguageCode = "en",
            Value = EnglishTitle,
            Source = DataSource.TMDB,
        },
    ];

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => null;

    IText? IWithDescriptions.PreferredDescription => null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [];

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

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType) => [];

    #endregion

    #region ISeason Implementation

    int ISeason.SeriesID => TmdbShowID;

    IImage? ISeason.DefaultPoster => null;

    ISeries? ISeason.Series => TmdbShow;

    IReadOnlyList<IEpisode> ISeason.Episodes => TmdbAlternateOrderingEpisodes;

    #endregion

    #region ITmdbSeason Implementation

    string ITmdbSeason.OrderingID => TmdbEpisodeGroupCollectionID;

    ITmdbShow? ITmdbSeason.Series => TmdbShow;

    ITmdbShowOrderingInformation? ITmdbSeason.CurrentShowOrdering => TmdbAlternateOrdering;

    IReadOnlyList<ITmdbEpisode> ITmdbSeason.Episodes => TmdbAlternateOrderingEpisodes;

    IReadOnlyList<ITmdbSeasonCrossReference> ITmdbSeason.TmdbSeasonCrossReferences =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(e => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(e.TmdbEpisodeID))
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => new CrossRef_AniDB_TMDB_Season(xref.AnidbAnimeID, TmdbEpisodeGroupID, TmdbShowID, SeasonNumber))
            .ToList();

    IReadOnlyList<ITmdbEpisodeCrossReference> ITmdbSeason.TmdbEpisodeCrossReferences =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(e => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(e.TmdbEpisodeID))
            .ToList();

    #endregion
}
