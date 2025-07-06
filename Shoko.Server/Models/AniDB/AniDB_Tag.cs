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

public class AniDB_Tag : IAnidbTag
{
    /// <summary>
    /// Local anidb tag id.
    /// </summary>
    public int AniDB_TagID { get; set; }

    /// <summary>
    /// Universal anidb tag id.
    /// </summary>
    public int TagID { get; set; }

    /// <summary>
    /// Universal anidb tag id of the parent tag, if any.
    /// </summary>
    /// <value>An int if the tag has a parent, otherwise null.</value>
    public int? ParentTagID { get; set; }

    /// <summary>
    /// The tag name to use.
    /// </summary>
    public string TagName { get => TagNameOverride ?? TagNameSource; }

    /// <summary>
    /// The original tag name as shown on anidb.
    /// </summary>
    public string TagNameSource { get; set; } = string.Empty;

    /// <summary>
    /// Name override for those tags where the original name doesn't make
    /// sense or is otherwise confusing.
    /// </summary>
    public string? TagNameOverride { get; set; }

    /// <summary>
    /// True if this tag itself is considered as a spoiler, regardless of
    /// which anime it's attached to.
    /// </summary>
    public bool GlobalSpoiler { get; set; }

    /// <summary>
    /// True if the tag has been verified for use by a mod. Unverified tags
    /// are not shown in AniDB's UI except when editing tags.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// The description for the tag, if any.
    /// </summary>
    public string TagDescription { get; set; } = string.Empty;

    /// <summary>
    /// The date (with no time) the tag was last updated at.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => TagID;

    #endregion

    #region IAnidbTag Implementation

    int? IAnidbTag.ParentTagID => ParentTagID;

    string IAnidbTag.Name => TagName;

    string IAnidbTag.Description => TagDescription;

    bool IAnidbTag.IsSpoiler => GlobalSpoiler;

    bool IAnidbTag.IsVerified => Verified;

    DateTime IAnidbTag.LastUpdated => LastUpdated.ToUniversalTime();

    IAnidbTag? IAnidbTag.ParentTag => ParentTagID is > 0 ? RepoFactory.AniDB_Tag.GetByTagID(ParentTagID.Value) : null;

    IReadOnlyList<IAnidbTag> IAnidbTag.ChildTags => RepoFactory.AniDB_Tag.GetByParentTagID(TagID);

    IReadOnlyList<IAnidbAnime> IAnidbTag.AllAnidbAnime => RepoFactory.AniDB_Anime_Tag.GetByTagID(TagID)
        .OrderBy(a => a.AnimeID)
        .Select(a => a.Anime)
        .WhereNotNull()
        .ToList();

    #endregion
}
