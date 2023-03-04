using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using AnimeType = Shoko.Models.Enums.AnimeType;

namespace Shoko.Server.API.v3.Models.Shoko;

public class WebUI
{
    public class WebUIGroupExtra
    {
        public WebUIGroupExtra(SVR_AnimeGroup group, SVR_AnimeSeries series, SVR_AniDB_Anime anime,
            TagFilter.Filter filter = TagFilter.Filter.None, bool orderByName = false, int tagLimit = 30)
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

            Tags = Series.GetTags(anime, filter, excludeDescriptions: true, orderByName)
                .Take(tagLimit)
                .ToList();
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

public class WebUISeriesExtra
{
    /// <summary>
    /// The first season this show was aired in.
    /// </summary>
    /// <value></value>
    public Filter FirstAirSeason { get; set; }

    /// <summary>
    /// A pre-filtered list of studios for the show.
    /// </summary>
    public List<Role.Person> Studios { get; set; }

    /// <summary>
    /// A pre-filtered list of producers for the show.
    /// </summary>
    /// <value></value>
    public List<Role.Person> Producers { get; set; }

    /// <summary>
    /// The inferred source material for the series.
    /// </summary>
    public string SourceMaterial { get; set; }

    public WebUISeriesExtra(HttpContext ctx, SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        var cast = Series.GetCast(anime.AnimeID, new () { Role.CreatorRoleType.Studio, Role.CreatorRoleType.Producer });

        FirstAirSeason = GetFirstAiringSeasonGroupFilter(ctx, anime);
        Studios = cast
            .Where(role => role.RoleName == Role.CreatorRoleType.Studio)
            .Select(role => role.Staff)
            .ToList();
        Producers = cast
            .Where(role => role.RoleName == Role.CreatorRoleType.Producer)
            .Select(role => role.Staff)
            .ToList();
        SourceMaterial = Series.GetTags(anime, TagFilter.Filter.Invert | TagFilter.Filter.Source, excludeDescriptions: true)
            .FirstOrDefault()?.Name ?? "Original Work";
    }

    private Filter GetFirstAiringSeasonGroupFilter(HttpContext ctx, SVR_AniDB_Anime anime)
    {
        var type = (AnimeType)anime.AnimeType;
        if (type != AnimeType.TVSeries && type != AnimeType.Web)
            return null;

        var (year, season) = anime.GetSeasons()
            .FirstOrDefault();
        if (year == 0)
            return null;

        var seasonName = $"{season} {year}";
        var seasonsFilterID = RepoFactory.GroupFilter.GetTopLevel()
            .FirstOrDefault(f => f.GroupFilterName == "Seasons").GroupFilterID;
        var firstAirSeason = RepoFactory.GroupFilter.GetByParentID(seasonsFilterID)
            .FirstOrDefault(f => f.GroupFilterName == seasonName);
        if (firstAirSeason == null)
            return null;

        return new Filter(ctx, firstAirSeason);
    } 
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

            /// <summary>
            /// Limits the number of returned tags.
            /// </summary>
            /// <value></value>
            public int TagLimit { get; set; } = 30;

            /// <summary>
            /// Order tags by name (and source) only. Don't use the tag weights.
            /// </summary>
            /// <value></value>
            public bool OrderByName { get; set; } = false;
        }
    }
}
