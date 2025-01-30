using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.AniDB;

/// <summary>
/// Cast member for an AniDB anime or episode.
/// </summary>
public class AniDB_Cast : ICast
{
    #region Properties

    private readonly AniDB_Anime_Character _xref;

    private readonly AniDB_Character _character;

    private readonly Func<IMetadata<int>?> _getParent;

    public string ID => $"{_xref.AnimeID}-{_xref.CharacterID}-{CreatorID}";

    public int? CreatorID { get; private set; }

    public int CharacterID => _character.CharacterID;

    public int ParentID => _xref.AnimeID;

    public string Name => _character.Name;

    public string? OriginalName => _character.OriginalName;

    public string? Description => _character.Description;

    public int Ordering => _xref.Ordering;

    public CastRoleType RoleType => _xref.AppearanceType switch
    {
        CharacterAppearanceType.Main_Character => CastRoleType.MainCharacter,
        CharacterAppearanceType.Minor_Character => CastRoleType.MinorCharacter,
        CharacterAppearanceType.Background_Character => CastRoleType.BackgroundCharacter,
        CharacterAppearanceType.Cameo => CastRoleType.Cameo,
        _ => CastRoleType.None,
    };

    public IImageMetadata? PortraitImage => _character.GetImageMetadata();

    public IMetadata<int>? Parent => _getParent();

    public AniDB_Character Character => _character;

    public AniDB_Creator? Creator => CreatorID.HasValue
        ? RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID.Value)
        : null;

    #endregion

    #region Constructors

    public AniDB_Cast(AniDB_Anime_Character xref, AniDB_Character character, int? creatorID, Func<IMetadata<int>?> getParent)
    {
        _xref = xref;
        _character = character;
        _getParent = getParent;
        CreatorID = creatorID;
    }

    #endregion

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region ICast Implementation

    int? ICast.CharacterID => CharacterID;

    ICharacter? ICast.Character => _character;

    ICreator? ICast.Creator => Creator;

    #endregion
}

public class AniDB_Cast<TMetadata> : AniDB_Cast, ICast<TMetadata> where TMetadata : IMetadata<int>
{
    public AniDB_Cast(AniDB_Anime_Character xref, AniDB_Character character, int? creatorID, Func<IMetadata<int>?> getParent) : base(xref, character, creatorID, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
