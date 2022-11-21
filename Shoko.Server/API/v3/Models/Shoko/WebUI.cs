using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Models.Shoko;

public class WebUI
{
    public class WebUIGroupExtra
    {
        public WebUIGroupExtra(HttpContext context, SVR_AnimeGroup group, SVR_AnimeSeries series, SVR_AniDB_Anime anime,
            TagFilter.Filter filter = TagFilter.Filter.None)
        {
            ID = group.AnimeGroupID;
            Type = Series.GetAniDBSeriesType(anime.AnimeType);
            Rating = new Rating { Source = "AniDB", Value = anime.Rating, MaxValue = 1000, Votes = anime.VoteCount };
            if (anime.AirDate != null)
            {
                var airdate = anime.AirDate.Value;
                if (airdate != DateTime.MinValue)
                {
                    AirDate = airdate;
                }
            }

            if (anime.EndDate != null)
            {
                var enddate = anime.EndDate.Value;
                if (enddate != DateTime.MinValue)
                {
                    EndDate = enddate;
                }
            }

            Tags = Series.GetTags(context, anime, filter, true);
        }

        /// <summary>
        /// Shoko Group ID.
        /// </summary>
        public int ID;

        /// <summary>
        /// Series type.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

        /// <summary>
        /// The overall rating from AniDB.
        /// </summary>
        public Rating Rating { get; set; }

        /// <summary>
        /// First aired date. Anything without an air date is going to be missing a lot of info.
        /// </summary>
        [Required]
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// Last aired date. Will be null if the series is still ongoing.
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Tags for the main series.
        /// </summary>
        /// <value></value>
        public List<Tag> Tags { get; set; }
    }

    public class Input
    {
        public class WebUIGroupViewBody
        {
            /// <summary>
            /// Group IDs to fetch info for.
            /// </summary>
            /// <value></value>
            [Required]
            [MaxLength(100)]
            public HashSet<int> GroupIDs { get; set; }

            /// <summary>
            /// Tag filter.
            /// </summary>
            /// <value></value>
            public TagFilter.Filter TagFilter { get; set; } = 0;
        }
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum WebUIReleaseChannel
{
    Stable = 1,
    Dev = 2,
}
