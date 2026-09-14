using System;
using System.Collections.Generic;
using System.Linq;
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

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Character Database Model.
/// </summary>
public class Anilist_Character : Anilist_Base<int>, ICharacter
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_CharacterID { get; set; }

    /// <summary>
    /// AniList Character ID.
    /// </summary>
    public int AnilistCharacterID { get; set; }

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
    /// Gender.
    /// </summary>
    public PersonGender Gender { get; set; }

    /// <summary>
    /// Date of birth, if set.
    /// </summary>
    public PartialDateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Age, as free-form text, if set.
    /// </summary>
    public string? Age { get; set; }

    /// <summary>
    /// Number of users that favorited the character.
    /// </summary>
    public int FavoriteCount { get; set; }

    /// <summary>
    /// When the metadata was last synchronized.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <inheritdoc/>
    public override int Id => AnilistCharacterID;

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Character() { }

    /// <summary>
    /// Creates a new AniList character entry.
    /// </summary>
    /// <param name="anilistCharacterId">The AniList character ID.</param>
    public Anilist_Character(int anilistCharacterId)
    {
        AnilistCharacterID = anilistCharacterId;
        LastUpdatedAt = DateTime.Now;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Get all anime appearances for the character.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Character> AnimeCharacters
        => RepoFactory.Anilist_Anime_Character.GetByAnilistCharacterID(AnilistCharacterID);

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => AnilistCharacterID;

    DataEntityType IMetadata.EntityType => DataEntityType.Character;

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

    #region ICharacter Implementation

    CharacterType ICharacter.Type => CharacterType.Character;

    IEnumerable<ICast<IEpisode>> ICharacter.EpisodeCastRoles => AnimeCharacters
        .GroupBy(xref => xref.AnilistAnimeID)
        .OrderBy(group => group.Key)
        .SelectMany(GetCastForGrouping);

    private IEnumerable<ICast<IEpisode>> GetCastForGrouping(IGrouping<int, Anilist_Anime_Character> group)
    {
        var xrefs = group
            .SelectMany(xref => xref.CreatorCrossReferences is { Count: > 0 } creators
                ? creators.Select(creator => (xref, creatorID: (int?)creator.AnilistCreatorID, creator.Ordering))
                : [(xref, null, 0)])
            .OrderBy(tuple => tuple.xref.Ordering)
            .ThenBy(tuple => tuple.Ordering)
            .ToList();
        var episodes = RepoFactory.Anilist_Episode.GetByAnilistAnimeID(group.Key);
        foreach (var episode in episodes)
            foreach (var (xref, creatorID, _) in xrefs)
                yield return new Anilist_Cast<IEpisode>(xref, this, creatorID, () => episode);
    }

    IEnumerable<ICast<IMovie>> ICharacter.MovieCastRoles => [];

    IEnumerable<ICast<ISeries>> ICharacter.SeriesCastRoles => AnimeCharacters
        .SelectMany(xref => xref.CreatorCrossReferences is { Count: > 0 } creators
            ? creators.Select(creator => new Anilist_Cast<ISeries>(xref, this, creator.AnilistCreatorID, () => xref.Anime))
            : [new Anilist_Cast<ISeries>(xref, this, null, () => xref.Anime)])
        .OrderBy(cast => cast.ParentID)
        .ThenBy(cast => cast.Ordering)
        .ToList();

    #endregion
}
