using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using TMDbLib.Objects.TvShows;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Season : TMDB_Base<string>, ITmdbSeason, IInlineTextSource
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
    ///   The season's name on its row, as its default title.
    /// </summary>
    /// <returns>The title, which is empty when the season has no name.</returns>
    public ITitle GetDefaultTitle()
        => TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   season.
    /// </summary>
    /// <returns>The title, or <c>null</c> when none is picked or in a preferred language.</returns>
    public ITitle? GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this);

    /// <summary>
    ///   The season's titles: its name, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

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
                return new TMDB_Season_Cast
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
                return new TMDB_Season_Crew
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

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Season, TmdbEpisodeGroupID);

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => TmdbInlineText.Title(EnglishTitle);

    IText? IInlineTextSource.InlineOverview => null;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => GetPreferredTitle()?.Value ?? EnglishTitle;

    ITitle IWithTitles.DefaultTitle => GetDefaultTitle();

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => null;

    IText? IWithOverviews.PreferredOverview => null;

    IReadOnlyList<IText> IWithOverviews.Overviews => [];

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

    #region ISeason Implementation

    MetadataGuid? ISeason.OrderingID => new(MetadataSource.TMDB, MetadataEntityType.Ordering, TmdbEpisodeGroupCollectionID);

    ISeries? ISeason.Series => TmdbShow;

    IReadOnlyList<IEpisode> ISeason.Episodes => TmdbAlternateOrderingEpisodes;

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(e => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(e.TmdbEpisodeID))
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => new CrossRef_AniDB_TMDB_Season(xref.AnidbAnimeID, TmdbEpisodeGroupID, TmdbShowID, SeasonNumber))
            .ToList();

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences =>
        TmdbAlternateOrderingEpisodes
            .SelectMany(e => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(e.TmdbEpisodeID))
            .ToList();

    // A film sits in no season, so nothing links one to a TMDB season.
    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences => [];

    #endregion

    #region ITmdbSeason Implementation

    string ITmdbSeason.TmdbOrderingID => TmdbEpisodeGroupCollectionID;

    ITmdbShow? ITmdbSeason.Series => TmdbShow;

    ITmdbShowOrderingInformation? ITmdbSeason.CurrentShowOrdering => TmdbAlternateOrdering;

    IReadOnlyList<ITmdbEpisode> ITmdbSeason.Episodes => TmdbAlternateOrderingEpisodes;
    #endregion
}
