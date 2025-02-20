using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
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
public class TMDB_Collection : TMDB_Base<int>, IEntityMetadata
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
        var translation = collection.Translations?.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updates = new[]
        {
            UpdateProperty(EnglishTitle, string.IsNullOrEmpty(translation?.Data.Name) ? collection.Name : translation.Data.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, string.IsNullOrEmpty(translation?.Data.Overview) ? collection.Overview : translation.Data.Overview, v => EnglishOverview = v),
            UpdateProperty(MovieCount, collection.Parts.Count, v => MovieCount = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <returns>The preferred movie collection title, or null if no preferred
    /// title was found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true)
    {
        var titles = Titles;

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new TMDB_Title_Collection
                {
                    ParentID = TmdbCollectionID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"
                };

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new TMDB_Title_Collection
        {
            ParentID = TmdbCollectionID, Value = EnglishTitle, LanguageCode = "en", CountryCode = "US"
        } : null;
    }

    /// <summary>
    /// Get the preferred overview using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <returns>The preferred movie collection overview, or null if no preferred overview
    /// was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = true)
    {
        var overviews = Overviews;

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new TMDB_Overview_Collection { ParentID = TmdbCollectionID, Value = EnglishOverview, LanguageCode = "en", CountryCode = "US" } : null;
    }

    /// <summary>
    /// Get all titles for the movie collection.
    /// </summary>
    /// <value>All titles for the movie collection.</value>
    public virtual IEnumerable<TMDB_Title> Titles { get; set; }

    /// <summary>
    /// Get all overviews for the movie collection.
    /// </summary>
    /// <value>All overviews for the movie collection.</value>
    public virtual IEnumerable<TMDB_Overview> Overviews { get; set; }

    /// <summary>
    /// Get all images for the movie collection, or all images for the given
    /// </summary>
    /// <value>
    ///     A read-only list of images that are linked to the movie collection.
    /// </value>
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
    });

    public virtual IEnumerable<TMDB_Image_Collection> ImageXRefs { get; set; }

    /// <summary>
    /// Get all local TMDB movies associated with the movie collection.
    /// </summary>
    /// <value>The TMDB movies.</value>
    public virtual IEnumerable<TMDB_Movie> Movies { get; set; }

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Collection;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion
}
