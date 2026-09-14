using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Anilist.Embedded;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Anime Database Model.
/// </summary>
public class Anilist_Anime : Anilist_Base<int>, IAnilistAnime
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_AnimeID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// The ID of the My Anime List (MAL) entry linked to the anime, if set.
    /// </summary>
    public int? MalID { get; set; }

    /// <summary>
    /// English title.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// Site hosting the trailer, e.g. "youtube" or "dailymotion", if AniList has one.
    /// </summary>
    public string? TrailerSite { get; set; }

    /// <summary>
    /// The trailer's video ID on <see cref="TrailerSite"/>.
    /// </summary>
    public string? TrailerID { get; set; }

    /// <summary>
    /// Main title. AniList stores a transcription of the native title here
    /// (romaji for japanese anime, pinyin for chinese anime, and so on), and
    /// treats it as the canonical title, so it fills the same role as the
    /// AniDB main title.
    /// </summary>
    public string MainTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original language of the anime, from its country of origin.
    /// </summary>
    public TitleLanguage OriginalLanguage => OriginalLanguageCode.GetTitleLanguage();

    /// <summary>
    /// The transcription language of the main title, derived from the
    /// original language.
    /// </summary>
    public TitleLanguage MainTitleLanguage => AnilistUtility.GetMainTitleLanguage(OriginalLanguageCode);

    /// <summary>
    /// Native title.
    /// </summary>
    public string NativeTitle { get; set; } = string.Empty;

    /// <summary>
    /// English overview/description.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Original language code.
    /// </summary>
    public string OriginalLanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Alternative titles.
    /// </summary>
    public List<string> Synonyms { get; set; } = [];

    /// <summary>
    /// Anime type (TV, Movie, OVA, etc.).
    /// </summary>
    public AnimeType Type { get; set; } = AnimeType.Unknown;

    /// <summary>
    /// Airing status.
    /// </summary>
    public AnilistMediaStatus ReleasingStatus { get; set; }

    /// <summary>
    /// Source material.
    /// </summary>
    public AnilistMediaSource MediaSource { get; set; }

    /// <summary>
    /// Season of release.
    /// </summary>
    public YearlySeason? Season { get; set; }

    /// <summary>
    /// Year of the season.
    /// </summary>
    public int? SeasonYear { get; set; }

    /// <summary>
    /// URL to the cover image (extra large).
    /// </summary>
    public string CoverImagePath { get; set; } = string.Empty;

    /// <summary>
    /// URL to the banner image.
    /// </summary>
    public string BannerImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Total episode count.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Default episode duration in minutes.
    /// </summary>
    public int? EpisodeDuration { get; set; }

    /// <summary>
    /// Average user rating (0-100 scale).
    /// </summary>
    public double UserRating { get; set; }

    /// <summary>
    /// Mean score (0-100 scale), separate from <see cref="UserRating"/>.
    /// </summary>
    public double MeanScore { get; set; }

    /// <summary>
    /// Number of user votes.
    /// </summary>
    public int UserVotes { get; set; }

    /// <summary>
    /// Popularity rank.
    /// </summary>
    public int Popularity { get; set; }

    /// <summary>
    /// Number of favorites.
    /// </summary>
    public int FavoriteCount { get; set; }

    /// <summary>
    /// Whether the anime is licensed.
    /// </summary>
    public bool IsLicensed { get; set; }

    /// <summary>
    /// Whether the anime is restricted (adult content).
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Primary color from the cover image.
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Genres.
    /// </summary>
    public List<string> Genres { get; set; } = [];

    /// <summary>
    /// Start date.
    /// </summary>
    public PartialDateOnly? FirstAiredAt { get; set; }

    /// <summary>
    /// End date.
    /// </summary>
    public PartialDateOnly? LastAiredAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistAnimeID;

    /// <summary>
    /// The title to show when nothing better is available. English if set,
    /// otherwise romaji.
    /// </summary>
    public string PreferredTitle => GetPreferredTitle()!.Value;

    /// <summary>
    /// Get the preferred title for the anime, following the series title
    /// language preference. AniList only carries the main (transcribed), native and
    /// english titles plus untyped synonyms, so the preference is resolved
    /// against those three.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <returns>The preferred title, or <see langword="null"/> if none was
    /// found and no fallback was requested.</returns>
    public ITitle? GetPreferredTitle(bool useFallback = true)
    {
        var titles = GetAllTitles();
        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return DefaultTitle;

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title is not null)
                return title;
        }

        return useFallback ? FallbackTitle : null;
    }

    /// <summary>
    /// The main title, which AniList treats as the canonical title.
    /// </summary>
    public ITitle DefaultTitle => new TitleStub
    {
        Source = DataSource.AniList,
        Language = MainTitleLanguage,
        LanguageCode = MainTitleLanguage.GetString(),
        Value = MainTitle,
        Type = TitleType.Main,
    };

    /// <summary>
    /// The english title when AniList has one, otherwise the main title.
    /// </summary>
    private ITitle FallbackTitle => !string.IsNullOrEmpty(EnglishTitle)
        ? new TitleStub { Source = DataSource.AniList, Language = TitleLanguage.English, LanguageCode = "en", Value = EnglishTitle, Type = TitleType.Official }
        : DefaultTitle;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime() { }

    /// <summary>
    /// Creates a new AniList anime entry.
    /// </summary>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    public Anilist_Anime(int anilistAnimeId)
    {
        AnilistAnimeID = anilistAnimeId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets all episodes for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Episode> Episodes
        => RepoFactory.Anilist_Episode.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets the synthesized season for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Season> Seasons => [new Anilist_Season(this)];

    /// <summary>
    /// Gets all tags for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Tag> Tags
        => RepoFactory.Anilist_Anime_Tag.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets the external links AniList lists for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_ExternalLink> ExternalLinks
        => RepoFactory.Anilist_Anime_ExternalLink.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// The trailer as a resource, if AniList has one on a site we know how to link.
    /// </summary>
    public Resource? TrailerResource => (TrailerSite?.ToLowerInvariant(), TrailerID) switch
    {
        ("youtube", { Length: > 0 } id) => new() { Type = ResourceType.Trailer, Name = "YouTube", Url = $"https://www.youtube.com/watch?v={id}" },
        ("dailymotion", { Length: > 0 } id) => new() { Type = ResourceType.Trailer, Name = "Dailymotion", Url = $"https://www.dailymotion.com/video/{id}" },
        _ => null,
    };

    /// <summary>
    /// Gets all studios for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Studio> Studios
        => RepoFactory.Anilist_Anime_Studio.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets all character appearances for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Character> Characters
        => RepoFactory.Anilist_Anime_Character.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets all staff roles for this anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Staff> Staff
        => RepoFactory.Anilist_Anime_Staff.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets all relations for this anime, including the ones pointing at manga and novels.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Relation> Relations
        => RepoFactory.Anilist_Anime_Relation.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Get all AniDB/AniList anime cross-references for the anime.
    /// </summary>
    public IReadOnlyList<CrossRef_AniDB_Anilist_Anime> CrossReferences
        => RepoFactory.CrossRef_AniDB_Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Get all AniDB/AniList episode cross-references for the anime.
    /// </summary>
    public IReadOnlyList<CrossRef_AniDB_Anilist_Episode> EpisodeCrossReferences
        => RepoFactory.CrossRef_AniDB_Anilist_Episode.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Get all titles for the anime, including synonyms.
    /// </summary>
    public IReadOnlyList<ITitle> GetAllTitles()
    {
        var titles = new List<ITitle>();
        if (!string.IsNullOrEmpty(MainTitle))
            titles.Add(DefaultTitle);
        if (!string.IsNullOrEmpty(NativeTitle))
            titles.Add(new TitleStub { Source = DataSource.AniList, Language = OriginalLanguageCode.GetTitleLanguage(), LanguageCode = OriginalLanguageCode, Value = NativeTitle, Type = TitleType.Official });
        if (!string.IsNullOrEmpty(EnglishTitle))
            titles.Add(new TitleStub { Source = DataSource.AniList, Language = TitleLanguage.English, LanguageCode = "en", Value = EnglishTitle, Type = TitleType.Official });
        foreach (var synonym in Synonyms)
            titles.Add(new TitleStub { Source = DataSource.AniList, Language = TitleLanguage.Unknown, LanguageCode = "und", Value = synonym, Type = TitleType.Synonym });
        return titles;
    }

    /// <summary>
    /// External resources/links associated with the anime.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>
            {
                new() { Type = ResourceType.Metadata, Name = "AniList", Url = $"https://anilist.co/anime/{AnilistAnimeID}" },
            };
            if (MalID is > 0)
                list.Add(new() { Type = ResourceType.CrossReference, Name = "MyAnimeList", Url = $"https://myanimelist.net/anime/{MalID}" });
            if (TrailerResource is { } trailer)
                list.Add(trailer);
            list.AddRange(ExternalLinks.Select(link => link.ToResource()));
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistAnimeID;

    DataEntityType IMetadata.EntityType => DataEntityType.Anime;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region ISeries Implementation

    IReadOnlyList<string> IAnilistAnime.Genres => Genres;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => CrossReferences
        .Select(xref => xref.AnimeSeries?.AnimeSeriesID)
        .WhereNotNull()
        .Distinct()
        .ToList();

    PartialDateOnly? ISeries.AirDate => FirstAiredAt;

    PartialDateOnly? ISeries.EndDate => LastAiredAt;

    double ISeries.Rating => UserRating / 10.0; // Normalize to 0-10 scale

    int ISeries.RatingVotes => UserVotes;

    bool ISeries.Restricted => IsRestricted;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => CrossReferences
        .Select(xref => xref.AnimeSeries)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries => Relations
        .Where(relation => relation.RelatedIsAnime)
        .Concat(RepoFactory.Anilist_Anime_Relation.GetByRelatedAnilistID(AnilistAnimeID).Select(relation => relation.Reversed))
        .Distinct()
        .OrderBy(relation => relation.AnilistAnimeID)
        .ThenBy(relation => relation.RelatedAnilistID)
        .ThenBy(relation => relation.AbstractRelationType)
        .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences => CrossReferences
        .DistinctBy(xref => xref.AnidbAnimeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByAnimeID(xref.AnidbAnimeID))
        .ToList();

    IReadOnlyList<ISeason> ISeries.Seasons => Seasons;

    IReadOnlyList<IEpisode> ISeries.Episodes => Episodes;

    IReadOnlyList<IVideo> ISeries.Videos => CrossReferences
        .DistinctBy(xref => xref.AnidbAnimeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByAnimeID(xref.AnidbAnimeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .DistinctBy(video => video.VideoLocalID)
        .ToList();

    EpisodeCounts ISeries.EpisodeCounts => new() { Episodes = Episodes.Count };

    #endregion

    #region IAnilistAnime Implementation

    IReadOnlyList<IAnilistTagForAnime> IAnilistAnime.Tags => Tags;

    IReadOnlyList<IAnilistSeason> IAnilistAnime.Seasons => Seasons;

    IReadOnlyList<IAnilistEpisode> IAnilistAnime.Episodes => Episodes;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => PreferredTitle;

    ITitle IWithTitles.DefaultTitle => DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => !string.IsNullOrEmpty(EnglishOverview)
        ? new TextStub
        {
            Source = DataSource.AniList,
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = EnglishOverview,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => ((IWithDescriptions)this).DefaultDescription is { } description ? [description] : [];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(CoverImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniList, CoverImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniList, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    public IImageCrossReference? DefaultBannerImageCrossReference => !string.IsNullOrEmpty(BannerImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniList, BannerImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniList, ImageType = ImageEntityType.Banner }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Characters
        .SelectMany(xref =>
        {
            // We don't want borked cross-references to show up.
            if (xref.Character is not { } character)
                return [];

            // If a role don't have a voice actor then we still want it to show up.
            var creatorXrefs = xref.CreatorCrossReferences;
            if (creatorXrefs is { Count: 0 })
                return [new Anilist_Cast(xref, character, null, () => this)];

            return creatorXrefs.Select(creatorXref => new Anilist_Cast(xref, character, creatorXref.AnilistCreatorID, () => this));
        })
        .ToList();

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Staff
        .Select(xref => new Anilist_Crew(xref, () => this))
        .ToList();

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => Studios;

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => [];

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons => Season is { } season && SeasonYear is { } year
        ? [(year, season)]
        : [];

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt;

    #endregion
}
