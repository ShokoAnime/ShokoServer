using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

public class AnimeSeries : IShokoSeries
{
    #region DB Columns

    public int AnimeSeriesID { get; set; }

    public int AnimeGroupID { get; set; }

    public int AniDB_ID { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public string? DefaultAudioLanguage { get; set; }

    public string? DefaultSubtitleLanguage { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public DayOfWeek? AirsOn { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int HiddenMissingEpisodeCount { get; set; }

    public int HiddenMissingEpisodeCountGroups { get; set; }

    public int LatestLocalEpisodeNumber { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    ///   The sources told to leave this anime alone, as a JSON array.
    /// </summary>
    /// <remarks>
    ///   A source is written as its <see cref="MetadataSource.Value"/>. Older
    ///   rows hold the source's name, which matches since keys compare
    ///   ignoring case. Kept as text rather than a bitfield so a source added
    ///   later, a plugin's included, has somewhere to go.
    /// </remarks>
    public string? DisabledAutoMatchSources { get; set; }

    /// <summary>
    ///   The ordering chosen for the series, of any source, or <c>null</c>
    ///   for its default one. Set through
    ///   <see cref="IMetadataOrderingService.SetPreferredOrdering"/>.
    /// </summary>
    public MetadataGuid? PreferredOrderingID { get; set; }

    #endregion

    #region Disabled Auto Matching

    /// <summary>
    ///   The sources told to leave this anime alone, read from
    ///   <see cref="DisabledAutoMatchSources"/>.
    /// </summary>
    public IReadOnlySet<string> DisabledAutoMatchKeys
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisabledAutoMatchSources))
                return FrozenSet<string>.Empty;

            try
            {
                return JsonConvert.DeserializeObject<string[]>(DisabledAutoMatchSources) is { } keys
                    ? keys.Where(key => !string.IsNullOrWhiteSpace(key)).ToFrozenSet(StringComparer.InvariantCultureIgnoreCase)
                    : FrozenSet<string>.Empty;
            }
            catch (JsonException)
            {
                // A hand-edited or half-written value should not take the
                // series down with it; nobody said no is the safe reading.
                return FrozenSet<string>.Empty;
            }
        }
        set => DisabledAutoMatchSources = value.Count is 0 ? null : JsonConvert.SerializeObject(value.OrderBy(key => key));
    }

    /// <summary>
    ///   Whether a source has been told to leave this anime alone.
    /// </summary>
    public bool IsAutoLinkingDisabled(MetadataSource source)
        => DisabledAutoMatchKeys.Contains(source.ToString());

    #endregion

    #region Titles & Overviews

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    /// <summary>
    ///   The name a user gave the series, stored as its <c>user</c> title.
    /// </summary>
    public ITitle? CustomTitle => TextManager.CustomTitleOf(((IMetadata)this).ID);

    /// <summary>
    ///   The series' default title: the name a user gave it, else its AniDB
    ///   anime's default title.
    /// </summary>
    public ITitle DefaultTitle => TextManager.DefaultTitleOf(this);

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   series.
    /// </summary>
    public ITitle? PreferredTitle => TextManager.PreferredTitleOf(this);

    /// <summary>
    ///   Every title of the series: the user's, AniDB's, the linked entries'
    ///   and the contributed ones.
    /// </summary>
    public IReadOnlyList<ITitle> Titles => TextManager.TitlesOf(this);

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   series.
    /// </summary>
    public IText? PreferredOverview => TextManager.PreferredDescriptionOf(this);

    private static MetadataTextManager TextManager
        => TextAccess.Manager;

