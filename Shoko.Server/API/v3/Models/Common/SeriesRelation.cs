using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;

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
        /// Explains how the main entry relates to the related entry.
        /// </summary>
        public enum RelationType
        {
            /// <summary>
            /// The relation between the entries cannot be explained in simple terms.
            /// </summary>
            Other = 0,

            /// <summary>
            /// The entries use the same setting, but follow different stories.
            /// </summary>
            SameSetting = 1,

            /// <summary>
            /// The entries use the same base story, but is set in alternate settings.
            /// </summary>
            AlternativeSetting = 2,

            /// <summary>
            /// The entries tell different stories in different settings but othwerwise shares some character(s).
            /// </summary>
            SharedCharacters = 3,

            /// <summary>
            /// The entries tell the same story in the same settings but are made at different times.
            /// </summary>
            AlternativeVersion = 4,

            /// <summary>
            /// The first story either continues, or expands upon the story of the related entry.
            /// </summary>
            Prequel = 20,

            /// <summary>
            /// The related entry is the main-story for the main entry, which is a side-story.
            /// </summary>
            MainStory = 21,

            /// <summary>
            /// The related entry is a longer version of the summerized events in the main entry.
            /// </summary>
            FullStory = 22,

            /// <summary>
            /// The related entry either continues, or expands upon the story of the main entry.
            /// </summary>
            Sequel = 40,

            /// <summary>
            /// The related entry is a side-story for the main entry, which is the main-story.
            /// </summary>
            SideStory = 41,

            /// <summary>
            /// The related entry summerizes the events of the story in the main entry.
            /// </summary>
            Summary = 42,
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
        public static SeriesRelation.RelationType Reverse(this SeriesRelation.RelationType type)
        {
            return (type) switch
            {
                SeriesRelation.RelationType.Prequel => SeriesRelation.RelationType.Sequel,
                SeriesRelation.RelationType.Sequel => SeriesRelation.RelationType.Prequel,
                SeriesRelation.RelationType.MainStory => SeriesRelation.RelationType.SideStory,
                SeriesRelation.RelationType.SideStory => SeriesRelation.RelationType.MainStory,
                SeriesRelation.RelationType.FullStory => SeriesRelation.RelationType.Summary,
                SeriesRelation.RelationType.Summary => SeriesRelation.RelationType.FullStory,
                _ => type,
            };
        }
    }
}