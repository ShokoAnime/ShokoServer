using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Server;
using TMDbLib.Objects.General;
using TMDbLib.Objects.People;

using PersonGender = Shoko.Server.Providers.TMDB.PersonGender;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Person
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_PersonID { get; set; }

    /// <summary>
    /// TMDB Person ID for the cast memeber.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// The official(?) English form of the person's name.
    /// </summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>
    /// The english biography, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishBiography { get; set; } = string.Empty;

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
    /// When the metadata was last syncronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    public TMDB_Person() { }

    public TMDB_Person(int personId)
    {
        TmdbPersonID = personId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public void Populate(Person person, TranslationsContainer translations)
    {
        var translation = translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        EnglishName = person.Name;
        // TODO: Waiting for https://github.com/Jellyfin/TMDbLib/pull/444 to close, but we don't need to do anything to the code for it to work afterwards.
        EnglishBiography = translation?.Data.Overview ?? person.Biography;
        IsRestricted = person.Adult;
        BirthDay = person.Birthday.HasValue ? DateOnly.FromDateTime(person.Birthday.Value) : null;
        DeathDay = person.Deathday.HasValue ? DateOnly.FromDateTime(person.Deathday.Value) : null;
        PlaceOfBirth = string.IsNullOrEmpty(person.PlaceOfBirth) ? null : person.PlaceOfBirth;
        LastUpdatedAt = DateTime.Now;
    }

    public TMDB_Overview? GetPreferredBiography(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Person, TmdbPersonID, EnglishBiography, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllBiographies()
    {
        // TODO: Implement this logic once the repositories are added.

        return new List<TMDB_Overview>();
    }

    #endregion
}
