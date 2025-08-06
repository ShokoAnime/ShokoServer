
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Anidb;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Tag_Abstract(AniDB_Tag tag, AniDB_Anime_Tag xref) : IAnidbTagForAnime
{
    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => tag.TagID;

    #endregion

    #region IAnidbTag Implementation

    int? IAnidbTag.ParentTagID => tag.ParentTagID;

    string IAnidbTag.Name => tag.TagName;

    string IAnidbTag.Description => tag.TagDescription;

    bool IAnidbTag.IsSpoiler => tag.GlobalSpoiler;

    bool IAnidbTag.IsVerified => tag.Verified;

    DateTime IAnidbTag.LastUpdated => tag.LastUpdated.ToUniversalTime();

    IAnidbTag? IAnidbTag.ParentTag => tag.ParentTagID is > 0 ? RepoFactory.AniDB_Tag.GetByTagID(tag.ParentTagID.Value) : null;

    IReadOnlyList<IAnidbTag> IAnidbTag.ChildTags => RepoFactory.AniDB_Tag.GetByParentTagID(tag.TagID);

    IReadOnlyList<IAnidbAnime> IAnidbTag.AllAnidbAnime => RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID)
        .OrderBy(a => a.AnimeID)
        .Select(a => a.Anime)
        .WhereNotNull()
        .ToList();

    #endregion

    #region IAnidbTagForAnime Implementation

    int IAnidbTagForAnime.AnidbAnimeID => xref.AnimeID;

    int IAnidbTagForAnime.Weight => xref.Weight;

    bool IAnidbTagForAnime.IsLocalSpoiler => xref.LocalSpoiler;

    IAnidbAnime IAnidbTagForAnime.AnidbAnime => RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID)
        ?? throw new InvalidOperationException("Unable to find AniDB_Anime with ID " + xref.AnimeID);

    #endregion
}