    #endregion

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences => RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    public IReadOnlyList<VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID);

    public IReadOnlyList<AnimeSeason> AnimeSeasons => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Any(e => e.EpisodeType is EpisodeType.Special)
        ? [new AnimeSeason(this, EpisodeType.Episode, 1), new AnimeSeason(this, EpisodeType.Special, 0)]
        : [new AnimeSeason(this, EpisodeType.Episode, 1)];

    public IReadOnlyList<AnimeEpisode> AnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Where(episode => !episode.IsHidden)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public IReadOnlyList<AnimeEpisode> AllAnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public HashSet<ImageEntityType> AvailableImageTypes
        => ((IWithImages)this).GetImages()
            .Select(image => image.Type)
            .ToHashSet();

    public HashSet<ImageEntityType> PreferredImageTypes
        => ((IWithImages)this).GetImages()
            .Where(image => image.IsPreferred)
            .Select(image => image.Type)
            .ToHashSet();

    #region AniDB

    public AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies => TmdbMovieCrossReferences
        .DistinctBy(xref => xref.TmdbMovieID)
        .Select(xref => xref.TmdbMovie)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Show> TmdbShows => TmdbShowCrossReferences
        .Select(xref => xref.TmdbShow)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    #endregion

    #region TMDB (continued)

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => tmdbShowId.HasValue
        ? RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(AniDB_ID, tmdbShowId.Value)
        : RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> TmdbSeasonCrossReferences =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    public IReadOnlyList<TMDB_Season> TmdbSeasons => TmdbSeasonCrossReferences
        .Select(xref => xref.TmdbSeason)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> GetTmdbSeasonCrossReferences(int? tmdbShowId = null) =>
        GetTmdbEpisodeCrossReferences(tmdbShowId)
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .Distinct()
            .ToList();

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

    #endregion

    private PartialDateOnly? _airDate;

    public PartialDateOnly? AirDate
    {
        get
        {
            if (_airDate is not null)
                return _airDate;

            var anime = AniDB_Anime;
            if (anime?.AirDate is { } airDate)
                return _airDate = airDate;

            // This will be slower, but hopefully more accurate
            var ep = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID)
                .Where(a => a.EpisodeType is EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
                .MinBy(a => a.AirDate);
            return _airDate = ep?.GetAirDateAsPartialDateOnly();
        }
    }

    private PartialDateOnly? _endDate;

    public PartialDateOnly? EndDate => _endDate ??= AniDB_Anime?.EndDate;

    public HashSet<int> Years
    {
        get
        {
            if (AniDB_Anime is not { } anime) return [];
            var startYear = anime.BeginYear;
            if (startYear == 0) return [];

            var endYear = anime.EffectiveEndDateForSeasons?.Year ?? DateTime.Today.Year;
            if (endYear < startYear) endYear = startYear;
            if (startYear == endYear) return [startYear];

            return Enumerable.Range(startYear, endYear - startYear + 1).Where(anime.IsInYear).ToHashSet();
        }
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID)!;

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID) ??
                throw new NullReferenceException($"Unable to find parent AnimeGroup {AnimeGroupID} for AnimeSeries {AnimeSeriesID}");

            int parentID;
            while ((parentID = parentGroup.AnimeGroupParentID ?? 0) != 0)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentID) ??
                    throw new NullReferenceException($"Unable to find parent AnimeGroup {parentGroup.AnimeGroupParentID} for AnimeGroup {parentGroup.AnimeGroupID}");
            }

            return parentGroup;
        }
    }

    public List<AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<AnimeGroup>();
            var groupID = AnimeGroupID;
            while (groupID != 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(groupID);
                if (grp != null)
                {
                    grps.Add(grp);
                    groupID = grp.AnimeGroupParentID ?? 0;
                }
                else
                {
                    groupID = 0;
                }
            }

            return grps;
        }
    }

    public override string ToString()
    {
        return $"Series: {AniDB_Anime?.MainTitle} ({AnimeSeriesID})";
        //return string.Empty;
    }

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
            var local = AnimeSeriesID;
            if (_metadataID is { } cached && cached.Item1 == local)
                return cached.Item2;

            var id = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, local.ToString());
            _metadataID = new(local, id);
            return id;
        }
    }

    int IShokoSeries.LocalID => AnimeSeriesID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithOverviews.DefaultOverview => TextManager.DefaultOverviewOf(this);

    IText? IWithOverviews.PreferredOverview => PreferredOverview;

    IReadOnlyList<IText> IWithOverviews.Overviews => TextManager.DescriptionsOf(this);

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => AniDB_Anime?.DefaultPrimaryImageCrossReference;

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => [
        .. LinkedSeries.SelectMany(series => series.Cast),
        .. LinkedMovies.SelectMany(movie => movie.Cast),
    ];

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => [
        .. LinkedSeries.SelectMany(series => series.Crew),
        .. LinkedMovies.SelectMany(movie => movie.Crew),
    ];

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => [
        .. LinkedSeries.SelectMany(series => series.Studios),
        .. LinkedMovies.SelectMany(movie => movie.Studios),
    ];

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => [
        .. LinkedSeries.SelectMany(series => series.ContentRatings),
        .. LinkedMovies.SelectMany(movie => movie.ContentRatings),
    ];

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => [.. AirDate.GetYearlySeasons(AniDB_Anime?.EffectiveEndDateForSeasons)];

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources => [
        .. LinkedSeries.SelectMany(series => series.Resources),
        .. LinkedMovies.SelectMany(movie => movie.Resources),
        // The resolvers answer about this entry itself, rather than about
        // anything it is linked to, so they are asked separately.
        .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this),
    ];

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => [new(MetadataSource.AniDB, MetadataEntityType.Series, AniDB_ID.ToString())];

    #endregion

    #region ISeries Implementation

    IReadOnlyList<INetwork> ISeries.Networks => [.. LinkedSeries.SelectMany(series => series.Networks).DistinctBy(network => network.ID)];

    IReadOnlyList<IOrdering> ISeries.Orderings => OrderingLookup.For(this);

    IOrdering ISeries.PreferredOrdering => OrderingLookup.PreferredFor(this);

    IReadOnlyList<IMetadataSeriesCrossReference> ISeries.MetadataSeriesCrossReferences
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeriesCrossReferences(AniDB_ID, null) ?? [];

    IReadOnlyList<IMetadataSeasonCrossReference> ISeries.MetadataSeasonCrossReferences
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeasonCrossReferences(AniDB_ID, null) ?? [];

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeries.MetadataEpisodeCrossReferences
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetEpisodeCrossReferences(AniDB_ID, null) ?? [];

    IReadOnlyList<IMetadataMovieCrossReference> ISeries.MetadataMovieCrossReferences
        => ISystemService.StaticServices.GetService<IMetadataService>()?.GetMovieCrossReferences(AniDB_ID, null) ?? [];

    AnimeType ISeries.Type => AniDB_Anime?.AnimeType ?? AnimeType.Unknown;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => [AnimeSeriesID];

    double ISeries.Rating => (AniDB_Anime?.Rating ?? 0) / 100D;

    int ISeries.RatingVotes => AniDB_Anime?.VoteCount ?? 0;

    bool ISeries.Restricted => AniDB_Anime?.IsRestricted ?? false;

    ReleaseStatus ISeries.ReleaseStatus => AniDB_Anime?.ReleaseStatus ?? ReleaseStatus.Unknown;

    SourceMaterial ISeries.SourceMaterial => AniDB_Anime?.SourceMaterial ?? SourceMaterial.Unknown;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => [this];

    /// <summary>
    ///   Everything every provider linked to this series relates it to, in one
    ///   list. A plugin that wants one provider's view reads it from that
    ///   provider's own entity instead.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries =>
        [.. LinkedSeries.SelectMany(series => series.RelatedSeries)];

    /// <summary>
    ///   The movies every provider linked to this series relates it to.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies =>
        [.. LinkedSeries.SelectMany(series => series.RelatedMovies)];

    /// <summary>
    ///   Everything every provider linked to this series suggests, in one
    ///   list. A plugin that wants one provider's view reads it from that
    ///   provider's own entity instead.
    /// </summary>
    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.Suggestions =>
        [.. LinkedSeries.SelectMany(series => series.Suggestions)];

    /// <summary>
    ///   Everything every provider linked to this series is suggested by.
    /// </summary>
    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.SuggestedBy =>
        [.. LinkedSeries.SelectMany(series => series.SuggestedBy)];

    IReadOnlyList<IVideoCrossReference> ISeries.VideoCrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    IReadOnlyList<ISeason> ISeries.Seasons => AnimeSeasons;

    IReadOnlyList<IEpisode> ISeries.Episodes => AllAnimeEpisodes;

    IReadOnlyList<IVideo> ISeries.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts => (this as IShokoSeries).AnidbAnime.EpisodeCounts;

    #endregion

    #region IShokoSeries Implementation

    int IShokoSeries.AnidbAnimeID => AniDB_ID;

    int IShokoSeries.ParentGroupID => AnimeGroupID;

    int IShokoSeries.TopLevelGroupID => TopLevelAnimeGroup.AnimeGroupID;

    IReadOnlyList<IShokoTagForSeries> IShokoSeries.Tags => RepoFactory.CustomTag.GetByAnimeID(AniDB_ID)
        .Select(x => new AnimeTag(x, this))
        .OrderBy(x => x.LocalID)
        .ToList();

    /// <summary>
    ///   The user's custom tags, then every linked series' and movie's tags.
    /// </summary>
    IReadOnlyList<ITag> IWithTags.Tags =>
    [
        .. ((IShokoSeries)this).Tags,
        .. LinkedSeries.SelectMany(series => series.Tags),
        .. LinkedMovies.SelectMany(movie => movie.Tags),
    ];

    int IShokoSeries.MissingEpisodeCount => MissingEpisodeCount;

    int IShokoSeries.MissingCollectingEpisodeCount => MissingEpisodeCountGroups;

    int IShokoSeries.HiddenMissingEpisodeCount => HiddenMissingEpisodeCount;

    int IShokoSeries.HiddenMissingCollectingEpisodeCount => HiddenMissingEpisodeCountGroups;

    IShokoGroup IShokoSeries.ParentGroup => AnimeGroup;

    IShokoGroup IShokoSeries.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IShokoGroup> IShokoSeries.AllParentGroups => AllGroupsAbove;

    FileSourceCounts IShokoSeries.FileSourceCounts
    {
        get
        {
            var counts = new FileSourceCounts();
            foreach (var vl in VideoLocals)
            {
                var ri = vl.ReleaseInfo;
                if (ri is null) continue;
                switch (ri.Source)
                {
                    case ReleaseSource.Unknown: counts.Unknown++; break;
                    case ReleaseSource.Other: counts.Other++; break;
                    case ReleaseSource.TV: counts.TV++; break;
                    case ReleaseSource.DVD: counts.DVD++; break;
                    case ReleaseSource.BluRay: counts.BluRay++; break;
                    case ReleaseSource.Web: counts.Web++; break;
                    case ReleaseSource.VHS: counts.VHS++; break;
                    case ReleaseSource.VCD: counts.VCD++; break;
                    case ReleaseSource.LaserDisc: counts.LaserDisc++; break;
                    case ReleaseSource.Camera: counts.Camera++; break;
                    case ReleaseSource.Film: counts.Film++; break;
                    default: counts.Other++; break;
                }
            }
            return counts;
        }
    }

    EpisodeCounts IShokoSeries.LocalEpisodeCounts
    {
        get
        {
            var counts = new EpisodeCounts();
            foreach (var ep in AnimeEpisodes)
            {
                if (ep.VideoLocals.Count == 0) continue;
                switch (ep.AniDB_Episode?.EpisodeType)
                {
                    case EpisodeType.Episode: counts.Episodes++; break;
                    case EpisodeType.Special: counts.Specials++; break;
                    case EpisodeType.Credits: counts.Credits++; break;
                    case EpisodeType.Trailer: counts.Trailers++; break;
                    case EpisodeType.Parody: counts.Parodies++; break;
                    default: counts.Others++; break;
                }
            }
            return counts;
        }
    }

    EpisodeCounts IShokoSeries.MissingEpisodeCounts
    {
        get
        {
            var counts = new EpisodeCounts();
            foreach (var ep in AnimeEpisodes)
            {
                if (ep.VideoLocals.Count > 0) continue;
                if (!(ep.AniDB_Episode?.HasAired ?? false)) continue;
                switch (ep.AniDB_Episode?.EpisodeType)
                {
                    case EpisodeType.Episode: counts.Episodes++; break;
                    case EpisodeType.Special: counts.Specials++; break;
                    case EpisodeType.Credits: counts.Credits++; break;
                    case EpisodeType.Trailer: counts.Trailers++; break;
                    case EpisodeType.Parody: counts.Parodies++; break;
                    default: counts.Others++; break;
                }
            }
            return counts;
        }
    }

    EpisodeCounts IShokoSeries.UnairedEpisodeCounts
    {
        get
        {
            var counts = new EpisodeCounts();
            foreach (var ep in AnimeEpisodes)
            {
                if (ep.VideoLocals.Count > 0) continue;
                if (ep.AniDB_Episode?.HasAired ?? false) continue;
                switch (ep.AniDB_Episode?.EpisodeType)
                {
                    case EpisodeType.Episode: counts.Episodes++; break;
                    case EpisodeType.Special: counts.Specials++; break;
                    case EpisodeType.Credits: counts.Credits++; break;
                    case EpisodeType.Trailer: counts.Trailers++; break;
                    case EpisodeType.Parody: counts.Parodies++; break;
                    default: counts.Others++; break;
                }
            }
            return counts;
        }
    }

    IReadOnlyDictionary<string, int> IShokoSeries.ReleaseProviderCounts
    {
        get
        {
            var counts = new Dictionary<string, int>();
            foreach (var vl in VideoLocals)
            {
                var ri = vl.ReleaseInfo;
                if (ri is null) continue;
                foreach (var provider in ri.ProviderName.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    counts.TryGetValue(provider, out var count);
                    counts[provider] = count + 1;
                }
            }
            return counts;
        }
    }

    IAnidbAnime IShokoSeries.AnidbAnime => AniDB_Anime ??
        throw new NullReferenceException($"Unable to find AniDB anime with id {AniDB_ID} in IShokoSeries.AnidbAnime");

    /// <summary>
    ///   Every series this anime is linked to: AniDB, then whatever the
    ///   cross-reference store holds, whether or not its source is enabled.
    ///   What the aggregate getters read from, so a newly linked source reaches
    ///   all of them at once.
    /// </summary>
    public IReadOnlyList<ISeries> LinkedSeries
    {
        get
        {
            var seriesList = new List<ISeries>();

            var anidbAnime = AniDB_Anime;
            if (anidbAnime is not null)
                seriesList.Add(anidbAnime);

            if (ISystemService.StaticServices.GetRequiredService<IMetadataService>() is MetadataService metadataService)
                seriesList.AddRange(metadataService.GetLinkedSeries(AniDB_ID));

            return seriesList;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IMovie> LinkedMovies
    {
        get
        {
            return ISystemService.StaticServices.GetRequiredService<IMetadataService>() is MetadataService metadataService
                ? metadataService.GetLinkedMovies(AniDB_ID)
                : [];
        }
    }

    IReadOnlyList<ISeries> IShokoSeries.LinkedSeries => LinkedSeries;

    IReadOnlyList<ISeason> IShokoSeries.LinkedSeasons
        => ISystemService.StaticServices.GetRequiredService<IMetadataService>() is MetadataService metadataService
            ? metadataService.GetLinkedSeasons(AniDB_ID)
            : [];

    IReadOnlyList<IMovie> IShokoSeries.LinkedMovies => LinkedMovies;

    IReadOnlyList<IShokoSeason> IShokoSeries.Seasons => AnimeSeasons;

    IReadOnlyList<IShokoEpisode> IShokoSeries.Episodes => AllAnimeEpisodes;

    ISeriesUserData IShokoSeries.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.LocalID is 0 || RepoFactory.JMMUser.GetByID(user.LocalID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(user.LocalID, AnimeSeriesID)
            ?? new() { JMMUserID = user.LocalID, AnimeSeriesID = AnimeSeriesID };
        if (userData.AnimeSeries_UserID is 0)
            RepoFactory.AnimeSeries_User.Save(userData);
        return userData;
    }

    #endregion
}
