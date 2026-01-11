using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.TvShows;

using AnimeSeason = Shoko.Models.Enums.AnimeSeason;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Show Database Model.
/// </summary>
public class TMDB_Show : TMDB_Base<int>, IEntityMetadata, ISeries
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id
    /// </summary>
    public override int Id => TmdbShowID;

    /// <summary>
    /// Local id.
    /// </summary>
    public int TMDB_ShowID { get; }

    /// <summary>
    /// TMDB Show Id.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// Linked TvDB Show ID.
    /// </summary>
    /// <remarks>
    /// Will be <code>null</code> if not linked. Will be <code>0</code> if no
    /// TvDB link is found in TMDB. Otherwise it will be the TvDB Show ID.
    /// </remarks>
    public int? TvdbShowID { get; set; }

    /// <summary>
    /// The default poster path. Used to determine the default poster for the
    /// show.
    /// </summary>
    public string PosterPath { get; set; } = string.Empty;

    /// <summary>
    /// The default backdrop path. Used to determine the default backdrop for
    /// the show.
    /// </summary>
    public string BackdropPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the show, used as a fallback for when no title is
    /// available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Original title in the original language.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original language this show was shot in, just as a title language
    /// enum instead.
    /// </summary>
    public TitleLanguage OriginalLanguage
    {
        get => string.IsNullOrEmpty(OriginalLanguageCode) ? TitleLanguage.None : OriginalLanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// The original language this show was shot in.
    /// </summary>
    public string OriginalLanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates the show is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Genres.
    /// </summary>
    public List<string> Genres { get; set; } = [];

    /// <summary>
    /// Keywords / Tags.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public List<TMDB_ContentRating> ContentRatings { get; set; } = [];

    /// <summary>
    /// Production countries.
    /// </summary>
    public List<TMDB_ProductionCountry> ProductionCountries { get; set; } = [];

    /// <summary>
    /// Number of episodes using the default ordering.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of hidden episodes using the default ordering.
    /// </summary>
    public int HiddenEpisodeCount { get; set; }

    /// <summary>
    /// Number of seasons using the default ordering.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// Number of alternate ordering schemas available for this show.
    /// </summary>
    public int AlternateOrderingCount { get; set; }

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
    /// First aired episode date.
    /// </summary>
    public DateOnly? FirstAiredAt { get; set; }

    /// <summary>
    /// Last aired episode date for the show, or null if the show is still
    /// running.
    /// </summary>
    public DateOnly? LastAiredAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #region Settings

    /// <summary>
    /// The ID of the preferred alternate ordering to use when not specified in the API.
    /// </summary>
    public string? PreferredAlternateOrderingID { get; set; }

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor for NHibernate to work correctly while hydrating the rows
    /// from the database.
    /// </summary>
    public TMDB_Show() { }

    /// <summary>
    /// Constructor to create a new show in the provider.
    /// </summary>
    /// <param name="showId">The TMDB show id.</param>
    public TMDB_Show(int showId)
    {
        TmdbShowID = showId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="show">The raw TMDB Tv Show object.</param>
    /// <param name="crLanguages">Content rating languages.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(TvShow show, HashSet<TitleLanguage>? crLanguages)
    {
        // Don't trust 'show.Name' for the English title since it will fall-back
        // to the original language if there is no title in English.
        var translation = show.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updates = new[]
        {
            UpdateProperty(PosterPath, show.PosterPath, v => PosterPath = v),
            UpdateProperty(BackdropPath, show.BackdropPath, v => BackdropPath = v),
            UpdateProperty(OriginalTitle, show.OriginalName, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, show.OriginalLanguage, v => OriginalLanguageCode = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data.Name) ? translation.Data.Name : show.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : show.Overview, v => EnglishOverview = v),
            UpdateProperty(IsRestricted, show.Adult, v => IsRestricted = v),
            UpdateProperty(Genres, show.GetGenres(), v => Genres = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(Keywords, show.Keywords.Results.Select(k => k.Name).ToList(), v => Keywords = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(
                ContentRatings,
                show.ContentRatings.Results
                    .Select(rating => new TMDB_ContentRating(rating.Iso_3166_1, rating.Rating))
                    .WhereInLanguages(crLanguages?.Append(TitleLanguage.EnglishAmerican).ToHashSet())
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(
                ProductionCountries,
                show.ProductionCountries
                    .Select(country => new TMDB_ProductionCountry(country.Iso_3166_1, country.Name))
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ProductionCountries = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(SeasonCount, show.NumberOfSeasons, v => SeasonCount = v),
            UpdateProperty(AlternateOrderingCount, show.EpisodeGroups?.Results.Count ?? AlternateOrderingCount, v => AlternateOrderingCount = v),
            UpdateProperty(UserRating, show.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, show.VoteCount, v => UserVotes = v),
            UpdateProperty(FirstAiredAt, show.FirstAirDate?.ToDateOnly(), v => FirstAiredAt = v),
            UpdateProperty(LastAiredAt, !string.IsNullOrEmpty(show.Status) && show.Status.Equals("Ended", StringComparison.InvariantCultureIgnoreCase) && show.LastAirDate.HasValue ? show.LastAirDate?.ToDateOnly(): null, v => LastAiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all show titles if they're
    /// already cached from a previous call to <seealso cref="GetAllTitles"/>.
    /// </param>
    /// <returns>The preferred show title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true, bool force = false)
    {
        var titles = GetAllTitles(force);

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new(ForeignEntityType.Show, TmdbShowID, EnglishTitle, "en", "US");

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the show, so we won't have to hit the
    /// database twice to get all titles _and_ the preferred title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? _allTitles = null;

    /// <summary>
    /// Get all titles for the show.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all show titles if they're
    /// already cached from a previous call.</param>
    /// <returns>All titles for the show.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? _allTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID)
        : _allTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    /// <summary>
    /// Get the preferred overview using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all episode overviews if they're
    /// already cached from a previous call to
    /// <seealso cref="GetAllOverviews"/>.
    /// </param>
    /// <returns>The preferred episode overview, or null if no preferred
    /// overview was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = true, bool force = false)
    {
        var overviews = GetAllOverviews(force);

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the show, so we won't have to
    /// hit the database twice to get all overviews _and_ the preferred
    /// overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allOverviews = null;

    /// <summary>
    /// Get all overviews for the show.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all show overviews if they're
    /// already cached from a previous call. </param>
    /// <returns>All overviews for the show.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? _allOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID)
        : _allOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    public TMDB_Image? DefaultPoster => RepoFactory.TMDB_Image.GetByRemoteFileName(PosterPath)?.GetImageMetadata(true, ImageEntityType.Poster);

    public TMDB_Image? DefaultBackdrop => RepoFactory.TMDB_Image.GetByRemoteFileName(BackdropPath)?.GetImageMetadata(true, ImageEntityType.Backdrop);

    /// <summary>
    /// Get all images for the show, or all images for the given
    /// <paramref name="entityType"/> provided for the show.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the show.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbShowIDAndType(TmdbShowID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all images for the show, or all images for the given
    /// <paramref name="entityType"/> provided for the show.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the show.
    /// </returns>
    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImageMetadata> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    /// <summary>
    /// Get all TMDB company cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the show.</returns>
    public IReadOnlyList<TMDB_Company_Entity> TmdbCompanyCrossReferences =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Show, TmdbShowID);

    /// <summary>
    /// Get all TMDB companies linked to the show.
    /// </summary>
    /// <returns>All TMDB companies linked to the show.</returns>
    public IReadOnlyList<TMDB_Company> TmdbCompanies =>
        TmdbCompanyCrossReferences
            .Select(xref => xref.GetTmdbCompany())
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all TMDB studios linked to the show.
    /// </summary>
    /// <returns>All TMDB studios linked to the show.</returns>
    public IReadOnlyList<TMDB_Studio<TMDB_Show>> TmdbStudios =>
        TmdbCompanyCrossReferences
            .Select(xref => xref.GetTmdbCompany() is { } company ? new TMDB_Studio<TMDB_Show>(company, this) : null)
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all TMDB network cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB network cross-references linked to the show.</returns>
    public IReadOnlyList<TMDB_Show_Network> TmdbNetworkCrossReferences =>
        RepoFactory.TMDB_Show_Network.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB networks linked to the show.
    /// </summary>
    /// <returns>All TMDB networks linked to the show.</returns>
    public IReadOnlyList<TMDB_Network> TmdbNetworks =>
        TmdbNetworkCrossReferences
            .Select(xref => xref.GetTmdbNetwork())
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all cast members that have worked on this show.
    /// </summary>
    /// <returns>All cast members that have worked on this show.</returns>
    public IReadOnlyList<TMDB_Show_Cast> Cast =>
        RepoFactory.TMDB_Episode_Cast.GetByTmdbShowID(TmdbShowID)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Cast()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    CharacterName = firstEpisode.CharacterName,
                    Ordering = firstEpisode.Ordering,
                    EpisodeCount = episodes.Count,
                    SeasonCount = seasonCount,
                };
            })
            .OrderBy(crew => crew.Ordering)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all crew members that have worked on this show.
    /// </summary>
    /// <returns>All crew members that have worked on this show.</returns>
    public IReadOnlyList<TMDB_Show_Crew> Crew =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbShowID(TmdbShowID)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Crew()
                {
                    TmdbPersonID = firstEpisode.TmdbPersonID,
                    TmdbShowID = firstEpisode.TmdbShowID,
                    Department = firstEpisode.Department,
                    Job = firstEpisode.Job,
                    EpisodeCount = episodes.Count,
                    SeasonCount = seasonCount,
                };
            })
            .OrderBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID)
            .ToList();

    /// <summary>
    /// Get all yearly seasons the show was released in.
    /// </summary>
    public IEnumerable<(int Year, AnimeSeason Season)> Seasons
        => FirstAiredAt.GetYearlySeasons(LastAiredAt);

    /// <summary>
    /// Get the preferred alternate ordering scheme associated with the show in
    /// the local database. You need alternate ordering to be enabled in the
    /// settings file for this to be populated.
    /// </summary>
    /// <returns>The preferred alternate ordering scheme associated with the
    /// show in the local database. <see langword="null"/> if the show does not
    /// have an alternate ordering scheme associated with it.</returns>
    public TMDB_AlternateOrdering? PreferredAlternateOrdering =>
        string.IsNullOrEmpty(PreferredAlternateOrderingID)
            ? null
            : RepoFactory.TMDB_AlternateOrdering.GetByEpisodeGroupCollectionAndShowIDs(PreferredAlternateOrderingID, TmdbShowID);

    /// <summary>
    /// Get all TMDB alternate ordering schemes associated with the show in the
    /// local database. You need alternate ordering to be enabled in the
    /// settings file for these to be populated.
    /// </summary>
    /// <returns>The list of TMDB alternate ordering schemes.</returns>
    public IReadOnlyList<TMDB_AlternateOrdering> TmdbAlternateOrdering =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB seasons associated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB seasons.</returns>
    public IReadOnlyList<TMDB_Season> TmdbSeasons =>
        RepoFactory.TMDB_Season.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB episodes associated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        RepoFactory.TMDB_Episode.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get AniDB/TMDB cross-references for the show.
    /// </summary>
    /// <returns>The cross-references.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> CrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get AniDB/TMDB episode cross-references for the show.
    /// </summary>
    /// <returns>The episode cross-references.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> EpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Show;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    DateOnly? IEntityMetadata.ReleasedAt => FirstAiredAt;

    #endregion

    #region IMetadata

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    int IMetadata<int>.ID => Id;

    #endregion

    #region IWithTitles

    string IWithTitles.DefaultTitle => EnglishTitle;

    string IWithTitles.PreferredTitle => GetPreferredTitle()!.Value;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles => GetAllTitles()
        .Select(title => new AnimeTitle()
        {
            Language = title.Language,
            LanguageCode = title.LanguageCode,
            Source = DataSourceEnum.TMDB,
            Title = title.Value,
            Type = TitleType.Official,
        })
        .ToList();

    #endregion

    #region IWithDescriptions

    string IWithDescriptions.DefaultDescription => EnglishOverview;

    string IWithDescriptions.PreferredDescription => GetPreferredOverview()!.Value;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => GetAllOverviews()
        .Select(overview => new TextDescription()
        {
            CountryCode = overview.CountryCode,
            Language = overview.Language,
            LanguageCode = overview.LanguageCode,
            Source = DataSourceEnum.TMDB,
            Value = overview.Value,
        })
        .ToList();

    #endregion

    #region IWithImages

    IImageMetadata? IWithImages.GetPreferredImageForType(ImageEntityType entityType) => null;

    IReadOnlyList<IImageMetadata> IWithImages.GetImages(ImageEntityType? entityType) => GetImages(entityType);

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => TmdbStudios;

    #endregion

    #region ISeries

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => CrossReferences.Select(xref => xref.AnimeSeries?.AnimeSeriesID).WhereNotNull().Distinct().ToList();

    AnimeType ISeries.Type => AnimeType.TVSeries;

    DateTime? ISeries.AirDate => FirstAiredAt?.ToDateTime();

    DateTime? ISeries.EndDate => LastAiredAt?.ToDateTime();

    double ISeries.Rating => UserRating;

    bool ISeries.Restricted => IsRestricted;

    IImageMetadata? ISeries.DefaultPoster => DefaultPoster;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => CrossReferences
        .Select(xref => xref.AnimeSeries)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries>> ISeries.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences => CrossReferences
        .DistinctBy(xref => xref.AnidbAnimeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByAnimeID(xref.AnidbAnimeID))
        .ToList();

    IReadOnlyList<IEpisode> ISeries.Episodes => TmdbEpisodes;

    EpisodeCounts ISeries.EpisodeCounts =>
        new()
        {
            Episodes = EpisodeCount,
        };

    IReadOnlyList<IVideo> ISeries.Videos => CrossReferences
        .DistinctBy(xref => xref.AnidbAnimeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByAnimeID(xref.AnidbAnimeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<ITag> Tags => [];
    public IReadOnlyList<ITag> CustomTags => [];

    #endregion

}

