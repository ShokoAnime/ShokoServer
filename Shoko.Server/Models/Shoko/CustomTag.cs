using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Shoko;

public class CustomTag : IShokoTag
{
    public int CustomTagID { get; set; }

    public string TagName { get; set; } = string.Empty;

    public string TagDescription { get; set; } = string.Empty;

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.User, MetadataEntityType.Tag, CustomTagID.ToString());

    #endregion

    #region ITag Implementation

    string ITag.Name => TagName;

    string ITag.Overview => TagDescription;

    // A user's own tag: a plain tag, in no category, never weighed or marked.
    TagKind ITag.Kind => TagKind.Tag;

    string? ITag.Category => null;

    bool ITag.IsSpoiler => false;

    bool ITag.IsRestricted => false;

    int? ITag.Weight => null;

    #endregion

    #region IShokoTag Implementation

    int IShokoTag.LocalID => CustomTagID;

    IReadOnlyList<IShokoSeries> IShokoTag.AllShokoSeries => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(CustomTagID)
        .Select(xref => RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID))
        .WhereNotNull()
        .ToList();

    #endregion
}
