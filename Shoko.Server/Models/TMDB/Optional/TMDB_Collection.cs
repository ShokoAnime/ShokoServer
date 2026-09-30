using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using TMDbLib.Objects.Collections;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Movie Collection Database Model.
/// </summary>
public class TMDB_Collection : TMDB_Base<int>, IEntityMetadata, ITmdbCollection, IInlineTextSource
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
    ///   Whether TMDB lists <see cref="EnglishTitle"/> among the collection's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishTitleListed { get; set; }

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishOverview"/> among the collection's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

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
    ///   The title the user's picks and language settings choose for the
    ///   collection.
    /// </summary>
    /// <returns>The title, or the English one when none is in a preferred language.</returns>
    public ITitle GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this) ?? TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The collection's titles: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   collection.
    /// </summary>
    /// <returns>The overview, or the English one when none is in a preferred language.</returns>
    public IText GetPreferredOverview()
        => TextAccess.Manager.PreferredOverviewFor(this) ?? TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    /// <summary>
    ///   The collection's overviews: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<IText> GetAllOverviews()
        => TextAccess.Manager.ListOverviews(this);
    /// <summary>
    /// Get all local TMDB movies associated with the movie collection.
    /// </summary>
    /// <returns>The TMDB movies.</returns>
    public IReadOnlyList<TMDB_Movie> GetTmdbMovies() =>
        RepoFactory.TMDB_Movie.GetByTmdbCollectionID(TmdbCollectionID);

    #endregion

    #region IEntityMetadata

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Collection;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Collection, TmdbCollectionID.ToString());

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

    #region ITmdbCollection Implementation

    int ITmdbCollection.TmdbID => TmdbCollectionID;

    IReadOnlyList<ITmdbMovie> ITmdbCollection.Movies => GetTmdbMovies();

    #endregion
}
