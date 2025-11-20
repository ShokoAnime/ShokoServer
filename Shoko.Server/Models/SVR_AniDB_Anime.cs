using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Anidb;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AnimeTypeEnum = Shoko.Models.Enums.AnimeType;
using AbstractAnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AniDB_Anime : AniDB_Anime, ISeries, IAnidbAnime
{
    #region Properties & Methods

    #region General

    public bool IsRestricted
    {
        get => Restricted > 0;
        set => Restricted = value ? 1 : 0;
    }

    public AnimeTypeEnum AnimeTypeEnum => (AnimeTypeEnum)AnimeType;

    public AbstractAnimeType AbstractAnimeType => (AbstractAnimeType)AnimeType;

    public string? RawAnimeType => AnimeTypeEnum switch
    {
        AnimeTypeEnum.Movie => "movie",
        AnimeTypeEnum.OVA => "ova",
        AnimeTypeEnum.TVSeries => "tv series",
        AnimeTypeEnum.TVSpecial => "tv special",
        AnimeTypeEnum.Web => "web",
        AnimeTypeEnum.Other => "other",
        AnimeTypeEnum.MusicVideo => "music video",
        _ => null,
    };

    [XmlIgnore]
    public AniDB_Vote? UserVote
        => RepoFactory.AniDB_Vote.GetByAnimeID(AnimeID);

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

    public IEnumerable<(int Year, AnimeSeason Season)> Seasons
        => AirDate.GetYearlySeasons(EndDate);

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

    public List<SVR_AniDB_Anime_Relation> RelatedAnime
        => RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Similar> SimilarAnime
        => RepoFactory.AniDB_Anime_Similar.GetByAnimeID(AnimeID);

    public IReadOnlyList<AniDB_Anime_Character> Characters
        => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);

    #endregion

    #region Titles

    public List<SVR_AniDB_Anime_Title> Titles
        => RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);

    private string? _preferredTitle = null;

    public string PreferredTitle => LoadPreferredTitle();

    public void ResetPreferredTitle()
    {
        _preferredTitle = null;
        LoadPreferredTitle();
    }

    private string LoadPreferredTitle()
    {
        // Check if we have already loaded the preferred title.
        if (_preferredTitle is not null)
            return _preferredTitle;

        // Check each preferred language in order.
        var titles = Titles;
        foreach (var namingLanguage in Languages.PreferredNamingLanguages)
        {
            var thisLanguage = namingLanguage.Language;
            if (thisLanguage == TitleLanguage.Main)
                return _preferredTitle = MainTitle;

            // First check the main title.
            var title = titles.FirstOrDefault(t => t.TitleType == TitleType.Main && t.Language == thisLanguage);
            if (title != null)
                return _preferredTitle = title.Title;

            // Then check for an official title.
            title = titles.FirstOrDefault(t => t.TitleType == TitleType.Official && t.Language == thisLanguage);
            if (title != null)
                return _preferredTitle = title.Title;

            // Then check for _any_ title at all, if there is no main or official title in the language.
            if (Utils.SettingsProvider.GetSettings().Language.UseSynonyms)
            {
                title = titles.FirstOrDefault(t => t.Language == thisLanguage);
                if (title != null)
                    return _preferredTitle = title.Title;
            }
        }

        // Otherwise just use the cached main title.
        return _preferredTitle = MainTitle;
    }

    #endregion

    #region Images

    [XmlIgnore]
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

    public IImageMetadata PreferredOrDefaultPoster
        => PreferredPoster?.GetImageMetadata() ?? this.GetImageMetadata();


    public IImageMetadata? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AnimeID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImageMetadata>();
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

    public IReadOnlyList<SVR_AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    #endregion

    #region Trakt

    public IReadOnlyList<CrossRef_AniDB_TraktV2> TraktShowCrossReferences
        => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AnimeID);

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
            .WhereNotNull().Distinct()
            .ToList();


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

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => AnimeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => MainTitle;

    string IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles => Titles
        .Select(a => new AnimeTitle
        {
            Source = DataSourceEnum.AniDB,
            LanguageCode = a.LanguageCode,
            Language = a.Language,
            Title = a.Title,
            Type = a.TitleType,
        })
        .Where(a => a.Type != TitleType.None)
        .ToList();

    #endregion

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => Description;

    string IWithDescriptions.PreferredDescription => Description;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => [
        new()
        {
            Source = DataSourceEnum.AniDB,
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = string.Empty,
        },
    ];

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

    #region ISeries Implementation

    AbstractAnimeType ISeries.Type => AbstractAnimeType;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series.AnimeSeriesID] : [];

    double ISeries.Rating => Rating / 100D;

    bool ISeries.Restricted => IsRestricted;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series] : [];

    IImageMetadata? ISeries.DefaultPoster => this.GetImageMetadata();

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

    IReadOnlyList<IEpisode> ISeries.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeTypeEnum)
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
                Episodes = episodes.Count(a => a.Type == AbstractEpisodeType.Episode),
                Credits = episodes.Count(a => a.Type == AbstractEpisodeType.Credits),
                Others = episodes.Count(a => a.Type == AbstractEpisodeType.Other),
                Parodies = episodes.Count(a => a.Type == AbstractEpisodeType.Parody),
                Specials = episodes.Count(a => a.Type == AbstractEpisodeType.Special),
                Trailers = episodes.Count(a => a.Type == AbstractEpisodeType.Trailer)
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

    IReadOnlyList<IAnidbEpisode> IAnidbAnime.Episodes => AniDBEpisodes
        .OrderBy(a => a.EpisodeTypeEnum)
        .ThenBy(a => a.EpisodeNumber)
        .ToList();

    #endregion
}
