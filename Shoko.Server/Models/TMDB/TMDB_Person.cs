using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;
using TMDbLib.Objects.People;

using PersonGender = Shoko.Abstractions.Metadata.Enums.PersonGender;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Person Database Model.
/// </summary>
public class TMDB_Person : TMDB_Base<int>, IEntityMetadata, ICreator, IInlineTextSource
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
    ///   Whether TMDB lists <see cref="EnglishBiography"/> among the person's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

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
    ///   Linked Imdb person ID.
    /// </summary>
    /// <remarks>
    ///   Will be <c>null</c> if not linked or not fetched. Otherwise,
    ///   it will be the Imdb person ID.
    /// </remarks>
    public string? ImdbPersonID { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// When the person was last linked to a crew or cast role in the local system.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

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
        var translation = person.Translations?.Translations!.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(EnglishName, person.Name!, v => EnglishName = v),
            UpdateProperty(EnglishBiography, !string.IsNullOrEmpty(translation?.Data?.Overview) ? translation.Data.Overview : person.Biography!, v => EnglishBiography = v),
            UpdateProperty(IsRestricted, person.Adult, v => IsRestricted = v),
            UpdateProperty(BirthDay, person.Birthday?.ToDateOnly(), v => BirthDay = v),
            UpdateProperty(DeathDay, person.Deathday?.ToDateOnly(), v => DeathDay = v),
            UpdateProperty(PlaceOfBirth, string.IsNullOrEmpty(person.PlaceOfBirth) ? null : person.PlaceOfBirth, v => PlaceOfBirth = v),
            UpdateProperty(ImdbPersonID, string.IsNullOrEmpty(person.ExternalIds?.ImdbId) ? null : person.ExternalIds.ImdbId, v => ImdbPersonID = v),
        };

        return updates.Any(updated => updated);
    }

    /// <summary>
    ///   External resources/links associated with the person.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>();
            if (!string.IsNullOrEmpty(ImdbPersonID))
                list.Add(new() { Type = ResourceType.CrossReference, Name = "IMDb", Url = $"https://www.imdb.com/name/{ImdbPersonID}/" });
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    #endregion

    #region IEntityMetadata Implementation

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Creator;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    string IEntityMetadata.EnglishTitle => EnglishName;

    string IEntityMetadata.EnglishOverview => EnglishBiography;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    // Technically not untrue. Though this is more of a joke mapping than anything.
    DateOnly? IEntityMetadata.ReleasedAt => BirthDay;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Creator, TmdbPersonID.ToString());

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(MetadataSource.TMDB, EnglishName, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => TmdbInlineText.Overview(EnglishBiography);

    InlineTextPlacement IInlineTextSource.InlineOverviewPlacement => TmdbInlineText.Placement(EnglishOverviewListed);

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => TmdbInlineText.OverviewOrEmpty(EnglishBiography);

    IText? IWithOverviews.PreferredOverview => TextAccess.Manager.PreferredOverviewFor(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => TextAccess.Manager.ListOverviews(this);

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region ICreator Implementation

    string ICreator.Name => EnglishName;

    string? ICreator.OriginalName => null;

    CreatorType ICreator.Type => CreatorType.Person;

    IReadOnlyList<ITitle> ICreator.AlternativeNames => TextAccess.Manager.AlternativeNamesOf(this);

    FuzzyDateOnly? ICreator.BirthDay => BirthDay is { } birthDay ? new(birthDay) : null;

    FuzzyDateOnly? ICreator.DeathDay => DeathDay is { } deathDay ? new(deathDay) : null;

    IEnumerable<ICast<IEpisode>> ICreator.EpisodeCastRoles =>
        RepoFactory.TMDB_Episode_Cast.GetByTmdbPersonID(TmdbPersonID);

    IEnumerable<ICast<IMovie>> ICreator.MovieCastRoles =>
        RepoFactory.TMDB_Movie_Cast.GetByTmdbPersonID(TmdbPersonID);

    IEnumerable<ICast<ISeries>> ICreator.SeriesCastRoles =>
        RepoFactory.TMDB_Episode_Cast.GetByTmdbPersonID(TmdbPersonID)
            .GroupBy(cast => new { cast.TmdbShowID, cast.TmdbPersonID, cast.CharacterName, cast.IsGuestRole })
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
            .OrderBy(cast => cast.TmdbShowID)
            .ThenBy(cast => cast.Ordering)
            .ThenBy(cast => cast.TmdbPersonID);

    IEnumerable<ICrew<IEpisode>> ICreator.EpisodeCrewRoles =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbPersonID(TmdbPersonID);

    IEnumerable<ICrew<IMovie>> ICreator.MovieCrewRoles =>
        RepoFactory.TMDB_Movie_Crew.GetByTmdbPersonID(TmdbPersonID);

    IEnumerable<ICrew<ISeries>> ICreator.SeriesCrewRoles =>
        RepoFactory.TMDB_Episode_Crew.GetByTmdbPersonID(TmdbPersonID)
            .GroupBy(cast => new { cast.TmdbShowID, cast.TmdbPersonID, cast.Department, cast.Job })
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
            .OrderBy(crew => crew.TmdbShowID)
            .ThenBy(crew => crew.Department)
            .ThenBy(crew => crew.Job)
            .ThenBy(crew => crew.TmdbPersonID);

    #endregion
}
