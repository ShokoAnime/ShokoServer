using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Episode Database Model.
/// </summary>
public class TMDB_Episode : TMDB_Base<int>, IEntityMetadata, IEpisode
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id.
    /// </summary>
    public override int Id => TmdbEpisodeID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Linked TvDB episode ID.
    /// </summary>
    /// <remarks>
    /// Will be <code>null</code> if not linked. Will be <code>0</code> if no
    /// TvDB link is found in TMDB. Otherwise it will be the TvDB episode ID.
    /// </remarks>
    public int? TvdbEpisodeID { get; set; }

    /// <summary>
    /// The default thumbnail path. Used to determine the default thumbnail for the episode.
    /// </summary>
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the episode, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Indicates that the episode should be hidden from view unless explicitly
    /// requested, and should now be used internally at all.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Episode number for default ordering.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Episode run-time in minutes.
    /// </summary>
    public int? RuntimeMinutes
    {
        get => Runtime.HasValue ? (int)Math.Floor(Runtime.Value.TotalMinutes) : null;
        set => Runtime = value.HasValue ? TimeSpan.FromMinutes(value.Value) : null;
    }

    /// <summary>
    /// Episode run-time.
    /// </summary>
    [NotMapped]
    public TimeSpan? Runtime { get; set; }

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
    /// When the episode aired, or when it will air in the future if it's known.
    /// </summary>
    public DateOnly? AiredAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor for NHibernate to work correctly while hydrating the rows
    /// from the database.
    /// </summary>
    public TMDB_Episode() { }

    /// <summary>
    /// Constructor to create a new episode in the provider.
    /// </summary>
    /// <param name="episodeId">The TMDB episode id.</param>
    public TMDB_Episode(int episodeId)
    {
        TmdbEpisodeID = episodeId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="show">The raw TMDB Tv Show object.</param>
    /// <param name="season">The raw TMDB Tv Season object.</param>
    /// <param name="episode">The raw TMDB Tv Episode object.</param>
    /// <param name="translations">The translation container for the Tv Episode object (fetched separately).</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(TvShow show, TvSeason season, TvSeasonEpisode episode, TranslationsContainer? translations)
    {
        var translation = translations?.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(ThumbnailPath, episode.StillPath, v => ThumbnailPath = v),
            // If the translations aren't provided, and we have an English title, then don't update it.
            UpdateProperty(EnglishTitle, translations is null && !string.IsNullOrEmpty(EnglishTitle) ? EnglishTitle : !string.IsNullOrEmpty(translation?.Data.Name) ? translation.Data.Name : episode.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : episode.Overview, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, episode.SeasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeNumber, episode.EpisodeNumber, v => EpisodeNumber = v),
            UpdateProperty(Runtime, episode.Runtime.HasValue ? TimeSpan.FromMinutes(episode.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, episode.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, episode.VoteCount, v => UserVotes = v),
            UpdateProperty(AiredAt, episode.AirDate.HasValue ? DateOnly.FromDateTime(episode.AirDate.Value) : null, v => AiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <returns>The preferred episode title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true)
    {
        var titles = AllTitles;

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new TMDB_Title_Episode { ParentID = TmdbEpisodeID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US" };

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new TMDB_Title_Episode { ParentID = TmdbEpisodeID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US" } : null;
    }

    /// <summary>
    /// Get all episode titles in the preferred episode title languages.
    /// </summary>
    /// <returns>All episode titles in the preferred episode title languages.</returns>
    public IReadOnlyList<TMDB_Title> GetAllPreferredTitles()
    {
        var preferredLanguages = Languages.PreferredEpisodeNamingLanguages;
        return AllTitles
            .WhereInLanguages(preferredLanguages.Select(language => language.Language).Append(TitleLanguage.English).ToHashSet())
            .ToList();
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

        return useFallback ? new TMDB_Overview_Episode { ParentID = TmdbEpisodeID, Value = EnglishOverview, LanguageCode = "en", CountryCode = "US" } : null;
    }

    /// <summary>
    /// Get all titles for the episode.
    /// </summary>
    /// <value>All titles for the episode.</value>
    public virtual IEnumerable<TMDB_Title_Episode> AllTitles { get; set; }

    /// <summary>
    /// Get all overviews for the episode.
    /// </summary>
    /// <value>All overviews for the episode.</value>
    public virtual IEnumerable<TMDB_Overview_Episode> AllOverviews { get; set; }

    [NotMapped]
    public TMDB_Image? DefaultThumbnail => Images.FirstOrDefault(a => a.IsPreferred && a.ImageType == ImageEntityType.Thumbnail);

    /// <summary>
    /// Get all images for the episode
    /// </summary>
    /// <returns>A read-only list of images that are linked to the episode.
    /// </returns>
    [NotMapped]
    public IEnumerable<TMDB_Image> Images => ImageXRefs.OrderBy(a => a.ImageType).ThenBy(a => a.Ordering).Select(a => new
    {
        a.ImageType, Image = a.GetTmdbImage()
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

    public virtual IEnumerable<TMDB_Image_Collection> ImageXRefs { get; set; }

    /// <summary>
    /// Get all images for the episode, or all images for the given
    /// <paramref name="entityType"/> provided for the episode.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the episode.
    /// </returns>
    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImageMetadata> preferredImages) =>
        Images.Where(a => entityType == null || a.ImageType == entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    IImageMetadata? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImageMetadata> IWithImages.GetImages(ImageEntityType? entityType)
        => entityType.HasValue
            ? Images.Where(a => a.ImageType == entityType.Value).ToList()
            : RepoFactory.TMDB_Image.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get all cast members that have worked on this episode.
    /// </summary>
    /// <returns>All cast members that have worked on this episode.</returns>
    public virtual IEnumerable<TMDB_Episode_Cast> Cast { get; set; }

    /// <summary>
    /// Get all crew members that have worked on this episode.
    /// </summary>
    /// <returns>All crew members that have worked on this episode.</returns>
    public virtual IEnumerable<TMDB_Episode_Crew> Crew { get; set; }

    /// <summary>
    /// Get the TMDB season associated with the episode, or null if the season
    /// have been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB season, or null.</returns>
    public virtual TMDB_Season? Season { get; set; }

    /// <summary>
    /// Get the TMDB show associated with the episode, or null if the show have
    /// been purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB show, or null.</returns>
    public virtual TMDB_Show? Show { get; set; }

    /// <summary>
    /// Get all alternate ordering entries for the episode available from the
    /// local database. You need to have alternate orderings enabled in the
    /// settings file for these to be populated.
    /// </summary>
    /// <returns>All alternate ordering entries for the episode.</returns>
    public virtual IEnumerable<TMDB_AlternateOrdering_Episode> TmdbAlternateOrderingEpisodes { get; set; }

    /// <summary>
    /// Get the alternate ordering entry for the episode with the given
    /// <paramref name="id"/>, or null if no such entry exists.
    /// </summary>
    /// <param name="id">The episode group collection ID of the alternate ordering
    /// entry to retrieve.</param>
    /// <returns>The alternate ordering entry associated with the given ID, or
    /// null if no such entry exists.</returns>
    public TMDB_AlternateOrdering_Episode? GetTmdbAlternateOrderingEpisodeById(string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : TmdbAlternateOrderingEpisodes.FirstOrDefault(a => a.TmdbEpisodeGroupCollectionID == id);

    /// <summary>
    /// Get all AniDB/TMDB cross-references for the episode.
    /// </summary>
    /// <returns>A read-only list of AniDB/TMDB cross-references for the episode.</returns>
    [NotMapped]
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> CrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    /// <summary>
    /// Get all file cross-references associated with the episode.
    /// </summary>
    /// <returns>A read-only list of file cross-references associated with the
    /// episode.</returns>
    [NotMapped]
    public IReadOnlyList<SVR_CrossRef_File_Episode> FileCrossReferences =>
        CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Episode;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => AiredAt;

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

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast.ToList();

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew.ToList();

    #endregion

    #region IEpisode

    int IEpisode.SeriesID => TmdbShowID;

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => CrossReferences
        .Select(xref => xref.AnimeEpisode?.AnimeEpisodeID)
        .WhereNotNull()
        .ToList();

    EpisodeType IEpisode.Type => SeasonNumber == 0 ? EpisodeType.Special : EpisodeType.Episode;

    int IEpisode.EpisodeNumber => EpisodeNumber;

    int? IEpisode.SeasonNumber => SeasonNumber;

    TimeSpan IEpisode.Runtime => Runtime ?? TimeSpan.Zero;

    DateTime? IEpisode.AirDate => AiredAt?.ToDateTime();

    ISeries? IEpisode.Series => Show;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => CrossReferences
        .Select(xref => xref.AnimeEpisode)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .ToList();

    IReadOnlyList<IVideo> IEpisode.VideoList => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .ToList();

    #endregion
}
