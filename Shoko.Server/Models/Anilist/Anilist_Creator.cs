using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Anilist.Embedded;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Creator (staff member) Database Model. Covers both voice actors
/// and production staff, the same way AniDB creators do.
/// </summary>
public class Anilist_Creator : Anilist_Base<int>, ICreator
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_CreatorID { get; set; }

    /// <summary>
    /// AniList Staff ID.
    /// </summary>
    public int AnilistCreatorID { get; set; }

    /// <summary>
    /// Full name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Native name, if set.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// Alternative names.
    /// </summary>
    public List<string> AlternativeNames { get; set; } = [];

    /// <summary>
    /// Description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Portrait resource ID relative to the AniList CDN, if set.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// The language the staff member works in, as reported by AniList, e.g.
    /// "Japanese" for a Japanese voice actor.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Primary occupations, as reported by AniList.
    /// </summary>
    public List<string> PrimaryOccupations { get; set; } = [];

    /// <summary>
    /// Gender.
    /// </summary>
    public PersonGender Gender { get; set; }

    /// <summary>
    /// Date of birth, if set.
    /// </summary>
    public PartialDateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Home town, if set.
    /// </summary>
    public string? HomeTown { get; set; }

    /// <summary>
    /// Number of users that favorited the staff member.
    /// </summary>
    public int FavoriteCount { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistCreatorID;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Creator() { }

    /// <summary>
    /// Creates a new AniList creator entry.
    /// </summary>
    /// <param name="anilistCreatorId">The AniList staff ID.</param>
    public Anilist_Creator(int anilistCreatorId)
    {
        AnilistCreatorID = anilistCreatorId;
        LastUpdatedAt = DateTime.Now;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Get all voice-acting roles for the creator.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Character_Creator> Characters
        => RepoFactory.Anilist_Anime_Character_Creator.GetByAnilistCreatorID(AnilistCreatorID);

    /// <summary>
    /// Get all staff roles for the creator.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Staff> Staff
        => RepoFactory.Anilist_Anime_Staff.GetByAnilistCreatorID(AnilistCreatorID);

    /// <summary>
    /// External resources/links associated with the creator.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>
            {
                new() { Type = ResourceType.Metadata, Name = "AniList", Url = $"https://anilist.co/staff/{AnilistCreatorID}" },
            };
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistCreatorID;

    DataEntityType IMetadata.EntityType => DataEntityType.Creator;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt;

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => Description is { Length: > 0 }
        ? new TextStub { Language = TitleLanguage.English, LanguageCode = "en", Value = Description, Source = DataSource.AniList }
        : null;

    IText? IWithDescriptions.PreferredDescription => ((IWithDescriptions)this).DefaultDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => ((IWithDescriptions)this).DefaultDescription is { } description ? [description] : [];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(ImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniList, ImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniList, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region ICreator Implementation

    CreatorType ICreator.Type => CreatorType.Person;

    DateOnly? ICreator.BirthDay => DateOfBirth is { Year: > 0, Month: > 0, Day: > 0 } date ? new DateOnly(date.Year, date.Month.Value, date.Day.Value) : null;

    IEnumerable<ICast<IEpisode>> ICreator.EpisodeCastRoles => Characters
        .GroupBy(xref => xref.AnilistAnimeID)
        .OrderBy(group => group.Key)
        .SelectMany(GetCastForGrouping);

    private IEnumerable<ICast<IEpisode>> GetCastForGrouping(IGrouping<int, Anilist_Anime_Character_Creator> group)
    {
        var xrefs = group
            .Select(xref => (xref, characterXref: xref.CharacterCrossReference, character: xref.Character))
            .Where(tuple => tuple.characterXref is not null && tuple.character is not null)
            .OrderBy(tuple => tuple.characterXref!.Ordering)
            .ThenBy(tuple => tuple.xref.Ordering)
            .ToList();
        var episodes = RepoFactory.Anilist_Episode.GetByAnilistAnimeID(group.Key);
        foreach (var episode in episodes)
            foreach (var (_, characterXref, character) in xrefs)
                yield return new Anilist_Cast<IEpisode>(characterXref!, character!, AnilistCreatorID, () => episode);
    }

    IEnumerable<ICast<IMovie>> ICreator.MovieCastRoles => [];

    IEnumerable<ICast<ISeries>> ICreator.SeriesCastRoles => Characters
        .Select(xref => xref.CharacterCrossReference is { } characterXref && xref.Character is { } character
            ? new Anilist_Cast<ISeries>(characterXref, character, AnilistCreatorID, () => characterXref.Anime)
            : null)
        .WhereNotNull()
        .OrderBy(cast => cast.ParentID)
        .ThenBy(cast => cast.Ordering)
        .ToList();

    IEnumerable<ICrew<IEpisode>> ICreator.EpisodeCrewRoles => Staff
        .GroupBy(xref => xref.AnilistAnimeID)
        .OrderBy(group => group.Key)
        .SelectMany(GetCrewForGrouping);

    private static IEnumerable<ICrew<IEpisode>> GetCrewForGrouping(IGrouping<int, Anilist_Anime_Staff> group)
    {
        var episodes = RepoFactory.Anilist_Episode.GetByAnilistAnimeID(group.Key);
        foreach (var episode in episodes)
            foreach (var xref in group.OrderBy(x => x.Ordering))
                yield return new Anilist_Crew<IEpisode>(xref, () => episode);
    }

    IEnumerable<ICrew<IMovie>> ICreator.MovieCrewRoles => [];

    IEnumerable<ICrew<ISeries>> ICreator.SeriesCrewRoles => Staff
        .Select(xref => new Anilist_Crew<ISeries>(xref, () => xref.Anime))
        .OrderBy(crew => crew.ParentID)
        .ThenBy(crew => crew.Ordering)
        .ToList();

    #endregion
}
