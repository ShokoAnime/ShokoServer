using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Abstractions.Extensions;

#nullable enable
namespace Shoko.Server.Models.Anilist.Embedded;

/// <summary>
/// Non-persisted cast view over an AniList anime-character relationship and
/// one of its voice actors. Mirrors <c>AniDB_Cast</c>.
/// </summary>
public class Anilist_Cast : ICast
{
    private readonly Anilist_Anime_Character _xref;

    private readonly Anilist_Character _character;

    private readonly Func<IMetadata<int>?> _getParent;

    public string ID => $"{_xref.AnilistAnimeID}-{_xref.AnilistCharacterID}-{CreatorID}";

    public int? CreatorID { get; }

    public int CharacterID => _character.AnilistCharacterID;

    public int ParentID => _xref.AnilistAnimeID;

    public string Name => _character.Name;

    public string? OriginalName => _character.OriginalName;

    public string? Description => _character.Description;

    public int Ordering => _xref.Ordering;

    public CastRoleType RoleType => _xref.CastRoleType;

    /// <summary>
    /// The language the voice actor performs in. AniList lists the cast for
    /// every dub, so this is the creator's language rather than the anime's.
    /// </summary>
    public TitleLanguage Language => Creator?.Language is { Length: > 0 } language ? language.GetTitleLanguage() : TitleLanguage.Unknown;

    public string LanguageCode => Language.GetString();

    public IMetadata<int>? Parent => _getParent();

    public Anilist_Character Character => _character;

    public Anilist_Creator? Creator => CreatorID.HasValue ? RepoFactory.Anilist_Creator.GetByAnilistCreatorID(CreatorID.Value) : null;

    public Anilist_Cast(Anilist_Anime_Character xref, Anilist_Character character, int? creatorID, Func<IMetadata<int>?> getParent)
    {
        _xref = xref;
        _character = character;
        _getParent = getParent;
        CreatorID = creatorID;
    }

    DataSource IMetadata.Source => DataSource.AniList;

    int? ICast.CharacterID => CharacterID;

    ICharacter? ICast.Character => _character;

    ICreator? ICast.Creator => Creator;
}

public class Anilist_Cast<TMetadata> : Anilist_Cast, ICast<TMetadata> where TMetadata : IMetadata<int>
{
    public Anilist_Cast(Anilist_Anime_Character xref, Anilist_Character character, int? creatorID, Func<IMetadata<int>?> getParent)
        : base(xref, character, creatorID, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
