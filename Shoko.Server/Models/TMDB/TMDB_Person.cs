using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.People;

using PersonGender = Shoko.Server.Providers.TMDB.PersonGender;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Person Database Model.
/// </summary>
public class TMDB_Person : TMDB_Base<int>, IEntityMetadata
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id.
    /// </summary>
    public override int Id => TmdbPersonID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_PersonID { get; set; }

    /// <summary>
    /// TMDB Person ID for the cast member.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// The official(?) English form of the person's name.
    /// </summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>
    /// The english biography, used as a fallback for when no biography is
    /// available in the preferred language.
    /// </summary>
    public string EnglishBiography { get; set; } = string.Empty;

    /// <summary>
    /// All known aliases for the person.
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// The person's gender, if known.
    /// </summary>
    public PersonGender Gender { get; set; }

    /// <summary>
    /// Indicates that all the works this person have produced or been part of
    /// has been restricted to an age group above the legal age, so pornographic
    /// works.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// The date of birth, if known.
    /// </summary>
    public DateOnly? BirthDay { get; set; }

    /// <summary>
    /// The date of death, if the person is dead and we know the date.
    /// </summary>
    public DateOnly? DeathDay { get; set; }

    /// <summary>
    /// Their place of birth, if known.
    /// </summary>
    public string? PlaceOfBirth { get; set; }

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
    public TMDB_Person() { }

    /// <summary>
    /// Constructor to create a new person in the provider.
    /// </summary>
    /// <param name="personId">The TMDB Person id.</param>
    public TMDB_Person(int personId)
    {
        TmdbPersonID = personId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="person">The raw TMDB Person object.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(Person person)
    {
        var translation = person.Translations?.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(EnglishName, person.Name, v => EnglishName = v),
            UpdateProperty(EnglishBiography, translation?.Data.Overview ?? person.Biography, v => EnglishBiography = v),
            UpdateProperty(Aliases, person.AlsoKnownAs, v => Aliases = v, (a, b) => string.Equals(string.Join("|", a),string.Join("|", b))),
            UpdateProperty(IsRestricted, person.Adult, v => IsRestricted = v),
            UpdateProperty(BirthDay, person.Birthday.HasValue ? DateOnly.FromDateTime(person.Birthday.Value) : null, v => BirthDay = v),
            UpdateProperty(DeathDay, person.Deathday.HasValue ? DateOnly.FromDateTime(person.Deathday.Value) : null, v => DeathDay = v),
            UpdateProperty(PlaceOfBirth, string.IsNullOrEmpty(person.PlaceOfBirth) ? null : person.PlaceOfBirth, v => PlaceOfBirth = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred biography using the preferred episode title
    /// preference from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback biography if no biography was
    /// found in any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all person biographies if
    /// they're already cached from a previous call to
    /// <seealso cref="GetAllBiographies"/>.
    /// </param>
    /// <returns>The preferred person biography, or null if no preferred biography
    /// was found.</returns>
    public TMDB_Overview? GetPreferredBiography(bool useFallback = false, bool force = false)
    {
        var biographies = GetAllBiographies(force);

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var biography = biographies.FirstOrDefault(biography => biography.Language == preferredLanguage.Language);
            if (biography != null)
                return biography;
        }

        return useFallback ? new(ForeignEntityType.Person, TmdbPersonID, EnglishBiography, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all biographies for the person, so we won't have to hit
    /// the database twice to get all biographies _and_ the preferred biography.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allBiographies = null;

    /// <summary>
    /// Get all biographies for the person.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all person biographies if they're
    /// already cached from a previous call.</param>
    /// <returns>All biographies for the person.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllBiographies(bool force = false) => force
        ? _allBiographies = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Person, TmdbPersonID)
        : _allBiographies ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Person, TmdbPersonID);

    /// <summary>
    /// Get all images for the person, or all images for the given
    /// <paramref name="entityType"/> provided for the person.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the person.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbPersonIDAndType(TmdbPersonID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbPersonID(TmdbPersonID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Person;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    string IEntityMetadata.EnglishTitle => EnglishName;

    string IEntityMetadata.EnglishOverview => EnglishBiography;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    // Technically not untrue. Though this is more of a joke mapping than anything.
    DateOnly? IEntityMetadata.ReleasedAt => BirthDay;

    #endregion
}
