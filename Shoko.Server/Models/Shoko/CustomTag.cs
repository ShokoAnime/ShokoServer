using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class CustomTag : IShokoTag
{
    public int CustomTagID { get; set; }

    public string TagName { get; set; } = string.Empty;

    public string TagDescription { get; set; } = string.Empty;

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.User;

    int IMetadata<int>.ID => CustomTagID;

    #endregion

    #region ITag Implementation

    string ITag.Name => TagName;

    string ITag.Description => TagDescription;

    #endregion

    #region IShokoTag Implementation

    IReadOnlyList<IShokoSeries> IShokoTag.AllShokoSeries => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(CustomTagID)
        .Select(xref => RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID))
        .WhereNotNull()
        .ToList();

    #endregion
}
