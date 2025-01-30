using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.AniDB;

/// <summary>
/// Crew member for an AniDB anime or episode.
/// </summary>
public class AniDB_Crew : ICrew
{
    #region Properties

    private readonly AniDB_Anime_Staff _xref;

    private readonly Func<IMetadata<int>?> _getParent;

    public string ID => $"{_xref.AnimeID}-{_xref.CreatorID}-{_xref.Role}";

    public int CreatorID => _xref.CreatorID;

    public int ParentID => _xref.AnimeID;

    public string Name => _xref.Role;

    public int Ordering => _xref.Ordering;

    public CrewRoleType RoleType => _xref.RoleType switch
    {
        CreatorRoleType.Producer => CrewRoleType.Producer,
        CreatorRoleType.Director => CrewRoleType.Director,
        CreatorRoleType.SeriesComposer => CrewRoleType.SeriesComposer,
        CreatorRoleType.CharacterDesign => CrewRoleType.CharacterDesign,
        CreatorRoleType.Music => CrewRoleType.Music,
        CreatorRoleType.SourceWork => CrewRoleType.SourceWork,
        _ => CrewRoleType.None,
    };

    public IMetadata<int>? Parent => _getParent();

    public AniDB_Creator? Creator => RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID);

    #endregion

    #region Constructors

    public AniDB_Crew(AniDB_Anime_Staff xref, Func<IMetadata<int>?> getParent)
    {
        _xref = xref;
        _getParent = getParent;
    }

    #endregion

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region ICrew Implementation

    ICreator? ICrew.Creator => Creator;

    #endregion
}

public class AniDB_Crew<TMetadata> : AniDB_Crew, ICrew<TMetadata> where TMetadata : IMetadata<int>
{
    public AniDB_Crew(AniDB_Anime_Staff xref, Func<IMetadata<int>?> getParent) : base(xref, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
