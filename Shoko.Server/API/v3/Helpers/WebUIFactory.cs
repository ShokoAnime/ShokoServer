using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Helpers;

public class WebUIFactory
{
    public WebUI.WebUISeriesExtra GetWebUISeriesExtra(AnimeSeries series)
    {
        var anime = series.AniDB_Anime;
        var animeEpisodes = anime.AniDBEpisodes;
        var runtimeLength = GuessCorrectRuntimeLength(animeEpisodes);
        var cast = Series.GetCast(anime.AnimeID, [CreatorRoleType.Studio, CreatorRoleType.Producer]);
        var season = GetFirstAiringSeason(anime);

        var result = new WebUI.WebUISeriesExtra
        {
            RuntimeLength = runtimeLength,
            FirstAirSeason = season,
            Studios = cast.Where(role => role.RoleName == CreatorRoleType.Studio).Select(role => role.Staff).ToList(),
            Producers = cast.Where(role => role.RoleName == CreatorRoleType.Producer).Select(role => role.Staff).ToList(),
            SourceMaterial = Series.GetTags(anime, TagFilter.Filter.Invert | TagFilter.Filter.Source, excludeDescriptions: true).FirstOrDefault()?.Name ?? "Original Work",
        };
        return result;
    }

    private static string GetFirstAiringSeason(AniDB_Anime anime)
    {
        if (anime.AnimeType is not AnimeType.TVSeries and not AnimeType.Web)
            return null;

        var (year, season) = anime.YearlySeasons.FirstOrDefault();
        return year == 0 ? null : $"{season} {year}";
    }

    private static TimeSpan? GuessCorrectRuntimeLength(IReadOnlyList<AniDB_Episode> episodes)
    {
        // Return early if empty.
        if (episodes == null || episodes.Count == 0)
            return null;

        // Filter the list and return if empty.
        episodes = episodes
            .Where(episode => episode.EpisodeType is EpisodeType.Episode)
            .ToList();
        if (episodes.Count == 0)
            return null;

        // Get the runtime length of the only episode.
        if (episodes.Count == 1)
            return TimeSpan.FromSeconds(episodes[0].LengthSeconds);

        // Get the runtime length of the episode in the middle of the stack.
        var index = (int)Math.Round(episodes.Count / 2d);
        return TimeSpan.FromSeconds(episodes[index].LengthSeconds);
    }

    public WebUI.WebUIGroupExtra GetWebUIGroupExtra(AnimeGroup group, AniDB_Anime anime,
        TagFilter.Filter filter = TagFilter.Filter.None, bool orderByName = false, int tagLimit = 30)
    {
        var result = new WebUI.WebUIGroupExtra
        {
            ID = group.AnimeGroupID,
            Type = anime.AnimeType.ToV3Dto(),
            Rating = new Rating { Source = "AniDB", Value = anime.Rating, MaxValue = 1000, Votes = anime.VoteCount }
        };
        if (anime.AirDate is { } airDate && airDate != DateTime.MinValue)
            result.AirDate = airDate.ToDateOnly();

        if (anime.EndDate is { } endDate && endDate != DateTime.MinValue)
            result.EndDate = endDate.ToDateOnly();

        result.Tags = Series.GetTags(anime, filter, excludeDescriptions: true, orderByName)
            .Take(tagLimit)
            .ToList();

        return result;
    }
}
