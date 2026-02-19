using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Collections;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Movie Collection Database Model.
/// </summary>
public class TMDB_Collection : TMDB_Base<int>, IEntityMetadata, ITmdbCollection
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id
    /// </summary>
    public override int Id => TmdbCollectionID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_CollectionID { get; set; }

    /// <summary>
    /// TMDB Collection ID.
    /// </summary>
    public int TmdbCollectionID { get; set; }

    /// <summary>
    /// The english title of the collection, used as a fallback for when no
    /// title is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of movies in the collection.
    /// </summary>
    public int MovieCount { get; set; }

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
    public TMDB_Collection() { }

    /// <summary>
    /// Constructor to create a new movie collection in the provider.
    /// </summary>
    /// <param name="collectionId">The TMDB movie collection id.</param>
    public TMDB_Collection(int collectionId)
    {
        TmdbCollectionID = collectionId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="collection">The raw TMDB Movie Collection object.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(Collection collection)
    {
        var translation = collection.Translations?.Translations!.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updates = new[]
        {
            UpdateProperty(EnglishTitle, string.IsNullOrEmpty(translation?.Data?.Name) ? collection.Name! : translation.Data.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, string.IsNullOrEmpty(translation?.Data?.Overview) ? collection.Overview! : translation.Data.Overview, v => EnglishOverview = v),
            UpdateProperty(MovieCount, collection.Parts?.Count ?? 0, v => MovieCount = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all movie collection titles if
    /// they're already cached from a previous call to
    /// <seealso cref="GetAllTitles"/>.
    /// </param>
    /// <returns>The preferred movie collection title, or null if no preferred
    /// title was found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true, bool force = false)
    {
        var titles = GetAllTitles(force);

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new(ForeignEntityType.Collection, TmdbCollectionID, EnglishTitle, "en", "US");

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Collection, TmdbCollectionID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the movie collection, so we won't
    /// have to hit the database twice to get all titles _and_ the preferred
    /// title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? _allTitles = null;

    /// <summary>
    /// Get all titles for the movie collection.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie collection titles if
    /// they're already cached from a previous call.</param>
    /// <returns>All titles for the movie collection.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? _allTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID)
        : _allTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID);

    /// <summary>
    /// Get the preferred overview using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all movie collection overviews if they're
    /// already cached from a previous call to
    /// <seealso cref="GetAllOverviews"/>.
    /// </param>
    /// <returns>The preferred movie collection overview, or null if no preferred overview
    /// was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = true, bool force = false)
    {
        var overviews = GetAllOverviews(force);

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Collection, TmdbCollectionID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the movie collection, so we won't have to hit
    /// the database twice to get all overviews _and_ the preferred overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allOverviews = null;

    /// <summary>
    /// Get all overviews for the movie collection.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie collection overviews
    /// if they're already cached from a previous call.</param>
    /// <returns>All overviews for the movie collection.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? _allOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID)
        : _allOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID);

    /// <summary>
    /// Get all images for the movie collection, or all images for the given
    /// <paramref name="entityType"/> provided for the movie collection.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the movie collection.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbCollectionIDAndType(TmdbCollectionID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbCollectionID(TmdbCollectionID);

    /// <summary>
    /// Get all local TMDB movies associated with the movie collection.
    /// </summary>
    /// <returns>The TMDB movies.</returns>
    public IReadOnlyList<TMDB_Movie> GetTmdbMovies() =>
        RepoFactory.TMDB_Movie.GetByTmdbCollectionID(TmdbCollectionID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Collection;

    DataSource IEntityMetadata.DataSource => DataSource.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    string IMetadata<string>.ID => TmdbCollectionID.ToString();

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => GetPreferredTitle()?.Value ?? EnglishTitle;

    ITitle IWithTitles.DefaultTitle => new TitleStub()
    {
        Language = TitleLanguage.EnglishAmerican,
        CountryCode = "US",
        LanguageCode = "en",
        Value = EnglishTitle,
        Source = DataSource.TMDB,
    };

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => new TextStub()
    {
        Language = TitleLanguage.EnglishAmerican,
        CountryCode = "US",
        LanguageCode = "en",
        Value = EnglishOverview,
        Source = DataSource.TMDB,
    };

    IText? IWithDescriptions.PreferredDescription => GetPreferredOverview();

    IReadOnlyList<IText> IWithDescriptions.Descriptions => GetAllOverviews();

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt.ToUniversalTime();

    #endregion

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => entityType.HasValue
            ? RepoFactory.TMDB_Image.GetByTmdbCollectionIDAndType(TmdbCollectionID, entityType.Value)
            : RepoFactory.TMDB_Image.GetByTmdbCollectionID(TmdbCollectionID);

    #endregion
}
