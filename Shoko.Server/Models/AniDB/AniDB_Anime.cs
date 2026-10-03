using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
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
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Services;

using AnidbRegularAirDates = Shoko.Server.Providers.AniDB.AnidbRegularAirDates;
using AnidbReleaseStatus = Shoko.Server.Providers.AniDB.AnidbReleaseStatus;
using AnidbResourceLinks = Shoko.Server.Providers.AniDB.AnidbResourceLinks;
using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;
using ResourceLinkType = Shoko.Server.Providers.AniDB.ResourceLinkType;

#pragma warning disable CS0618
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime : IAnidbAnime, IInlineTextSource
{
    #region Server DB columns

    public int AniDB_AnimeID { get; set; }

    public int AnimeID { get; set; }

    public int EpisodeCount { get; set; }

    public PartialDateOnly? AirDate { get; set; }

    public PartialDateOnly? EndDate { get; set; }

    public string? URL { get; set; }

    public string? Picname { get; set; }

    public int BeginYear { get; set; }

    public int EndYear { get; set; }

    public AnimeType AnimeType { get; set; }

    public string MainTitle { get; set; } = string.Empty;

    private static int _tagGeneration;

    internal static int TagGeneration => Volatile.Read(ref _tagGeneration);

    private string _allTags = string.Empty;

    public string AllTags
    {
        get => _allTags;
        set { _allTags = value; _allTagsCache = null; ResetSourceMaterial(); Interlocked.Increment(ref _tagGeneration); }
    }

    public string Description { get; set; } = string.Empty;

    public int EpisodeCountNormal { get; set; }

    public int EpisodeCountSpecial { get; set; }

    public int Rating { get; set; }

    public int VoteCount { get; set; }

    public int TempRating { get; set; }

    public int TempVoteCount { get; set; }

    public int AvgReviewRating { get; set; }

    public int ReviewCount { get; set; }

    /// <summary>
    ///   When the anime was first stored locally. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When we last tried to update the metadata.
    /// </summary>
    [Obsolete("Deprecated in favor of AniDB_AnimeUpdate. This is for when an AniDB_Anime fails to save")]
    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    ///   When the metadata was last updated.
    /// </summary>
    public DateTime DateTimeDescUpdated { get; set; }

    public int ImageEnabled { get; set; }

    public int Restricted { get; set; }

    public int? LatestEpisodeNumber { get; set; }

    /// <summary>
    ///   The ordering chosen for the anime, of any source, or <c>null</c>
    ///   for its default one. Set through
    ///   <see cref="IMetadataOrderingService.SetPreferredOrdering"/>.
    /// </summary>
    public MetadataGuid? PreferredOrderingID { get; set; }

    #endregion

    #region Properties & Methods

    #region General

    public bool IsRestricted
    {
        get => Restricted > 0;
        set => Restricted = value ? 1 : 0;
    }

    public string? RawAnimeType => AnimeType switch
    {
        AnimeType.Movie => "movie",
        AnimeType.OVA => "ova",
        AnimeType.TV => "tv series",
        AnimeType.TVSpecial => "tv special",
        AnimeType.Web => "web",
        AnimeType.Other => "other",
        AnimeType.MusicVideo => "music video",
        _ => null,
    };

    /// <summary>
    ///   Every resource AniDB lists for the anime, then the links the
    ///   resource resolvers add.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var result = GetAnidbResources();
            result.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return result;
        }
    }

    /// <summary>
    ///   Every resource AniDB lists for the anime, in AniDB's order, then
    ///   the MyAnimeList links AniDB no longer lists.
    /// </summary>
    /// <returns>The anime's own links.</returns>
    public List<Resource> GetAnidbResources()
    {
        var rows = RepoFactory.AniDB_Resource.GetByAnimeID(AnimeID);
        var result = AnidbResourceLinks.ToResources(rows);
        var listedMalIDs = rows
            .Where(row => row.ResourceType is ResourceLinkType.MAL && row.Identifiers.Count > 0)
            .Select(row => row.Identifiers[0])
            .ToHashSet();
        foreach (var malID in MalCrossReferences.Select(xref => xref.MALID).Distinct().Where(x => x >= 0))
            if (!listedMalIDs.Contains(malID.ToString()))
                result.Add(new()
                {
                    Type = ResourceType.CrossReference,
                    Name = "MyAnimeList",
                    Url = $"https://myanimelist.net/anime/{malID}",
                    ID = malID.ToString(),
                });

        return result;
    }

    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => [.. AirDate.GetYearlySeasons(this.EffectiveEndDateForSeasons)];

    public List<CustomTag> CustomTags
        => RepoFactory.CustomTag.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Tag> AnimeTags
        => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);

    private HashSet<string>? _allTagsCache;

    public HashSet<string> GetAllTagsSet()
    {
        if (_allTagsCache is { } cached) return cached;
        var tags = string.IsNullOrEmpty(AllTags)
            ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            : new HashSet<string>(AllTags.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.InvariantCultureIgnoreCase);
        return Interlocked.CompareExchange(ref _allTagsCache, tags, null) ?? tags;
    }

    public List<AniDB_Tag> Tags
        => GetAniDBTags();

    public List<AniDB_Tag> GetAniDBTags(bool onlyVerified = true)
        => onlyVerified
            ? AnimeTags
                .Select(tag => RepoFactory.AniDB_Tag.GetByTagID(tag.TagID))
                .WhereNotNull()
                .Where(tag => tag.Verified)
                .ToList()
            : AnimeTags
                .Select(tag => RepoFactory.AniDB_Tag.GetByTagID(tag.TagID))
                .WhereNotNull()
                .ToList();

    private int _sourceMaterial = -1;

    /// <summary>
    ///   What the anime was adapted from, picked from its source material
    ///   tags. Cached until the tags are imported again.
    /// </summary>
    public SourceMaterial SourceMaterial
    {
        get
        {
            var cached = Volatile.Read(ref _sourceMaterial);
            if (cached >= 0)
                return (SourceMaterial)cached;

            var value = TagFilter.GetSourceMaterial(
                AnimeTags.Select(xref => (xref.TagID, xref.Weight)),
                tagID => RepoFactory.AniDB_Tag.GetByTagID(tagID)?.ParentTagID
            );
            Volatile.Write(ref _sourceMaterial, (int)value);
            return value;
        }
    }

    /// <summary>
    ///   Clears the cached <see cref="SourceMaterial"/>, for after the tags
    ///   were imported again.
    /// </summary>
    public void ResetSourceMaterial()
        => Volatile.Write(ref _sourceMaterial, -1);

    private sealed record ReleaseStatusEntry(DateOnly Today, ReleaseStatus Value);

    private ReleaseStatusEntry? _releaseStatus;

    /// <summary>
    ///   Where the anime is in its release, inferred from its dates, type and
    ///   episodes. Cached for the day, or until the anime is imported again.
    /// </summary>
    public ReleaseStatus ReleaseStatus
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (Volatile.Read(ref _releaseStatus) is { } cached && cached.Today == today)
                return cached.Value;

            var value = AnidbReleaseStatus.Infer(
                AirDate,
                EndDate,
                AnimeType,
                EpisodeCountNormal,
                AniDBEpisodes.Where(episode => episode.EpisodeType is EpisodeType.Episode).Select(episode => episode.GetAirDateAsDateOnly()),
                today
            );
            Volatile.Write(ref _releaseStatus, new(today, value));
            return value;
        }
    }

    /// <summary>
    ///   Clears the cached <see cref="ReleaseStatus"/>, for after the anime or
    ///   its episodes were imported again.
    /// </summary>
    public void ResetReleaseStatus()
        => Volatile.Write(ref _releaseStatus, null);

    private sealed record RegularAirDatesEntry(PartialDateOnly? ReadWith, IReadOnlyDictionary<int, (DateOnly Stored, DateOnly Regular)> Episodes, PartialDateOnly? AnimeDate);

    private RegularAirDatesEntry? _regularAirDates;

    /// <summary>
    ///   The normal episodes the note in the description moves from an early
    ///   showing onto the regular run, by episode ID, and the anime's own
    ///   regular date. Cached until the anime is imported again or its
    ///   <see cref="AirDate"/> changes, as the calendar changes it.
    /// </summary>
    private RegularAirDatesEntry RegularAirDates
    {
        get
        {
            var airDate = AirDate;
            if (Volatile.Read(ref _regularAirDates) is { } cached && cached.ReadWith == airDate)
                return cached;

            var episodes = AniDBEpisodes
                .Where(episode => episode.EpisodeType is EpisodeType.Episode)
                .Select(episode => (Episode: episode, AirDate: episode.GetAirDateAsDateOnly()))
                .Where(tuple => tuple.AirDate.HasValue)
                .ToList();
            var reading = AnidbRegularAirDates.Read(
                Description,
                AnimeType,
                episodes.Select(tuple => (tuple.Episode.EpisodeNumber, tuple.AirDate!.Value)),
                airDate is { IsComplete: true } complete ? complete.ToDateOnly() : null
            );
            var byNumber = episodes
                .GroupBy(tuple => tuple.Episode.EpisodeNumber)
                .ToDictionary(group => group.Key, group => group.First().Episode.EpisodeID);
            var moved = reading.Episodes.ToDictionary(episode => byNumber[episode.EpisodeNumber], episode => (episode.Stored, episode.Regular));

            // The first moved episode's regular date starts the regular run;
            // an anime dated before it is dated by the early showing too.
            var animeDate = airDate;
            if (reading.Episodes is [var first, ..] && airDate is { IsComplete: true } date && date.ToDateOnly() < first.Regular)
                animeDate = new PartialDateOnly(first.Regular);

            var value = new RegularAirDatesEntry(airDate, moved, animeDate);
            Volatile.Write(ref _regularAirDates, value);
            return value;
        }
    }

    /// <summary>
    ///   When the anime's regular broadcast started, for matching it against
    ///   other sources: <see cref="AirDate"/>, unless the first episode was
    ///   shown early and its regular date falls after it.
    /// </summary>
    public PartialDateOnly? RegularAirDate
        => RegularAirDates.AnimeDate;

    /// <summary>
    ///   The regular broadcast date the note in the description moves one of
    ///   the anime's episodes to.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>
    ///   The regular date, or <see langword="null"/> when the episode was not
    ///   moved or its stored date changed since.
    /// </returns>
    public DateOnly? GetRegularAirDate(AniDB_Episode episode)
        => RegularAirDates.Episodes.TryGetValue(episode.EpisodeID, out var moved) && episode.GetAirDateAsDateOnly() == moved.Stored
            ? moved.Regular
            : null;

    /// <summary>
    ///   Clears the cached regular air dates, for after the anime or its
    ///   episodes were imported again.
    /// </summary>
    public void ResetRegularAirDates()
        => Volatile.Write(ref _regularAirDates, null);

    public List<AniDB_Anime_Relation> RelatedAnime
        => RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Similar> SimilarAnime
        => RepoFactory.AniDB_Anime_Similar.GetByAnimeID(AnimeID);

    public IReadOnlyList<AniDB_GroupStatus> ReleaseGroupStatuses
        => RepoFactory.AniDB_GroupStatus.GetByAnimeID(AnimeID).OrderBy(a => a.GroupID).ToList();

    public IReadOnlyList<AniDB_Anime_Character> Characters
        => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);

    #endregion

    #region Titles

    /// <summary>
    ///   The titles AniDB gave the anime, in AniDB's own order.
    /// </summary>
    public IReadOnlyList<ITitle> Titles
        => AnidbText.Present(TextAccess.Manager.OwnTitlesOf(this));

    /// <summary>
    ///   Every title AniDB gave the anime, joined with <c>|</c>, worked out
    ///   from the stored titles.
    /// </summary>
    public string AllTitles
        => string.Join('|', Titles.Select(title => title.Value));

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    public string OriginalTitle => GetOriginalTitle(Titles) ?? MainTitle;

    /// <summary>
    ///   The anime's main title: AniDB's stored one, else the one in the
    ///   catalog of every anime AniDB has, else the main title on the row.
    /// </summary>
    public ITitle DefaultTitle
    {
        get
        {
            if (Titles.FirstOrDefault(title => title.Type == TitleType.Main) is { } title)
                return title;

            var titleHelper = ISystemService.StaticServices.GetRequiredService<AniDBTitleHelper>();
            if (titleHelper.SearchAnimeID(AnimeID) is { } titleResponse)
                return titleResponse.Titles.First(title => title.TitleType == TitleType.Main);

            return new TitleStub
            {
                Language = TitleLanguage.Romaji,
                LanguageCode = "x-jat",
                Source = MetadataSource.AniDB,
                Type = TitleType.Main,
                Value = MainTitle,
            };
        }
    }

    /// <summary>
    /// The original language of the anime, derived from the language of the
    /// main title. AniDB only lists the original-language cast, so this is
    /// also the language of every cast role.
    /// </summary>
    public TitleLanguage OriginalLanguage => DefaultTitle.Language.GetSpokenLanguage();

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   anime, or <c>null</c> when none is picked or in a preferred language.
    /// </summary>
    public ITitle? PreferredTitle
        => AnidbText.Present(TextAccess.Manager.PreferredTitleFor(this));

    private static string? GetOriginalTitle(IReadOnlyList<ITitle> titles)
        => GetTitleForLanguage(titles, GuessOriginLanguage(titles));

    private static string? GetTitleForLanguage(IReadOnlyList<ITitle> titles, params string?[] metadataLanguages)
    {
        foreach (var lang in metadataLanguages)
        {
            if (string.IsNullOrEmpty(lang))
                continue;

            var title = titles.FirstOrDefault(t => t.Type == TitleType.Official && t.LanguageCode == lang)?.Value;
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        return null;
    }

    private static string[] GuessOriginLanguage(IReadOnlyList<ITitle> titles)
    {
        var langCode = GetMainLanguage(titles);
        return langCode switch
        {
            "x-other" or "x-jat" => ["ja", "jap"],
            "x-zht" => ["zn-hans", "zn-hant", "zn-c-mcm", "zn", "zht"],
            _ => string.IsNullOrEmpty(langCode) ? [] : [langCode],
        };
    }

    private static string GetMainLanguage(IReadOnlyList<ITitle> titles)
        => titles.FirstOrDefault(t => t?.Type == TitleType.Main)?.LanguageCode ?? titles.FirstOrDefault()?.LanguageCode ?? "x-other";

    #endregion

    #region Images

    public string PosterPath
    {
        get
        {
            if (string.IsNullOrEmpty(Picname))
            {
                return string.Empty;
            }

            var id = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, Picname).ToString("N");
            return Path.Join(ApplicationPaths.Instance.ImagesPath, MetadataSource.AniDB.ToString(), id[..2], id);
        }
    }

    #endregion

    #region AniDB

    public IReadOnlyList<AniDB_Season> AniDBSeasons => AniDBEpisodes.Any(e => e.EpisodeType is EpisodeType.Special)
        ? [new AniDB_Season(this, EpisodeType.Episode, 1), new AniDB_Season(this, EpisodeType.Special, 0)]
        : [new AniDB_Season(this, EpisodeType.Episode, 1)];

    public IReadOnlyList<AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);

    #endregion

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Series, AnimeID.ToString());

    int IAnidbAnime.AnidbID => AnimeID;

    #endregion

    #region IWithTitles Implementation

    ITitle IWithTitles.DefaultTitle => DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => AnidbText.Present(TextAccess.Manager.ListTitles(this));

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => null;

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(MetadataSource.AniDB, Description, TitleLanguage.English, "en");

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => TextAccess.Manager.DefaultOverviewFor(this);

    IText? IWithOverviews.PreferredOverview => TextAccess.Manager.PreferredOverviewFor(this);

    // A missing description is still listed, empty, as it always was.
    IReadOnlyList<IText> IWithOverviews.Overviews => [
        ((IInlineTextSource)this).InlineOverview ?? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = MetadataSource.AniDB,
        },
    ];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(Picname) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, Picname) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeDescUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
        .SelectMany(xref =>
        {
            // We don't want borked cross-references to show up.
            if (xref.Character is not { } character)
                return [];

            // If a role don't have a creator then we still want it to show up.
            var creatorXrefs = xref.CreatorCrossReferences;
            if (creatorXrefs is { Count: 0 })
                return [new AniDB_Cast(xref, character, null, () => this)];

            return creatorXrefs
                .Select(x => new AniDB_Cast(xref, character, x.CreatorID, () => this));
        })
        .ToList();

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
        .Select(xref =>
        {
            // Hide studio and actor roles from crew members. Actors should show up as cast, and studios as studios.
            if (xref is { RoleType: CreatorRoleType.Studio or CreatorRoleType.Actor })
                return null;

            return new AniDB_Crew(xref, () => this);
        })
        .WhereNotNull()
        .ToList();

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AnimeID)
        .Select(xref =>
        {
            // We only want the studio roles to mapped as studios.
            if (xref is { RoleType: not CreatorRoleType.Studio })
                return null;

            // Hide broken cross-references and non-companies.
            if (xref.Creator is not { Type: CreatorType.Company } creator)
                return null;

            return new AniDB_Studio_For_Anime(xref, creator, this);
        })
        .WhereNotNull()
        .ToList();

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => [];

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs
        =>
        [
            .. ((IAnidbAnime)this).MalIDs.Select(malID => CrossSourceID.For("mal", MetadataEntityType.Series, malID)).WhereNotNull(),
            .. AnidbResourceLinks.ToCrossSourceIDs(RepoFactory.AniDB_Resource.GetByAnimeID(AnimeID), AnimeType is AnimeType.Movie),
        ];

    #endregion

    #region ISeries Implementation

    // Set on every fetch the anime is processed from, whether or not anything changed.
    DateTime? ISeries.LastRefreshedAt => DateTimeUpdated.ToUniversalTime();

    IReadOnlyList<IOrdering<IAnidbAnime, IAnidbEpisode>> ISeries<IAnidbAnime, IAnidbEpisode>.Orderings => OrderingLookup.For<IAnidbAnime, IAnidbEpisode>(this);

    IOrdering<IAnidbAnime, IAnidbEpisode> ISeries<IAnidbAnime, IAnidbEpisode>.PreferredOrdering => OrderingLookup.PreferredFor<IAnidbAnime, IAnidbEpisode>(this);

    IReadOnlyList<IMetadataSeriesCrossReference> ISeries.MetadataSeriesCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeriesCrossReferences(AnimeID) ?? [];

    IReadOnlyList<IMetadataSeasonCrossReference> ISeries.MetadataSeasonCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetSeasonCrossReferences(AnimeID) ?? [];

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeries.MetadataEpisodeCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetEpisodeCrossReferencesForSeries(AnimeID) ?? [];

    IReadOnlyList<IMetadataMovieCrossReference> ISeries.MetadataMovieCrossReferences =>
        ISystemService.StaticServices.GetService<IMetadataService>()?.GetMovieCrossReferencesForSeries(AnimeID) ?? [];

    AnimeType ISeries.Type => AnimeType;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series.AnimeSeriesID] : [];

    double ISeries.Rating => Rating / 100D;

    int ISeries.RatingVotes => VoteCount;

    bool ISeries.Restricted => IsRestricted;

    ReleaseStatus ISeries.ReleaseStatus => ReleaseStatus;

    SourceMaterial ISeries.SourceMaterial => SourceMaterial;

    // AniDB keeps no original language, popularity, favorite count, networks
    // or production countries for an anime.
    string? ISeries.OriginalLanguageCode => null;

    double? ISeries.Popularity => null;

    int? ISeries.FavoriteCount => null;

    IReadOnlyList<INetwork> ISeries.Networks => [];

    IReadOnlyList<string> ISeries.ProductionCountries => [];

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series] : [];

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries =>
        RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID)
            .Concat(RepoFactory.AniDB_Anime_Relation.GetByRelatedAnimeID(AnimeID).Select(a => a.Reversed))
            .Distinct()
            .OrderBy(a => a.AnimeID)
            .ThenBy(a => a.RelatedAnimeID)
            .ThenBy(a => a.RelationType)
            .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.VideoCrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID);

    IReadOnlyList<IVideo> ISeries.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts
    {
        get
        {
            var episodes = (this as ISeries).Episodes;
            return new()
            {
                Episodes = episodes.Count(a => a.Type == EpisodeType.Episode),
                Credits = episodes.Count(a => a.Type == EpisodeType.Credits),
                Others = episodes.Count(a => a.Type == EpisodeType.Other),
                Parodies = episodes.Count(a => a.Type == EpisodeType.Parody),
                Specials = episodes.Count(a => a.Type == EpisodeType.Special),
                Trailers = episodes.Count(a => a.Type == EpisodeType.Trailer)
            };
        }
    }

    #endregion

    #region IAnidbAnime Implementation

    IReadOnlyList<int> IAnidbAnime.MalIDs => MalCrossReferences
        .Select(xref => xref.MALID)
        .Distinct()
        .Where(x => x >= 0)
        .ToList();

    IReadOnlyList<IAnidbTagForAnime> IAnidbAnime.Tags => AnimeTags
        .Select(xref => (xref, tag: xref.Tag!))
        .Where(tuple => tuple.tag is not null)
        .Select(tuple => new AniDB_Anime_Tag_Abstract(tuple.tag, tuple.xref))
        .ToList();

    IReadOnlyList<IAnidbReleaseGroupStatus> IAnidbAnime.ReleaseGroupStatuses => ReleaseGroupStatuses;

    IReadOnlyList<ISeason<IAnidbAnime, IAnidbEpisode>> ISeries<IAnidbAnime, IAnidbEpisode>.Seasons => AniDBSeasons;

    IReadOnlyList<IAnidbEpisode> ISeries<IAnidbAnime, IAnidbEpisode>.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeType)
        .ThenBy(a => a.EpisodeNumber)
        .ToList();

    IReadOnlyList<ISuggestedMetadata<IAnidbAnime, ISeries>> ISeries<IAnidbAnime, IAnidbEpisode>.Suggestions => SimilarAnime;

    IReadOnlyList<ISuggestedMetadata<IAnidbAnime, ISeries>> ISeries<IAnidbAnime, IAnidbEpisode>.SuggestedBy
        => RepoFactory.AniDB_Anime_Similar.GetBySimilarAnimeID(AnimeID);

    #endregion
}
