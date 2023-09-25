using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
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

    public SeriesRelation(HttpContext context, AniDB_Anime_Relation relation, AnimeSeries series = null,
        AnimeSeries relatedSeries = null)
    {
        if (series == null)
        {
            series = RepoFactory.AnimeSeries.GetByAnimeID(relation.AnimeID);
        }

        if (relatedSeries == null)
        {
            relatedSeries = RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedAnimeID);
        }

        IDs = new RelationIDs { AniDB = relation.AnimeID, Shoko = series?.AnimeSeriesID };
        RelatedIDs = new RelationIDs { AniDB = relation.RelatedAnimeID, Shoko = relatedSeries?.AnimeSeriesID };
        Type = ((IRelatedAnime)relation).RelationType;
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

public static class RelationExtensions
{
    /// <summary>
    /// Reverse the relation.
    /// </summary>
    /// <param name="type">The relation to reverse.</param>
    /// <returns>The reversed relation.</returns>
    public static RelationType Reverse(this RelationType type)
    {
        return type switch
        {
            RelationType.Prequel => RelationType.Sequel,
            RelationType.Sequel => RelationType.Prequel,
            RelationType.MainStory => RelationType.SideStory,
            RelationType.SideStory => RelationType.MainStory,
            RelationType.FullStory => RelationType.Summary,
            RelationType.Summary => RelationType.FullStory,
            _ => type
        };
    }
}
