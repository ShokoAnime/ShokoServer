using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

public class AnimeEpisode : IShokoEpisode, IEquatable<AnimeEpisode>
{
    #region DB Columns

    /// <summary>
    /// Local <see cref="AnimeEpisode"/> id.
    /// </summary>
    public int AnimeEpisodeID { get; set; }

    /// <summary>
    /// Local <see cref="Shoko.AnimeSeries"/> id.
    /// </summary>
    public int AnimeSeriesID { get; set; }

    /// <summary>
    /// The universally unique anidb episode id.
    /// </summary>
    /// <remarks>
    /// Also see <seealso cref="AniDB.AniDB_Episode"/> for a local representation
    /// of the anidb episode data.
    /// </remarks>
    public int AniDB_EpisodeID { get; set; }

    /// <summary>
    /// Timestamp for when the entry was first created.
    /// </summary>
    public DateTime DateTimeCreated { get; set; }

    /// <summary>
    /// Timestamp for when the entry was last updated.
    /// </summary>
    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    /// Hidden episodes will not show up in the UI unless explicitly
    /// requested, and will also not count towards the unwatched count for
    /// the series.
    /// </summary>
    public bool IsHidden { get; set; }

    #endregion

    public EpisodeType EpisodeType => AniDB_Episode?.EpisodeType ?? EpisodeType.Episode;

    #region Titles

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    /// <summary>
    ///   The name a user gave the episode, stored as its <c>user</c> title.
    /// </summary>
    public ITitle? CustomTitle => TextManager.CustomTitleOf(((IMetadata)this).ID);

