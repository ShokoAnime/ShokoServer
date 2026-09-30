using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Server.Repositories;

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
    ///   The name the core gives a tag whose name on AniDB doesn't make sense
    ///   or is otherwise confusing, or <c>null</c> when it keeps its own.
    /// </summary>
    /// <remarks>
    ///   Stored as the core's overall preferred title of the tag.
    /// </remarks>
    public string? TagNameOverride
        => RepoFactory.TextCache?.OverallTitleValue(((IMetadata)this).ID, MetadataSource.Shoko);

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

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Tag, TagID.ToString());

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdated.ToUniversalTime();

    #endregion

    #region ITag Implementation

    string ITag.Name => TagName;

    string ITag.Overview => TagDescription;

    #endregion

    #region IAnidbTag Implementation

    int IAnidbTag.AnidbID => TagID;

    MetadataGuid? IAnidbTag.ParentTagID
        => ParentTagID is > 0 ? new(MetadataSource.AniDB, MetadataEntityType.Tag, ParentTagID.Value.ToString()) : null;

    bool IAnidbTag.IsSpoiler => GlobalSpoiler;

    bool IAnidbTag.IsVerified => Verified;

    IAnidbTag? IAnidbTag.ParentTag => ParentTagID is > 0 ? RepoFactory.AniDB_Tag.GetByTagID(ParentTagID.Value) : null;

    IReadOnlyList<IAnidbTag> IAnidbTag.ChildTags => RepoFactory.AniDB_Tag.GetByParentTagID(TagID);

    IReadOnlyList<IAnidbAnime> IAnidbTag.AllAnidbAnime => RepoFactory.AniDB_Anime_Tag.GetByTagID(TagID)
        .OrderBy(a => a.AnimeID)
        .Select(a => a.Anime)
        .WhereNotNull()
        .ToList();

    #endregion
}
