using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;
using TMDbLib.Objects.TvShows;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Season Database Model.
/// </summary>
public class TMDB_Season : TMDB_Base<int>, IEntityMetadata, ITmdbSeason, IInlineTextSource
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
    ///   Whether TMDB lists <see cref="EnglishTitle"/> among the season's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishTitleListed { get; set; }

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishOverview"/> among the season's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

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
        var translation = season.Translations!.Translations!.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(PosterPath, season.PosterPath!, v => PosterPath = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data?.Name) ? translation.Data.Name : season.Name!, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data?.Overview) ? translation.Data.Overview : season.Overview!, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, season.SeasonNumber, v => SeasonNumber = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   season.
    /// </summary>
    /// <returns>The title, or the English one when none is in a preferred language.</returns>
    public ITitle GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this) ?? TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The season's titles: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   season.
    /// </summary>
    /// <returns>The overview, or the English one when none is in a preferred language.</returns>
    public IText GetPreferredOverview()
        => TextAccess.Manager.PreferredOverviewFor(this) ?? TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    /// <summary>
    ///   The season's overviews: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<IText> GetAllOverviews()
        => TextAccess.Manager.ListOverviews(this);

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
        RepoFactory.TMDB_Episode_Crew.GetByTmdbSeasonID(TmdbSeasonID)
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

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Season;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID.ToString());

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => TmdbInlineText.Title(EnglishTitle);

    IText? IInlineTextSource.InlineOverview => TmdbInlineText.Overview(EnglishOverview);

    InlineTextPlacement IInlineTextSource.InlineTitlePlacement => TmdbInlineText.Placement(EnglishTitleListed);

    InlineTextPlacement IInlineTextSource.InlineOverviewPlacement => TmdbInlineText.Placement(EnglishOverviewListed);

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => GetPreferredTitle().Value;

    ITitle IWithTitles.DefaultTitle => TmdbInlineText.TitleOrEmpty(EnglishTitle);

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    IText? IWithOverviews.PreferredOverview => GetPreferredOverview();

    IReadOnlyList<IText> IWithOverviews.Overviews => GetAllOverviews();

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

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(PosterPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, PosterPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region ISeason Implementation

    ISeries? ISeason.Series => TmdbShow;

    IReadOnlyList<IEpisode> ISeason.Episodes => TmdbEpisodes;

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences =>
        TmdbEpisodes
            .SelectMany(e => e.CrossReferences)
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences =>
        TmdbEpisodes
            .SelectMany(e => e.CrossReferences)
            .ToList();

    // A film sits in no season, so nothing links one to a TMDB season.
    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences => [];

    #endregion

    #region ITmdbSeason Implementation

    string ITmdbSeason.TmdbOrderingID => TmdbShowID.ToString();

    ITmdbShow? ITmdbSeason.Series => TmdbShow;

    ITmdbShowOrderingInformation? ITmdbSeason.CurrentShowOrdering => TmdbShow is { } show ? new TMDB_Show_DefaultOrdering(show, OrderingLookup.Service) : null;

    IReadOnlyList<ITmdbEpisode> ITmdbSeason.Episodes => TmdbEpisodes;
    #endregion
}
