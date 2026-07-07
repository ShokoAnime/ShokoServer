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
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;

#pragma warning disable CS0618
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime : IAnidbAnime
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

    public string AllTitles { get; set; } = string.Empty;

    private static int _tagGeneration;

    internal static int TagGeneration => Volatile.Read(ref _tagGeneration);

    private string _allTags = string.Empty;

    public string AllTags
    {
        get => _allTags;
        set { _allTags = value; _allTagsCache = null; Interlocked.Increment(ref _tagGeneration); }
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

    public int? ANNID { get; set; }

    public int? AllCinemaID { get; set; }

    public int? AnisonID { get; set; }

    public int? SyoboiID { get; set; }

    public int? VNDBID { get; set; }

    public int? BangumiID { get; set; }

    public int? LainID { get; set; }

    public string? Site_JP { get; set; }

    public string? Site_EN { get; set; }

    public string? Wikipedia_ID { get; set; }

    public string? WikipediaJP_ID { get; set; }

    public string? CrunchyrollID { get; set; }

    public string? FunimationID { get; set; }

    public string? HiDiveID { get; set; }

    public int? LatestEpisodeNumber { get; set; }

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

    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var result = new List<Resource>();
            if (!string.IsNullOrEmpty(Site_EN))
                foreach (var site in Site_EN.Split('|'))
                    result.Add(new() { Type = ResourceType.Website, Name = "Official Site (EN)", Url = site, LanguageCode = "en" });

            if (!string.IsNullOrEmpty(Site_JP))
                foreach (var site in Site_JP.Split('|'))
                    result.Add(new() { Type = ResourceType.Website, Name = "Official Site (JP)", Url = site, LanguageCode = "ja" });

            if (!string.IsNullOrEmpty(Wikipedia_ID))
                result.Add(new() { Type = ResourceType.Metadata, Name = "Wikipedia (EN)", Url = $"https://en.wikipedia.org/{Wikipedia_ID}", LanguageCode = "en" });

            if (!string.IsNullOrEmpty(WikipediaJP_ID))
                result.Add(new() { Type = ResourceType.Metadata, Name = "Wikipedia (JP)", Url = $"https://en.wikipedia.org/{WikipediaJP_ID}", LanguageCode = "ja" });

            if (!string.IsNullOrEmpty(CrunchyrollID))
                result.Add(new() { Type = ResourceType.Streaming, Name = "Crunchyroll", Url = $"https://crunchyroll.com/series/{CrunchyrollID}" });

            if (!string.IsNullOrEmpty(FunimationID))
                result.Add(new() { Type = ResourceType.Streaming, Name = "Funimation", Url = FunimationID });

            if (!string.IsNullOrEmpty(HiDiveID))
                result.Add(new() { Type = ResourceType.Streaming, Name = "HiDive", Url = $"https://www.hidive.com/{HiDiveID}" });

            if (AllCinemaID.HasValue && AllCinemaID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "allcinema", Url = $"https://allcinema.net/cinema/{AllCinemaID.Value}" });

            if (AnisonID.HasValue && AnisonID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "Anison", Url = $"https://anison.info/data/program/{AnisonID.Value}.html" });

            if (SyoboiID.HasValue && SyoboiID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "syoboi", Url = $"https://cal.syoboi.jp/tid/{SyoboiID.Value}/time" });

            if (BangumiID.HasValue && BangumiID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "bangumi", Url = $"https://bgm.tv/subject/{BangumiID.Value}" });

            if (LainID.HasValue && LainID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = ".lain", Url = $"https://lain.gr.jp/mediadb/media/{LainID.Value}" });

            if (ANNID.HasValue && ANNID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "AnimeNewsNetwork", Url = $"https://www.animenewsnetwork.com/encyclopedia/php?id={ANNID.Value}" });

            if (VNDBID.HasValue && VNDBID.Value > 0)
                result.Add(new() { Type = ResourceType.CrossReference, Name = "VNDB", Url = $"https://vndb.org/v{VNDBID.Value}" });

            foreach (var malId in MalCrossReferences.Select(xref => xref.MALID).Distinct().Where(x => x >= 0))
                result.Add(new() { Type = ResourceType.CrossReference, Name = "MyAnimeList", Url = $"https://myanimelist.net/anime/{malId}" });

            return result;
        }
    }

    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => [.. AirDate.GetYearlySeasons(EndDate)];

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

    public List<AniDB_Anime_Title> Titles
        => RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    public string OriginalTitle => GetOriginalTitle(Titles) ?? MainTitle;

    private ITitle? _defaultTitle;

    public ITitle DefaultTitle
    {
        get
        {
            if (_defaultTitle is not null)
                return _defaultTitle;

            lock (this)
            {
                if (_defaultTitle is not null)
                    return _defaultTitle;

                var title = _defaultTitle = Titles.FirstOrDefault(title => title.TitleType == TitleType.Main);
                if (title is not null)
                    return _defaultTitle = title;

                var titleHelper = ISystemService.StaticServices.GetRequiredService<AniDBTitleHelper>();
                if (titleHelper.SearchAnimeID(AnimeID) is { } titleResponse)
                    return _preferredTitle = titleResponse.Titles.First(title => title.TitleType == TitleType.Main);

                return _preferredTitle = new TitleStub
                {
                    Language = TitleLanguage.Romaji,
                    LanguageCode = "x-jat",
                    Source = DataSource.AniDB,
                    Type = TitleType.Main,
                    Value = MainTitle,
                };
            }
        }
    }

    private bool _preferredTitleLoaded;

    private ITitle? _preferredTitle;

    public ITitle? PreferredTitle => LoadPreferredTitle();

    public void ResetPreferredTitle()
    {
        _preferredTitleLoaded = false;
        _preferredTitle = null;
        LoadPreferredTitle();
    }

    private ITitle? LoadPreferredTitle()
    {
        // Check if we have already loaded the preferred title.
        if (_preferredTitleLoaded)
            return _preferredTitle;

        lock (this)
        {
            if (_preferredTitleLoaded)
                return _preferredTitle;
            _preferredTitleLoaded = true;

            // Check each preferred language in order.
            var titles = Titles;
            foreach (var namingLanguage in Languages.PreferredNamingLanguages)
            {
                var thisLanguage = namingLanguage.Language;
                if (thisLanguage is TitleLanguage.Main)
                    return _preferredTitle = DefaultTitle;

                // First check the main title.
                var title = titles.FirstOrDefault(t => t.TitleType is TitleType.Main && t.Language == thisLanguage);
                if (title != null)
                    return _preferredTitle = title;

                // Then check for an official title.
                title = titles.FirstOrDefault(t => t.TitleType is TitleType.Official && t.Language == thisLanguage);
                if (title != null)
                    return _preferredTitle = title;

                // Then check for _any_ title at all, if there is no main or official title in the language.
                if (ISettingsProvider.Instance.GetSettings().Language.UseSynonyms)
                {
                    title = titles.FirstOrDefault(t => t.Language == thisLanguage);
                    if (title != null)
                        return _preferredTitle = title;
                }
            }

            // Otherwise just use the cached main title.
            return _preferredTitle = null;
        }
    }

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

            var id = IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, Picname).ToString("N");
            return Path.Join(ApplicationPaths.Instance.ImagesPath, DataSource.AniDB.ToString(), id[..2], id);
        }
    }

    public string PreferredOrDefaultPosterPath
        => (this as IWithPrimaryImage).PrimaryImageCrossReference?.GetImage() is { } primaryImage ? primaryImage.LocalPath! : PosterPath;

    #endregion

    #region AniDB

    public IReadOnlyList<AniDB_Season> AniDBSeasons => AniDBEpisodes.Any(e => e.EpisodeType is EpisodeType.Special)
        ? [new AniDB_Season(this, EpisodeType.Episode, 1), new AniDB_Season(this, EpisodeType.Special, 0)]
        : [new AniDB_Season(this, EpisodeType.Episode, 1)];

    public IReadOnlyList<AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences
        => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<TMDB_Show> TmdbShows
        => TmdbShowCrossReferences
            .Select(xref => RepoFactory.TMDB_Show.GetByTmdbShowID(xref.TmdbShowID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => tmdbShowId.HasValue
        ? RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(AnimeID, tmdbShowId.Value)
        : RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AnimeID);

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

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences
        => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies
        => TmdbMovieCrossReferences
            .Select(xref => RepoFactory.TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);

    #endregion

    #endregion

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Anime;

    DataSource IMetadata.Source => DataSource.AniDB;

    int IMetadata<int>.ID => AnimeID;

    #endregion

    #region IWithTitles Implementation

    ITitle IWithTitles.DefaultTitle => DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => Titles;

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => Description is { Length: > 0 }
        ? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => Description is { Length: > 0 } && ISettingsProvider.Instance.GetSettings().Language.DescriptionLanguageOrder.Contains("en")
        ? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [
        new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        },
    ];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(Picname) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, Picname) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

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

    #region ISeries Implementation

    AnimeType ISeries.Type => AnimeType;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series.AnimeSeriesID] : [];

    double ISeries.Rating => Rating / 100D;

    int ISeries.RatingVotes => VoteCount;

    bool ISeries.Restricted => IsRestricted;

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

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID);

    IReadOnlyList<ISeason> ISeries.Seasons => AniDBSeasons;

    IReadOnlyList<IEpisode> ISeries.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeType)
        .ThenBy(a => a.EpisodeNumber)
        .ToList();

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

    IReadOnlyList<IAnidbSimilarAnime> IAnidbAnime.Similar => SimilarAnime;

    IReadOnlyList<IAnidbReleaseGroupStatus> IAnidbAnime.ReleaseGroupStatuses => ReleaseGroupStatuses;

    IReadOnlyList<IAnidbSeason> IAnidbAnime.Seasons => AniDBSeasons;

    IReadOnlyList<IAnidbEpisode> IAnidbAnime.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeType)
        .ThenBy(a => a.EpisodeNumber)
        .ToList();

    #endregion
}
