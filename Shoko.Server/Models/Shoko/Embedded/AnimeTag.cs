using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Shoko.Embedded;

public class AnimeTag(CustomTag tag, AnimeSeries series) : IShokoTagForSeries
{
    #region IMetadata Implementation

    public int ID => tag.CustomTagID;

    public DataSource Source => DataSource.User;

    #endregion

    #region ITag Implementation

    public string Name => tag.TagName;

    public string Description => tag.TagDescription;

    #endregion

    #region IShokoTag Implementation

    public IReadOnlyList<IShokoSeries> AllShokoSeries => RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID)
        .Select(xref => RepoFactory.AnimeSeries.GetByAnimeID(xref.CrossRefID))
        .WhereNotNull()
        .ToList();

    #endregion

    #region IShokoTagForSeries Implementation

    public int ShokoSeriesID => series.AnimeSeriesID;

    public IShokoSeries ShokoSeries => series;

    #endregion
}
