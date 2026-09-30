using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
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
    [Required]
    public RelationIDs IDs { get; set; }

    /// <summary>
    /// The IDs of the related series.
    /// </summary>
    [Required]
    public RelationIDs RelatedIDs { get; set; }

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

    /// <summary>
    /// Whether the relation has been verified.
    /// </summary>
    [Required]
    public bool Verified { get; set; }

    public SeriesRelation(IRelatedMetadata relation, IShokoSeries? series = null,
        IShokoSeries? relatedSeries = null)
    {
        // AniDB relations only, so both ends are AniDB anime IDs.
        var baseID = relation.BaseID.GetNumericID<int>();
        var relatedID = relation.RelatedID.GetNumericID<int>();
        series ??= RepoFactory.AnimeSeries.GetByAnimeID(baseID);
        relatedSeries ??= RepoFactory.AnimeSeries.GetByAnimeID(relatedID);

        IDs = new RelationIDs { AniDB = baseID, Shoko = series?.LocalID };
        RelatedIDs = new RelationIDs { AniDB = relatedID, Shoko = relatedSeries?.LocalID };
        Type = relation.RelationType;
        Source = "AniDB";
        Verified = relation.Verified;
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
