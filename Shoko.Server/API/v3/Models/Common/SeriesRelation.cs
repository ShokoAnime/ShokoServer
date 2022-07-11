using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Server.API.v3.Models.Common
{
    /// <summary>
    /// Describes relations between two series entries.
    /// </summary>
    public class SeriesRelation
    {
        /// <summary>
        /// Relation IDs.
        /// </summary>
        public RelationIDs IDs;

        /// <summary>
        /// The relation between <see cref="RelationIDs.Series"/> and <see cref="RelationIDs.RelatedSeries"/>.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public RelationType Type { get; set; }

        /// <summary>
        /// AniDB, etc.
        /// </summary>
        [Required]
        public string Source { get; set; }

        public SeriesRelation(HttpContext context, AniDB_Anime_Relation relation)
        {
            IDs = new RelationIDs { ID = relation.AniDB_Anime_RelationID, Series = relation.AnimeID, RelatedSeries = relation.RelatedAnimeID };
            Type = GetRelationTypeFromAnidbRelationType(relation.RelationType);
            Source = "AniDB";
        }

        public SeriesRelation(HttpContext context, AniDB_Anime_Relation relation, AnimeSeries series, AnimeSeries relatedSeries)
        {
            IDs = new RelationIDs { ID = relation.AniDB_Anime_RelationID, Series = series.AnimeSeriesID, RelatedSeries = relatedSeries.AnimeSeriesID };
            Type = GetRelationTypeFromAnidbRelationType(relation.RelationType);
            Source = "AniDB";
        }

        internal static RelationType GetRelationTypeFromAnidbRelationType(string anidbType)
        {
            return (anidbType.ToLowerInvariant()) switch
            {
                "prequel" => RelationType.Prequel,
                "sequel" => RelationType.Sequel,
                "parent story" => RelationType.MainStory,
                "side story" => RelationType.SideStory,
                "full story" => RelationType.FullStory,
                "summary" => RelationType.Summary,
                "other" => RelationType.Other,
                "alternative setting" => RelationType.AlternativeSetting,
                "alternative version" => RelationType.AlternativeVersion,
                "same setting" => RelationType.SameSetting,
                "character" => RelationType.SharedCharacters,
                
                _ => RelationType.Other,
            };
        }

        /// <summary>
        /// Relation IDs.
        /// </summary>
        public class RelationIDs : IDs
        {
            /// <summary>
            /// The ID of the main entry in this relation.
            /// </summary>
            public int Series { get; set; }

            /// <summary>
            /// The ID of the related entry in this relation.
            /// </summary>
            public int RelatedSeries { get; set; }
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
                _ => type,
            };
        }
    }
}