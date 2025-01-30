using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Describes relations between two series entries.
/// </summary>
public class SeriesRelation
{
    /// <summary>
    /// The IDs of the series.
    /// </summary>
    public RelationIDs IDs;

    /// <summary>
    /// The IDs of the related series.
    /// </summary>
    public RelationIDs RelatedIDs;

    /// <summary>
    /// The relation between <see cref="SeriesRelation.IDs"/> and <see cref="SeriesRelation.RelatedIDs"/>.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public RelationType Type { get; set; }

    /// <summary>
    /// AniDB, etc.
    /// </summary>
    [Required]
    public string Source { get; set; }

    public SeriesRelation(IRelatedMetadata relation, AnimeSeries series = null,
        AnimeSeries relatedSeries = null)
    {
        series ??= RepoFactory.AnimeSeries.GetByAnimeID(relation.BaseID);
        relatedSeries ??= RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedID);

        IDs = new RelationIDs { AniDB = relation.BaseID, Shoko = series?.AnimeSeriesID };
        RelatedIDs = new RelationIDs { AniDB = relation.RelatedID, Shoko = relatedSeries?.AnimeSeriesID };
        Type = relation.RelationType;
        Source = "AniDB";
    }

    /// <summary>
    /// Relation IDs.
    /// </summary>
    public class RelationIDs
    {
        /// <summary>
        /// The ID of the <see cref="Series"/> entry.
        /// </summary>
        public int? Shoko { get; set; }

        /// <summary>
        /// The ID of the <see cref="Series.AniDB"/> entry.
        /// </summary>
        public int? AniDB { get; set; }
    }
}
