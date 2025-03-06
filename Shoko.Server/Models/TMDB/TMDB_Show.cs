using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Show Database Model.
/// </summary>
public class TMDB_Show : TMDB_Base<int>, IEntityMetadata, ISeries
{
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
    [NotMapped]
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

    /// <summary>
    /// The ID of the preferred alternate ordering to use when not specified in the API.
    /// </summary>
    public string? PreferredAlternateOrderingID { get; set; }

    /// <summary>
    /// Get all titles for the show.
    /// </summary>
    /// <value>All titles for the show.</value>
    public virtual ICollection<TMDB_Title> AllTitles { get; set; }

    /// <summary>
    /// Get all overviews for the show.
    /// </summary>
    /// <value>All overviews for the show.</value>
    public ICollection<TMDB_Overview> AllOverviews { get; set; }

    public virtual ICollection<TMDB_Image_TVShow> ImageXRefs { get; set; }

    /// <summary>
    /// Get all TMDB network cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB network cross-references linked to the show.</returns>
    public virtual ICollection<TMDB_Show_Network> NetworkXRefs { get; set; }

    /// <summary>
    /// Get all TMDB seasons associated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB seasons.</returns>
    public ICollection<TMDB_Season> Seasons { get; set; }

    /// <summary>
    /// Get all TMDB episodes associated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public virtual ICollection<TMDB_Episode> Episodes { get; set; }

    /// <summary>
    /// Get all TMDB company cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the show.</returns>
    public virtual ICollection<TMDB_Company_Show> CompanyXRefs { get; set; }

    /// <summary>
    /// Get all TMDB alternate ordering schemes associated with the show in the
    /// local database. You need alternate ordering to be enabled in the
    /// settings file for these to be populated.
    /// </summary>
    /// <returns>The list of TMDB alternate ordering schemes.</returns>
    public virtual ICollection<TMDB_AlternateOrdering> AlternateOrderings { get; set; }

    /// <summary>
    /// Get the preferred alternate ordering scheme associated with the show in
    /// the local database. You need alternate ordering to be enabled in the
    /// settings file for this to be populated.
    /// </summary>
    /// <returns>The preferred alternate ordering scheme associated with the
    /// show in the local database. <see langword="null"/> if the show does not
    /// have an alternate ordering scheme associated with it.</returns>
    [NotMapped]
    public virtual TMDB_AlternateOrdering? PreferredAlternateOrdering => PreferredAlternateOrderingID == null
        ? null
        : AlternateOrderings.FirstOrDefault(a => a.TmdbEpisodeGroupCollectionID == PreferredAlternateOrderingID);

    [NotMapped]
    public TMDB_Image? DefaultPoster => Images.FirstOrDefault(a => a is { IsPreferred: true, ImageType: ImageEntityType.Poster });

    [NotMapped]
    public TMDB_Image? DefaultBackdrop => Images.FirstOrDefault(a => a is { IsPreferred: true, ImageType: ImageEntityType.Poster });

    /// <summary>
    /// Get all images for the show
    /// </summary>
    [NotMapped]
    public IEnumerable<TMDB_Image> Images => ImageXRefs.OrderBy(a => a.ImageType).ThenBy(a => a.Ordering).Select(a => new
    {
        a.ImageType, Image = a.Image
    }).Where(a => a.Image != null).Select(a => new TMDB_Image
    {
        ImageType = a.ImageType,
        RemoteFileName = a.Image!.RemoteFileName,
        IsEnabled = a.Image.IsEnabled,
        IsPreferred = a.Image.IsPreferred,
        LanguageCode = a.Image.LanguageCode,
        Height = a.Image.Height,
        Width = a.Image.Width,
        TMDB_ImageID = a.Image.TMDB_ImageID,
        UserRating = a.Image.UserRating,
        UserVotes = a.Image.UserVotes
    }).ToList();

    /// <summary>
    /// Get all TMDB companies linked to the show.
    /// </summary>
    /// <returns>All TMDB companies linked to the show.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Company> Companies =>
        CompanyXRefs
            .Select(xref => xref.Company)
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all TMDB studios linked to the show.
    /// </summary>
    /// <returns>All TMDB studios linked to the show.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Studio<TMDB_Show>> Studios =>
        CompanyXRefs
            .Select(xref => xref.Company is { } company ? new TMDB_Studio<TMDB_Show>(company, this) : null)
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all TMDB networks linked to the show.
    /// </summary>
    /// <returns>All TMDB networks linked to the show.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Network> Networks =>
        NetworkXRefs
            .Select(xref => xref.Network)
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all cast members that have worked on this show.
    /// </summary>
    /// <returns>All cast members that have worked on this show.</returns>
    [NotMapped]
    public IReadOnlyList<TMDB_Show_Cast> Cast =>
        Episodes.SelectMany(a => a.Cast)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Cast
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
    [NotMapped]
    public IReadOnlyList<TMDB_Show_Crew> Crew =>
        Episodes.SelectMany(a => a.Crew)
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
    /// Get AniDB/TMDB cross-references for the show.
    /// </summary>
    /// <returns>The cross-references.</returns>
    [NotMapped]
    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> CrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get AniDB/TMDB episode cross-references for the show.
    /// </summary>
    /// <returns>The episode cross-references.</returns>
    [NotMapped]
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> EpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbShowID(TmdbShowID);


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
                    .Select(rating => new TMDB_ContentRating { CountryCode = rating.Iso_3166_1, Rating = rating.Rating})
                    .WhereInLanguages(crLanguages?.Append(TitleLanguage.EnglishAmerican).ToHashSet())
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(
                ProductionCountries,
                show.ProductionCountries
                    .Select(country => new TMDB_ProductionCountry{CountryCode = country.Iso_3166_1, CountryName = country.Name})
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ProductionCountries = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(SeasonCount, show.NumberOfSeasons, v => SeasonCount = v),
            UpdateProperty(AlternateOrderingCount, show.EpisodeGroups?.Results.Count ?? AlternateOrderingCount, v => AlternateOrderingCount = v),
            UpdateProperty(UserRating, show.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, show.VoteCount, v => UserVotes = v),
            UpdateProperty(FirstAiredAt, show.FirstAirDate.HasValue ? DateOnly.FromDateTime(show.FirstAirDate.Value) : null, v => FirstAiredAt = v),
            UpdateProperty(LastAiredAt, !string.IsNullOrEmpty(show.Status) && show.Status.Equals("Ended", StringComparison.InvariantCultureIgnoreCase) && show.LastAirDate.HasValue ? DateOnly.FromDateTime(show.LastAirDate.Value) : null, v => LastAiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <returns>The preferred show title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true)
    {
        var titles = AllTitles;

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new TMDB_Title_TVShow { ParentID = TmdbShowID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"};

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new TMDB_Title_TVShow { ParentID = TmdbShowID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"} : null;
    }

    /// <summary>
    /// Get the preferred overview using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <returns>The preferred episode overview, or null if no preferred
    /// overview was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = true)
    {
        var overviews = AllOverviews;

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new TMDB_Overview_TVShow { ParentID = TmdbShowID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"} : null;
    }

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

    IReadOnlyList<AnimeTitle> IWithTitles.Titles => AllTitles
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

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => AllOverviews
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

    IReadOnlyList<IImageMetadata> IWithImages.GetImages(ImageEntityType? entityType) => Images.ToList();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => Studios;

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

    IReadOnlyList<IEpisode> ISeries.Episodes => Episodes.ToList();

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

    #endregion
}

