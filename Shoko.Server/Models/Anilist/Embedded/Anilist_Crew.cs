using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Abstractions.Extensions;

#nullable enable
namespace Shoko.Server.Models.Anilist.Embedded;

/// <summary>
/// Non-persisted crew view over an AniList anime-staff relationship. Mirrors
/// <c>AniDB_Crew</c>.
/// </summary>
public class Anilist_Crew : ICrew
{
    private readonly Anilist_Anime_Staff _xref;

    private readonly Func<IMetadata<int>?> _getParent;

    public string ID => $"{_xref.AnilistAnimeID}-{_xref.AnilistCreatorID}-{_xref.Role}";

    public int CreatorID => _xref.AnilistCreatorID;

    public int ParentID => _xref.AnilistAnimeID;

    public string Name => _xref.RoleName;

    public int Ordering => _xref.Ordering;

    public CrewRoleType RoleType => _xref.CrewRoleType;

    /// <summary>
    /// The language the creator works in, as reported by AniList.
    /// </summary>
    /// <summary>
    /// AniList appends the language to dub staff roles ("ADR Director
    /// (English)"); roles without one are the original production staff and
    /// take the anime's original language.
    /// </summary>
    public TitleLanguage Language => _xref.RoleLanguage
        ?? RepoFactory.Anilist_Anime.GetByAnilistAnimeID(_xref.AnilistAnimeID)?.OriginalLanguage
        ?? TitleLanguage.Unknown;

    public string LanguageCode => Language.GetString();

    public IMetadata<int>? Parent => _getParent();

    public Anilist_Creator? Creator => RepoFactory.Anilist_Creator.GetByAnilistCreatorID(CreatorID);

    public Anilist_Crew(Anilist_Anime_Staff xref, Func<IMetadata<int>?> getParent)
    {
        _xref = xref;
        _getParent = getParent;
    }

    DataSource IMetadata.Source => DataSource.AniList;

    ICreator? ICrew.Creator => Creator;
}

public class Anilist_Crew<TMetadata> : Anilist_Crew, ICrew<TMetadata> where TMetadata : IMetadata<int>
{
    public Anilist_Crew(Anilist_Anime_Staff xref, Func<IMetadata<int>?> getParent) : base(xref, getParent) { }

    public TMetadata? ParentOfType => (TMetadata?)Parent;
}
