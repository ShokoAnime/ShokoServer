using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
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
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime : IAnidbAnime
{
    #region Server DB columns

    public int AniDB_AnimeID { get; set; }

    public int AnimeID { get; set; }

    public int EpisodeCount { get; set; }

    public DateTime? AirDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? URL { get; set; }

    public string? Picname { get; set; }

    public int BeginYear { get; set; }

    public int EndYear { get; set; }

    public AnimeType AnimeType { get; set; }

    public string MainTitle { get; set; } = string.Empty;

    public string AllTitles { get; set; } = string.Empty;

    public string AllTags { get; set; } = string.Empty;

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
        AnimeType.TVSeries => "tv series",
        AnimeType.TVSpecial => "tv special",
        AnimeType.Web => "web",
        AnimeType.Other => "other",
        AnimeType.MusicVideo => "music video",
        _ => null,
    };

    public List<(string Type, string Name, string URL)> Resources
    {
        get
        {
            var result = new List<(string Type, string Name, string URL)>();
            if (!string.IsNullOrEmpty(Site_EN))
                foreach (var site in Site_EN.Split('|'))
                    result.Add((Type: "source", Name: "Official Site (EN)", URL: site));

            if (!string.IsNullOrEmpty(Site_JP))
                foreach (var site in Site_JP.Split('|'))
                    result.Add((Type: "source", Name: "Official Site (JP)", URL: site));

            if (!string.IsNullOrEmpty(Wikipedia_ID))
                result.Add((Type: "wiki", Name: "Wikipedia (EN)", URL: $"https://en.wikipedia.org/{Wikipedia_ID}"));

            if (!string.IsNullOrEmpty(WikipediaJP_ID))
                result.Add((Type: "wiki", Name: "Wikipedia (JP)", URL: $"https://en.wikipedia.org/{WikipediaJP_ID}"));

            if (!string.IsNullOrEmpty(CrunchyrollID))
                result.Add((Type: "streaming", Name: "Crunchyroll", URL: $"https://crunchyroll.com/series/{CrunchyrollID}"));

            if (!string.IsNullOrEmpty(FunimationID))
                result.Add((Type: "streaming", Name: "Funimation", URL: FunimationID));

            if (!string.IsNullOrEmpty(HiDiveID))
                result.Add((Type: "streaming", Name: "HiDive", URL: $"https://www.hidive.com/{HiDiveID}"));

            if (AllCinemaID.HasValue && AllCinemaID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "allcinema", URL: $"https://allcinema.net/cinema/{AllCinemaID.Value}"));

            if (AnisonID.HasValue && AnisonID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "Anison", URL: $"https://anison.info/data/program/{AnisonID.Value}.html"));

            if (SyoboiID.HasValue && SyoboiID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "syoboi", URL: $"https://cal.syoboi.jp/tid/{SyoboiID.Value}/time"));

            if (BangumiID.HasValue && BangumiID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "bangumi", URL: $"https://bgm.tv/subject/{BangumiID.Value}"));

            if (LainID.HasValue && LainID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: ".lain", URL: $"https://lain.gr.jp/mediadb/media/{LainID.Value}"));

            if (ANNID.HasValue && ANNID.Value > 0)
                result.Add((Type: "english-metadata", Name: "AnimeNewsNetwork", URL: $"https://www.animenewsnetwork.com/encyclopedia/php?id={ANNID.Value}"));

            if (VNDBID.HasValue && VNDBID.Value > 0)
                result.Add((Type: "english-metadata", Name: "VNDB", URL: $"https://vndb.org/v{VNDBID.Value}"));

            return result;
        }
    }

    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => [.. AirDate.GetYearlySeasons(EndDate)];

    public List<CustomTag> CustomTags
        => RepoFactory.CustomTag.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Tag> AnimeTags
        => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);

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

    public IReadOnlyList<AniDB_Anime_Character> Characters
        => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);

    #endregion

    #region Titles

    public List<AniDB_Anime_Title> Titles
        => RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);

    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle = null;

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

                var titleHelper = Utils.ServiceContainer.GetRequiredService<AniDBTitleHelper>();
                if (titleHelper.SearchAnimeID(AnimeID) is { } titleResponse)
                    return _preferredTitle = titleResponse.Titles.First(title => title.TitleType == TitleType.Main);

                return _preferredTitle = new TitleStub()
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

    private bool _preferredTitleLoaded = false;

    private ITitle? _preferredTitle = null;

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
                if (Utils.SettingsProvider.GetSettings().Language.UseSynonyms)
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

            return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
        }
    }

    public AniDB_Anime_PreferredImage? PreferredPoster
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Poster);

    public AniDB_Anime_PreferredImage? PreferredBackdrop
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Backdrop);

    public AniDB_Anime_PreferredImage? PreferredBanner
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Banner);

    public string PreferredOrDefaultPosterPath
        => PreferredPoster?.GetImageMetadata() is { } defaultPoster ? defaultPoster.LocalPath! : PosterPath;

    public IImage PreferredOrDefaultPoster
        => PreferredPoster?.GetImageMetadata() ?? this.GetImageMetadata();


    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AnimeID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImage>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = this.GetImageMetadata(false);
            if (poster is not null)
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster.Equals(preferredPoster)
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var xref in TmdbShowCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbSeasonCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbMovieCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

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

    public IReadOnlyList<TMDB_Image> TmdbShowBackdrops
        => TmdbShowCrossReferences
            .SelectMany(xref => RepoFactory.TMDB_Image.GetByTmdbShowIDAndType(xref.TmdbShowID, ImageEntityType.Backdrop))
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

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
    {
        throw new NotImplementedException();
    }

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences
        => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies
        => TmdbMovieCrossReferences
            .Select(xref => RepoFactory.TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<TMDB_Image> TmdbMovieBackdrops
        => TmdbMovieCrossReferences
            .SelectMany(xref => RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(xref.TmdbMovieID, ImageEntityType.Backdrop))
            .ToList();

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);

    #endregion

    #endregion

    #region IMetadata Implementation

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
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => Description is { Length: > 0 } && Utils.SettingsProvider.GetSettings().Language.DescriptionLanguageOrder.Contains("en")
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [
        new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        },
    ];

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeDescUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
        .SelectMany(xref =>
        {
            // We don't want organizations or borked cross-references to show up.
            var character = xref.Character;
            if (character is not { Type: not Server.CharacterType.Organization })
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
            if (xref is { RoleType: Server.CreatorRoleType.Studio or Server.CreatorRoleType.Actor })
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
            if (xref is { RoleType: not Server.CreatorRoleType.Studio })
                return null;

            // Hide broken cross-references and non-companies.
            if (xref.Creator is not { Type: Providers.AniDB.CreatorType.Company } creator)
                return null;

            return new AniDB_Studio(xref, creator, this);
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

    IImage? ISeries.DefaultPoster => this.GetImageMetadata();

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

    IReadOnlyList<IAnidbSeason> IAnidbAnime.Seasons => AniDBSeasons;

    IReadOnlyList<IAnidbEpisode> IAnidbAnime.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeType)
        .ThenBy(a => a.EpisodeNumber)
        .ToList();

    #endregion
}
