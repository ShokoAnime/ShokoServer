using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.People;

using PersonGender = Shoko.Server.Providers.TMDB.PersonGender;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Person Database Model.
/// </summary>
public class TMDB_Person : TMDB_Base<int>, IEntityMetadata, ICreator
{
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
    /// The date of death, if the person is dead, and we know the date.
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

    /// <summary>
    /// Get all biographies for the person.
    /// </summary>
    /// <value>All biographies for the person.</value>
    public virtual ICollection<TMDB_Overview> AllBiographies { get; set; }

    /// <summary>
    /// Gets all image xrefs, which can be used to get all images for the person
    /// </summary>
    public virtual ICollection<TMDB_Image_Person> ImageXRefs { get; set; }

    public virtual ICollection<TMDB_Episode_Cast> EpisodeRoles { get; set; }

    public virtual ICollection<TMDB_Movie_Cast> MovieRoles { get; set; }

    public virtual ICollection<TMDB_Episode_Crew> EpisodeCrew { get; set; }

    public virtual ICollection<TMDB_Movie_Crew> MovieCrew { get; set; }

    /// <summary>
    /// Get all images for the person
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
    });

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
            UpdateProperty(EnglishBiography, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : person.Biography, v => EnglishBiography = v),
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
    /// <returns>The preferred person biography, or null if no preferred biography
    /// was found.</returns>
    public TMDB_Overview? GetPreferredBiography(bool useFallback = false)
    {
        var biographies = AllBiographies;

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var biography = biographies.GetByLanguage(preferredLanguage.Language);
            if (biography != null)
                return biography;
        }

        return useFallback ? new TMDB_Overview_Person { ParentID = TmdbPersonID, Value = EnglishBiography,LanguageCode = "en", CountryCode = "US"} : null;
    }

    #region IEntityMetadata Implementation

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

    #region IMetadata Implementation

    int IMetadata<int>.ID => TmdbPersonID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    #endregion

    #region IWithDescriptions Implementation

    string IWithDescriptions.DefaultDescription => EnglishBiography;

    string IWithDescriptions.PreferredDescription => GetPreferredBiography(useFallback: true)!.Value;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => throw new NotImplementedException();

    #endregion

    #region IWithPortraitImage Implementation

    IImageMetadata? IWithPortraitImage.PortraitImage => Images.FirstOrDefault(a => a.ImageType == ImageEntityType.Person);

    #endregion

    #region ICreator Implementation

    string ICreator.Name => EnglishName;

    string? ICreator.OriginalName => null;

    CreatorType ICreator.Type => CreatorType.Person;

    IEnumerable<ICast<IEpisode>> ICreator.EpisodeCastRoles => EpisodeRoles;

    IEnumerable<ICast<IMovie>> ICreator.MovieCastRoles => MovieRoles;

    IEnumerable<ICast<ISeries>> ICreator.SeriesCastRoles => EpisodeRoles
            .GroupBy(cast => new { cast.TmdbShowID, cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
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
            .OrderBy(cast => cast.TmdbShowID)
            .ThenBy(cast => cast.Ordering)
            .ThenBy(cast => cast.TmdbPersonID);

    IEnumerable<ICrew<IEpisode>> ICreator.EpisodeCrewRoles => EpisodeCrew;

    IEnumerable<ICrew<IMovie>> ICreator.MovieCrewRoles => MovieCrew;

    IEnumerable<ICrew<ISeries>> ICreator.SeriesCrewRoles => EpisodeCrew
            .GroupBy(cast => new { cast.TmdbShowID, cast.TmdbPersonID, cast.Department, cast.Job })
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
            .OrderBy(crew => crew.TmdbShowID)
            .ThenBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID);

    #endregion
}
