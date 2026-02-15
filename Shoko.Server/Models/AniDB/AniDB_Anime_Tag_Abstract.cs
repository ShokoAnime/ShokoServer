using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Tag_Abstract(AniDB_Tag tag, AniDB_Anime_Tag xref) : IAnidbTagForAnime
{
    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.AniDB;

    int IMetadata<int>.ID => tag.TagID;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => tag.LastUpdated.ToUniversalTime();

    #endregion

    #region ITag Implementation

    string ITag.Name => tag.TagName;

    string ITag.Description => tag.TagDescription;

    #endregion

    #region IAnidbTag Implementation

    int? IAnidbTag.ParentTagID => tag.ParentTagID;

    bool IAnidbTag.IsSpoiler => tag.GlobalSpoiler;

    bool IAnidbTag.IsVerified => tag.Verified;

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
