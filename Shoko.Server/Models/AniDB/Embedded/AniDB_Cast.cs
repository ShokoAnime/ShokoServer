using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

/// <summary>
/// Cast member for an AniDB anime or episode.
/// </summary>
public class AniDB_Cast : ICast
{
    #region Properties

    private readonly AniDB_Anime_Character _xref;

    private readonly AniDB_Character _character;

    private readonly Func<IMetadata?> _getParent;

    public string ID => $"{_xref.AnimeID}-{_xref.CharacterID}-{CreatorID}";

    public int? CreatorID { get; private set; }

    public int CharacterID => _character.CharacterID;

    public int ParentID => _xref.AnimeID;

    MetadataGuid ICast.ParentID => new(MetadataSource.AniDB, MetadataEntityType.Series, ParentID.ToString());

    public string Name => _character.Name;

    public string? OriginalName => _character.OriginalName;

    public string? Description => _character.Description;

    public int Ordering => _xref.Ordering;

    public CastRoleType RoleType => _xref.CastRoleType;

    public TitleLanguage Language => RepoFactory.AniDB_Anime.GetByAnimeID(_xref.AnimeID)?.OriginalLanguage ?? TitleLanguage.Unknown;

    public string LanguageCode => Language.GetString();

    public IMetadata? Parent => _getParent();

    public AniDB_Character Character => _character;

    public AniDB_Creator? Creator => CreatorID.HasValue
        ? RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID.Value)
        : null;

    #endregion

    #region Constructors

    public AniDB_Cast(AniDB_Anime_Character xref, AniDB_Character character, int? creatorID, Func<IMetadata?> getParent)
    {
        _xref = xref;
        _character = character;
        _getParent = getParent;
        CreatorID = creatorID;
    }

    #endregion

    #region ICast Implementation

    MetadataSource ICast.Source => MetadataSource.AniDB;

    MetadataGuid? ICast.CreatorID => CreatorID is { } creatorID ? new(MetadataSource.AniDB, MetadataEntityType.Creator, creatorID.ToString()) : null;

    MetadataGuid? ICast.CharacterID => new(MetadataSource.AniDB, MetadataEntityType.Character, CharacterID.ToString());

    ICharacter? ICast.Character => _character;

    ICreator? ICast.Creator => Creator;

    string? ICast.DubGroup => null;

    #endregion
}

public class AniDB_Cast<TMetadata> : AniDB_Cast, ICast<TMetadata> where TMetadata : IMetadata
{
    public AniDB_Cast(AniDB_Anime_Character xref, AniDB_Character character, int? creatorID, Func<IMetadata?> getParent) : base(xref, character, creatorID, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