    /// <summary>
    ///   The episode's default title: its AniDB episode's, else a stand-in
    ///   naming the AniDB episode.
    /// </summary>
    public ITitle DefaultTitle
        => AniDB_Episode?.DefaultTitle ?? new TitleStub
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = $"<AniDB Episode {AniDB_EpisodeID}>",
            Source = MetadataSource.Shoko,
        };

    public ITitle? PreferredTitle => TextManager.PreferredTitleOf(this);

    public IReadOnlyList<ITitle> Titles => TextManager.TitlesOf(this);

    public IText? PreferredOverview => TextManager.PreferredDescriptionOf(this);

    private static MetadataTextManager TextManager
        => TextAccess.Manager;

    #endregion

    #region Shoko

    public AnimeEpisode_User? GetUserRecord(int userID)
        => userID <= 0 ? null : RepoFactory.AnimeEpisode_User.GetByUserAndEpisodeID(userID, AnimeEpisodeID);

    /// <summary>
    /// Gets the AnimeSeries this episode belongs to
    /// </summary>
    public AnimeSeries? AnimeSeries
        => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    public IReadOnlyList<VideoLocal> VideoLocals
        => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    /// <summary>
    /// Determines whether an aired episode with no local files should be counted as missing,
    /// based on the cached AniDB group release statuses for its anime.
    /// </summary>
    /// <param name="groupStatuses">Group statuses already scoped to this episode's anime. An empty list is treated as missing.</param>
    /// <returns><see langword="true"/> if the episode is considered missing; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This predicate is intended for <see cref="EpisodeType.Episode"/> episodes only; callers must
    /// pre-filter by episode type AND file presence before calling it.
    /// </remarks>
    public bool IsMissingEpisode(IReadOnlyList<AniDB_GroupStatus> groupStatuses)
    {
        if (IsHidden) return false;
        var anidb = AniDB_Episode;
        if (anidb == null) return false;
        if (!anidb.HasAired) return false;

        return groupStatuses.Count == 0 || groupStatuses.Any(gs =>
            gs.CompletionState is (int)GroupCompletionStatus.Complete or (int)GroupCompletionStatus.Finished
            || gs.LastEpisodeNumber >= anidb.EpisodeNumber);
    }

    #endregion

    #region AniDB

    public AniDB_Episode? AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public AniDB_Anime? AniDB_Anime => AniDB_Episode?.AniDB_Anime;

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies =>
        TmdbMovieCrossReferences
            .Select(xref => xref.TmdbMovie)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbEpisode)
            .WhereNotNull()
            .ToList();

    #endregion

    public bool Equals(AnimeEpisode? other)
        => other is not null &&
            AnimeEpisodeID == other.AnimeEpisodeID &&
            AnimeSeriesID == other.AnimeSeriesID &&
            AniDB_EpisodeID == other.AniDB_EpisodeID &&
            DateTimeUpdated == other.DateTimeUpdated &&
            DateTimeCreated == other.DateTimeCreated;

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || (obj is AnimeEpisode ep && Equals(ep)));

    public override int GetHashCode()
        => HashCode.Combine(AnimeEpisodeID, AnimeSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated);

    #region IMetadata Implementation

    /// <summary>
    ///   The ID last handed out, with the row ID it was made from. Kept since
    ///   it is asked for on every read of a title, and made again only when
    ///   the row's ID changes on insert.
    /// </summary>
    private Tuple<int, MetadataGuid>? _metadataID;

    /// <summary>
    ///   The texts the text manager worked out for this entry, kept here so
    ///   reading them back skips looking them up.
    /// </summary>
    internal object? TextMemo;

    MetadataGuid IMetadata.ID
    {
        get
        {
            var local = AnimeEpisodeID;
            if (_metadataID is { } cached && cached.Item1 == local)
                return cached.Item2;

            var id = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, local.ToString());
            _metadataID = new(local, id);
            return id;
        }
    }

    int IShokoEpisode.LocalID => AnimeEpisodeID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithOverviews.DefaultOverview => TextManager.DefaultOverviewOf(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => TextManager.DescriptionsOf(this);

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => [
        .. LinkedEpisodes.SelectMany(episode => episode.Cast),
        .. LinkedMovies.SelectMany(movie => movie.Cast),
    ];

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => [
        .. LinkedEpisodes.SelectMany(episode => episode.Crew),
        .. LinkedMovies.SelectMany(movie => movie.Crew),
    ];

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources => [
        .. LinkedEpisodes.SelectMany(episode => episode.Resources),
        .. LinkedMovies.SelectMany(movie => movie.Resources),
        // The resolvers answer about this entry itself, rather than about
        // anything it is linked to, so they are asked separately.
        .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this),
    ];

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => [new(MetadataSource.AniDB, MetadataEntityType.Episode, AniDB_EpisodeID.ToString())];

    #endregion

    #region IEpisode Implementation

    IReadOnlyList<IEpisodeOrderingInformation> IEpisode.Orderings => OrderingLookup.For(this);

    IEpisodeOrderingInformation? IEpisode.PreferredOrdering => OrderingLookup.PreferredFor(this);

    IReadOnlyList<IMetadataEpisodeCrossReference> IEpisode.MetadataEpisodeCrossReferences => ((IShokoEpisode)this).GetMetadataEpisodeCrossReferences();

    IReadOnlyList<IMetadataSeriesCrossReference> IEpisode.MetadataSeriesCrossReferences => ((IShokoEpisode)this).GetMetadataSeriesCrossReferences();

    IReadOnlyList<IMetadataMovieCrossReference> IEpisode.MetadataMovieCrossReferences => ((IShokoEpisode)this).GetMetadataMovieCrossReferences();

    IReadOnlyList<IMetadataSeriesCrossReference> IShokoEpisode.GetMetadataSeriesCrossReferences(MetadataSource? source)
        => AnimeSeries is not { AniDB_ID: var anidbAnimeID }
            ? []
            : ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeriesCrossReferences(anidbAnimeID, source) ?? [];

    IReadOnlyList<IMetadataEpisodeCrossReference> IShokoEpisode.GetMetadataEpisodeCrossReferences(MetadataSource? source)
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetEpisodeCrossReferences(AniDB_EpisodeID, source) ?? [];

    IReadOnlyList<IMetadataMovieCrossReference> IShokoEpisode.GetMetadataMovieCrossReferences(MetadataSource? source)
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetMovieCrossReferences(AniDB_EpisodeID, source) ?? [];

    int IShokoEpisode.ShokoSeriesID => AnimeSeriesID;

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => [AnimeEpisodeID];

    EpisodeType IEpisode.Type => EpisodeType;

    int IEpisode.EpisodeNumber => AniDB_Episode?.EpisodeNumber ?? 1;

    int? IEpisode.SeasonNumber => EpisodeType switch { EpisodeType.Episode => 1, EpisodeType.Special => 0, _ => null };

    MetadataGuid? IEpisode.SeasonID => ((IEpisode)this).SeasonNumber is { } seasonNumber
        ? new(MetadataSource.Shoko, MetadataEntityType.Season, AnimeSeason.GetID(AnimeSeriesID, EpisodeType, seasonNumber))
        : null;

    double IEpisode.Rating => AniDB_Episode?.RatingDouble ?? 0;

    int IEpisode.RatingVotes => AniDB_Episode?.VotesInt ?? 0;

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(AniDB_Episode?.LengthSeconds ?? 0);

    // The day is the UTC calendar day of the precise air time, so the two never disagree. When a
    // provider only knows the date, the date is the answer for both.
    DateOnly? IEpisode.AirDate
        => ((IEpisode)this).AirDateWithTime is { } airDateWithTime ? DateOnly.FromDateTime(airDateWithTime) : null;

    DateTime? IEpisode.AirDateWithTime
    {
        get
        {
            if (AniDB_Episode is { } anidbEpisode && anidbEpisode.GetAirDateAsDate() is { } airDate)
                return airDate;

            foreach (var xref in TmdbEpisodeCrossReferences)
            {
                if (xref.TmdbEpisode?.AiredAt is { } tmdbAirDate)
                    return tmdbAirDate.ToDateTime().Date;
            }

            return null;
        }
    }

    ISeries IEpisode.Series => ((IShokoEpisode)this).Series;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => [this];

    IReadOnlyList<IVideoCrossReference> IEpisode.VideoCrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    IReadOnlyList<IVideo> IEpisode.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    #endregion

    #region IShokoEpisode Implementation

    int IShokoEpisode.AnidbEpisodeID => AniDB_EpisodeID;

    IShokoSeries IShokoEpisode.Series => AnimeSeries ??
        throw new NullReferenceException($"Unable to find Shoko Series {AnimeSeriesID} for AnimeEpisode {AnimeEpisodeID}");

    IAnidbEpisode IShokoEpisode.AnidbEpisode => AniDB_Episode ??
        throw new NullReferenceException($"Unable to find AniDB Episode {AniDB_EpisodeID} for AnimeEpisode {AnimeEpisodeID}");

    /// <summary>
    ///   Every episode this one is linked to: AniDB, then whatever the
    ///   cross-reference store holds, whether or not its source is enabled.
    ///   What the aggregate getters read from, so a newly linked source reaches
    ///   all of them at once.
    /// </summary>
    public IReadOnlyList<IEpisode> LinkedEpisodes
    {
        get
        {
            var episodeList = new List<IEpisode>();

            var anidbEpisode = AniDB_Episode;
            if (anidbEpisode is not null)
                episodeList.Add(anidbEpisode);

            if (ISystemService.StaticServices.GetRequiredService<IMetadataService>() is MetadataService metadataService)
                episodeList.AddRange(metadataService.GetLinkedEpisodes(AniDB_EpisodeID));

            return episodeList;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IMovie> LinkedMovies
    {
        get
        {
            return ISystemService.StaticServices.GetRequiredService<IMetadataService>() is MetadataService metadataService
                ? metadataService.GetLinkedMoviesForEpisode(AniDB_EpisodeID)
                : [];
        }
    }

    IReadOnlyList<IEpisode> IShokoEpisode.LinkedEpisodes => LinkedEpisodes;

    IReadOnlyList<IMovie> IShokoEpisode.LinkedMovies => LinkedMovies;

    IEpisodeUserData IShokoEpisode.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.LocalID is 0 || RepoFactory.JMMUser.GetByID(user.LocalID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = GetUserRecord(user.LocalID)
            ?? new() { JMMUserID = user.LocalID, AnimeEpisodeID = AnimeEpisodeID, AnimeSeriesID = AnimeSeriesID };
        if (userData.AnimeEpisode_UserID is 0)
            RepoFactory.AnimeEpisode_User.Save(userData);
        return userData;
    }

    #endregion
}
