using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

/// <summary>
/// Crew member for an AniDB anime or episode.
/// </summary>
public class AniDB_Crew : ICrew
{
    #region Properties

    private readonly AniDB_Anime_Staff _xref;

    private readonly Func<IMetadata?> _getParent;

    public string ID => $"{_xref.AnimeID}-{_xref.CreatorID}-{_xref.Role}";

    public int CreatorID => _xref.CreatorID;

    public int ParentID => _xref.AnimeID;

    MetadataGuid ICrew.ParentID => new(MetadataSource.AniDB, MetadataEntityType.Series, ParentID.ToString());

    public string Name => _xref.Role;

    public int Ordering => _xref.Ordering;

    public CrewRoleType RoleType => _xref.CrewRoleType;

    public TitleLanguage Language => RepoFactory.AniDB_Anime.GetByAnimeID(_xref.AnimeID)?.OriginalLanguage ?? TitleLanguage.Unknown;

    public string LanguageCode => Language.GetString();

    public IMetadata? Parent => _getParent();

    public AniDB_Creator? Creator => RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID);

    #endregion

    #region Constructors

    public AniDB_Crew(AniDB_Anime_Staff xref, Func<IMetadata?> getParent)
    {
        _xref = xref;
        _getParent = getParent;
    }

    #endregion

    #region ICrew Implementation

    MetadataSource ICrew.Source => MetadataSource.AniDB;

    MetadataGuid ICrew.CreatorID => new(MetadataSource.AniDB, MetadataEntityType.Creator, CreatorID.ToString());

    ICreator? ICrew.Creator => Creator;

    #endregion
}

public class AniDB_Crew<TMetadata> : AniDB_Crew, ICrew<TMetadata> where TMetadata : IMetadata
{
    public AniDB_Crew(AniDB_Anime_Staff xref, Func<IMetadata?> getParent) : base(xref, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
