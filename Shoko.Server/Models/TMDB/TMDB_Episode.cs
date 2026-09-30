using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Episode Database Model.
/// </summary>
public class TMDB_Episode : TMDB_Base<int>, IEntityMetadata, IEpisode, ITmdbEpisode, IInlineTextSource
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id.
    /// </summary>
    public override int Id => TmdbEpisodeID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Linked TvDB episode ID.
    /// </summary>
    /// <remarks>
    /// Will be <code>null</code> if not linked. Will be <code>0</code> if no
    /// TvDB link is found in TMDB. Otherwise it will be the TvDB episode ID.
    /// </remarks>
    public int? TvdbEpisodeID { get; set; }

    /// <summary>
    /// The default thumbnail path. Used to determine the default thumbnail for the episode.
    /// </summary>
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the episode, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishTitle"/> among the episode's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishTitleListed { get; set; }

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishOverview"/> among the episode's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

    /// <summary>
    /// Indicates that the episode should be hidden from view unless explicitly
    /// requested, and should not be used internally at all. Set through
    /// <see cref="IMetadataOrderingService.SetEpisodeHidden"/>.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Episode number for default ordering.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Episode run-time in minutes.
    /// </summary>
    public int? RuntimeMinutes
    {
        get => Runtime.HasValue ? (int)Math.Floor(Runtime.Value.TotalMinutes) : null;
        set => Runtime = value.HasValue ? TimeSpan.FromMinutes(value.Value) : null;
    }

    /// <summary>
    /// Episode run-time.
    /// </summary>
    public TimeSpan? Runtime { get; set; }

    /// <summary>
    /// Average user rating across all <see cref="UserVotes"/>.
    /// </summary>
    public double UserRating { get; set; }

    /// <summary>
    /// Number of users that cast a vote for a rating of this show.
    /// </summary>
    /// <value></value>
    public int UserVotes { get; set; }

    /// <summary>
    /// When the episode aired, or when it will air in the future if it's known.
    /// </summary>
    public DateOnly? AiredAt { get; set; }

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
    public TMDB_Episode() { }

    /// <summary>
    /// Constructor to create a new episode in the provider.
    /// </summary>
    /// <param name="episodeId">The TMDB episode id.</param>
    public TMDB_Episode(int episodeId)
    {
        TmdbEpisodeID = episodeId;
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
    /// <param name="episode">The raw TMDB Tv Episode object.</param>
    /// <param name="translations">The translation container for the Tv Episode object (fetched separately).</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(TvShow show, TvSeason season, TvSeasonEpisode episode, TranslationsContainer? translations)
    {
        var translation = translations?.Translations!.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value!, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(ThumbnailPath, episode.StillPath!, v => ThumbnailPath = v),
            // If the translations aren't provided and we have an English title, then don't update it.
            UpdateProperty(EnglishTitle, translations is null && !string.IsNullOrEmpty(EnglishTitle) ? EnglishTitle : !string.IsNullOrEmpty(translation?.Data?.Name) ? translation.Data.Name : episode.Name!, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data?.Overview) ? translation.Data.Overview : episode.Overview!, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, episode.SeasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeNumber, episode.EpisodeNumber, v => EpisodeNumber = (int)v),
            UpdateProperty(Runtime, episode.Runtime.HasValue ? TimeSpan.FromMinutes(episode.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, episode.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, episode.VoteCount, v => UserVotes = v),
            UpdateProperty(AiredAt, episode.AirDate?.ToDateOnly(), v => AiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   episode.
    /// </summary>
    /// <returns>The title, or the English one when none is in a preferred language.</returns>
    public ITitle GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this) ?? TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The episode's titles: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

    /// <summary>
    ///   The episode's titles in the preferred episode title languages, or in
    ///   English.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllPreferredTitles()
        => GetAllTitles()
            .WhereInLanguages(Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language).Append(TitleLanguage.English).ToHashSet())
            .ToList();

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   episode.
    /// </summary>
    /// <returns>The overview, or the English one when none is in a preferred language.</returns>
    public IText GetPreferredOverview()
        => TextAccess.Manager.PreferredOverviewFor(this) ?? TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    /// <summary>
    ///   The episode's overviews: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<IText> GetAllOverviews()
        => TextAccess.Manager.ListOverviews(this);

    /// <summary>
    /// Get all cast members that have worked on this episode.
    /// </summary>
    /// <returns>All cast members that have worked on this episode.</returns>
    public IReadOnlyList<TMDB_Episode_Cast> Cast =>
        RepoFactory.TMDB_Episode_Cast.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get all crew members that have worked on this episode.
    /// </summary>
    /// <returns>All crew members that have worked on this episode.</returns>
    public IReadOnlyList<TMDB_Episode_Crew> Crew =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get the TMDB season associated with the episode, or null if the season
    /// have been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB season, or null.</returns>
    public TMDB_Season? TmdbSeason =>
        RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID);

    /// <summary>
    /// Get the TMDB show associated with the episode, or null if the show have
    /// been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB show, or null.</returns>
    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all alternate ordering entries for the episode available from the
    /// local database. You need to have alternate orderings enabled in the
    /// settings file for these to be populated.
    /// </summary>
    /// <returns>All alternate ordering entries for the episode.</returns>
    public IReadOnlyList<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisodes =>
        RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get the alternate ordering entry for the episode with the given
    /// <paramref name="id"/>, or null if no such entry exists.
    /// </summary>
    /// <param name="id">The episode group collection ID of the alternate ordering
    /// entry to retrieve.</param>
    /// <returns>The alternate ordering entry associated with the given ID, or
    /// null if no such entry exists.</returns>
    public TMDB_AlternateOrdering_Episode? GetTmdbAlternateOrderingEpisodeById(string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(id, TmdbEpisodeID);

    /// <summary>
    /// Get all AniDB/TMDB cross-references for the episode.
    /// </summary>
    /// <returns>A read-only list of AniDB/TMDB cross-references for the episode.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> CrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get all file cross-references associated with the episode.
    /// </summary>
    /// <returns>A read-only list of file cross-references associated with the
    /// episode.</returns>
    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences =>
        CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region IEntityMetadata Implementation

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Episode;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => AiredAt;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID.ToString());

    int ITmdbEpisode.TmdbID => TmdbEpisodeID;

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

    public IImageCrossReference? DefaultBackdropImageCrossReference => !string.IsNullOrEmpty(ThumbnailPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, ThumbnailPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Backdrop }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithResources Implementation

    /// <summary>
    ///   External resources/links associated with the episode.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>();
            if (TvdbEpisodeID is > 0)
                list.Add(new()
                {
                    Type = ResourceType.CrossReference,
                    Name = "TheTVDB",
                    Url = $"https://www.thetvdb.com/dereferrer/episode/{TvdbEpisodeID}",
                    ID = TvdbEpisodeID.Value.ToString(),
                });
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => CrossSourceID.For("tvdb", MetadataEntityType.Episode, TvdbEpisodeID) is { } tvdbID ? [tvdbID] : [];

    #endregion

    #region IEpisode Implementation

    IReadOnlyList<IEpisodeOrderingInformation> IEpisode.Orderings => OrderingLookup.For(this);

    IEpisodeOrderingInformation? IEpisode.PreferredOrdering => OrderingLookup.PreferredFor(this);

    IReadOnlyList<IMetadataEpisodeCrossReference> IEpisode.MetadataEpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    IReadOnlyList<IMetadataSeriesCrossReference> IEpisode.MetadataSeriesCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(TmdbShowID);

    // A film claims no TMDB episode, only the AniDB one standing for it.
    IReadOnlyList<IMetadataMovieCrossReference> IEpisode.MetadataMovieCrossReferences => [];

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => CrossReferences
        .Select(xref => xref.AnimeEpisode?.AnimeEpisodeID)
        .WhereNotNull()
        .ToList();

    EpisodeType IEpisode.Type => SeasonNumber == 0 ? EpisodeType.Special : EpisodeType.Episode;

    int IEpisode.EpisodeNumber => EpisodeNumber;

    int? IEpisode.SeasonNumber => SeasonNumber;

    MetadataGuid? IEpisode.SeasonID => TmdbSeasonID is 0 ? null : new(MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID.ToString());

    double IEpisode.Rating => UserRating;

    int IEpisode.RatingVotes => UserVotes;

    TimeSpan IEpisode.Runtime => Runtime ?? TimeSpan.Zero;

    DateOnly? IEpisode.AirDate => AiredAt;

    DateTime? IEpisode.AirDateWithTime => AiredAt?.ToDateTime();

    ISeries? IEpisode.Series => TmdbShow;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => CrossReferences
        .Select(xref => xref.AnimeEpisode)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IVideoCrossReference> IEpisode.VideoCrossReferences => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .ToList();

    IReadOnlyList<IVideo> IEpisode.Videos => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .DistinctBy(video => video.VideoLocalID)
        .ToList();

    #endregion

    #region ITmdbEpisode Implementation

    string ITmdbEpisode.TmdbOrderingID => TmdbShowID.ToString();

    ITmdbShow? ITmdbEpisode.Series => TmdbShow;

    ITmdbSeason? ITmdbEpisode.Season => TmdbSeason;

    ITmdbEpisodeOrderingInformation ITmdbEpisode.Ordering => new TMDB_Episode_DefaultOrdering(this, TmdbShow, OrderingLookup.Service);

    ITmdbShowOrderingInformation? ITmdbEpisode.SeriesOrdering => TmdbShow is { } show ? new TMDB_Show_DefaultOrdering(show, OrderingLookup.Service) : null;

    IReadOnlyList<ITmdbEpisodeOrderingInformation> ITmdbEpisode.TmdbOrderings => [((ITmdbEpisode)this).Ordering, .. TmdbAlternateOrderingEpisodes];
    #endregion
}
