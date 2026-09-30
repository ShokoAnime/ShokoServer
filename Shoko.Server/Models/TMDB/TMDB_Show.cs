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
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using TMDbLib.Objects.TvShows;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Show Database Model.
/// </summary>
public class TMDB_Show : TMDB_Base<int>, IEntityMetadata, ISeries, ITmdbShow, IInlineTextSource
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
    ///   Whether TMDB lists <see cref="EnglishTitle"/> among the show's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishTitleListed { get; set; }

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishOverview"/> among the show's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

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

    /// <summary>
    /// The ordering chosen for the show, of any source, or
    /// <see langword="null"/> for its default one. Set through
    /// <see cref="IMetadataOrderingService.SetPreferredOrdering"/>.
    /// </summary>
    public MetadataGuid? PreferredOrderingID { get; set; }

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
        var translation = show.Translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updates = new[]
        {
            UpdateProperty(PosterPath, show.PosterPath!, v => PosterPath = v),
            UpdateProperty(BackdropPath, show.BackdropPath!, v => BackdropPath = v),
            UpdateProperty(OriginalTitle, show.OriginalName!, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, show.OriginalLanguage!, v => OriginalLanguageCode = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data?.Name) ? translation.Data.Name : show.Name!, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data?.Overview) ? translation.Data.Overview : show.Overview!, v => EnglishOverview = v),
            UpdateProperty(IsRestricted, show.Adult, v => IsRestricted = v),
            UpdateProperty(Genres, show.GetGenres(), v => Genres = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(Keywords, show.Keywords!.Results!.Select(k => k.Name!).ToList(), v => Keywords = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(
                ContentRatings,
                show.ContentRatings!.Results!
                    .Select(rating => new TMDB_ContentRating(rating.Iso_3166_1!, rating.Rating!))
                    .WhereInLanguages(crLanguages?.Append(TitleLanguage.EnglishAmerican).ToHashSet())
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(
                ProductionCountries,
                show.ProductionCountries!
                    .Select(country => new TMDB_ProductionCountry(country.Iso_3166_1!, country.Name!))
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ProductionCountries = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(SeasonCount, show.NumberOfSeasons, v => SeasonCount = v),
            UpdateProperty(AlternateOrderingCount, show.EpisodeGroups?.Results!.Count ?? AlternateOrderingCount, v => AlternateOrderingCount = v),
            UpdateProperty(UserRating, show.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, show.VoteCount, v => UserVotes = v),
            UpdateProperty(FirstAiredAt, show.FirstAirDate?.ToDateOnly(), v => FirstAiredAt = v),
            UpdateProperty(LastAiredAt, !string.IsNullOrEmpty(show.Status) && show.Status.Equals("Ended", StringComparison.InvariantCultureIgnoreCase) && show.LastAirDate.HasValue ? show.LastAirDate?.ToDateOnly(): null, v => LastAiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   show.
    /// </summary>
    /// <returns>The title, or the English one when none is in a preferred language.</returns>
    public ITitle GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this) ?? TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The show's titles: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   show.
    /// </summary>
    /// <returns>The overview, or the English one when none is in a preferred language.</returns>
    public IText GetPreferredOverview()
        => TextAccess.Manager.PreferredOverviewFor(this) ?? TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    /// <summary>
    ///   The show's overviews: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<IText> GetAllOverviews()
        => TextAccess.Manager.ListOverviews(this);

    /// <summary>
    /// Get all TMDB company cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the show.</returns>
    public IReadOnlyList<TMDB_Company_Entity> TmdbCompanyCrossReferences =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(MetadataEntityType.Series, TmdbShowID);

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
    /// The shows TMDB suggests to someone looking at this one, both its
    /// recommendations and its similar titles, best first. Most of them are
    /// not in the collection, so their <c>Suggested</c> is usually
    /// <c>null</c>.
    /// </summary>
    public IReadOnlyList<TMDB_Show_Suggestion> TmdbSuggestions =>
        RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(MetadataEntityType.Series, TmdbShowID)
            .Select(suggestion => new TMDB_Show_Suggestion(suggestion))
            .ToList();

    /// <summary>
    /// The shows in the collection that TMDB suggests this one from.
    /// </summary>
    public IReadOnlyList<TMDB_Show_Suggestion> TmdbSuggestedBy =>
        RepoFactory.TMDB_Suggestion.GetBySuggestedTmdbEntityID(MetadataEntityType.Series, TmdbShowID)
            .Select(suggestion => new TMDB_Show_Suggestion(suggestion))
            .ToList();

    /// <summary>
    ///   External resources/links associated with the show.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>();
            if (TvdbShowID is > 0)
                list.Add(new()
                {
                    Type = ResourceType.CrossReference,
                    Name = "TheTVDB",
                    Url = $"https://www.thetvdb.com/dereferrer/series/{TvdbShowID}",
                    ID = TvdbShowID.Value.ToString(),
                });
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

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
    public IReadOnlyList<TMDB_Show_Crew> Crew =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbShowID(TmdbShowID)
            .GroupBy(cast => new { cast.TmdbPersonID, cast.Department, cast.Job })
            .Select(group =>
            {
                var episodes = group.ToList();
                var firstEpisode = episodes.First();
                var seasonCount = episodes.GroupBy(a => a.TmdbSeasonID).Count();
                return new TMDB_Show_Crew
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
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => [.. FirstAiredAt.GetYearlySeasons(LastAiredAt)];

    /// <summary>
    /// The episode group chosen as the show's ordering, read from the ordering
    /// chosen for the show through <see cref="IMetadataOrderingService"/>.
    /// </summary>
    /// <returns>The episode group, or <see langword="null"/> when the show's
    /// default ordering, or an ordering TMDB does not keep, is chosen.</returns>
    public TMDB_AlternateOrdering? PreferredAlternateOrdering =>
        OrderingLookup.PreferredFor(this) as TMDB_AlternateOrdering;

    /// <summary>
    /// The episode group collection ID of <see cref="PreferredAlternateOrdering"/>,
    /// or <see langword="null"/> when no episode group is chosen.
    /// </summary>
    public string? PreferredAlternateOrderingID =>
        PreferredAlternateOrdering?.TmdbEpisodeGroupCollectionID;

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
    /// Get AniDB/TMDB season cross-references for the show.
    /// </summary>
    /// <returns>The season cross-references.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> SeasonCrossReferences =>
        EpisodeCrossReferences
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    /// <summary>
    /// Get AniDB/TMDB episode cross-references for the show.
    /// </summary>
    /// <returns>The episode cross-references.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> EpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IEntityMetadata Implementation

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Series;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    DateOnly? IEntityMetadata.ReleasedAt => FirstAiredAt;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString());

    int ITmdbShow.TmdbID => TmdbShowID;

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

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(PosterPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, PosterPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    public IImageCrossReference? DefaultBackdropImageCrossReference => !string.IsNullOrEmpty(BackdropPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, BackdropPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Backdrop }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => TmdbStudios;

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => ContentRatings;

    #endregion

    #region IWithTags Implementation

    /// <summary>
    ///   TMDB's genres, then its keywords, as tags.
    /// </summary>
    IReadOnlyList<ITag> IWithTags.Tags => TMDB_Tag.For(Genres, Keywords);

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => CrossSourceID.For("tvdb", MetadataEntityType.Series, TvdbShowID) is { } tvdbID ? [tvdbID] : [];

    #endregion

    #region ISeries Implementation

    IReadOnlyList<INetwork> ISeries.Networks => TmdbNetworks;

    IReadOnlyList<IOrdering> ISeries.Orderings => OrderingLookup.For(this);

    IOrdering ISeries.PreferredOrdering => OrderingLookup.PreferredFor(this);

    IReadOnlyList<IMetadataSeriesCrossReference> ISeries.MetadataSeriesCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(TmdbShowID);

    IReadOnlyList<IMetadataSeasonCrossReference> ISeries.MetadataSeasonCrossReferences => SeasonCrossReferences;

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeries.MetadataEpisodeCrossReferences => EpisodeCrossReferences;

    // A film sits in no show, so nothing links one to a TMDB show.
    IReadOnlyList<IMetadataMovieCrossReference> ISeries.MetadataMovieCrossReferences => [];

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => CrossReferences.Select(xref => xref.AnimeSeries?.AnimeSeriesID).WhereNotNull().Distinct().ToList();

    AnimeType ISeries.Type => AnimeType.TV;

    PartialDateOnly? ISeries.AirDate => PartialDateOnly.FromDateOnly(FirstAiredAt);

    PartialDateOnly? ISeries.EndDate => PartialDateOnly.FromDateOnly(LastAiredAt);

    double ISeries.Rating => UserRating;

    int ISeries.RatingVotes => UserVotes;

    bool ISeries.Restricted => IsRestricted;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => CrossReferences
        .Select(xref => xref.AnimeSeries)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries => [];

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.Suggestions => TmdbSuggestions;

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.SuggestedBy => TmdbSuggestedBy;

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.VideoCrossReferences => CrossReferences
        .DistinctBy(xref => xref.AnidbAnimeID)
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByAnimeID(xref.AnidbAnimeID))
        .ToList();

    IReadOnlyList<ISeason> ISeries.Seasons => TmdbSeasons;

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

    #endregion

    #region ITmdbShow Implementation

    IReadOnlyList<string> ITmdbShow.ProductionCountries => ProductionCountries
        .Select(country => country.CountryCode)
        .Order()
        .ToList();

    IReadOnlyList<string> ITmdbShow.Keywords => Keywords;

    IReadOnlyList<string> ITmdbShow.Genres => Genres;

    IReadOnlyList<ITmdbSeason> ITmdbShow.Seasons => TmdbSeasons;

    IReadOnlyList<ITmdbEpisode> ITmdbShow.Episodes => TmdbEpisodes;

    IReadOnlyList<ITmdbShowSuggestion> ITmdbShow.Suggestions => TmdbSuggestions;

    IReadOnlyList<ITmdbShowSuggestion> ITmdbShow.SuggestedBy => TmdbSuggestedBy;

    IReadOnlyList<ITmdbShowOrderingInformation> ITmdbShow.TmdbOrderings =>
        [new TMDB_Show_DefaultOrdering(this, OrderingLookup.Service), .. TmdbAlternateOrdering];

    #endregion
}

