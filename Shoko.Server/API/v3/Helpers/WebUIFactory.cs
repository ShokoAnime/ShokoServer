using System;
using System.Linq;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Helpers;

public class WebUIFactory
{
    private readonly FilterFactory _filterFactory;
    private readonly SeriesFactory _seriesFactory;

    public WebUIFactory(FilterFactory filterFactory, SeriesFactory seriesFactory)
    {
        _filterFactory = filterFactory;
        _seriesFactory = seriesFactory;
    }

    public Models.Shoko.WebUI.WebUISeriesExtra GetWebUISeriesExtra(SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        var cast = _seriesFactory.GetCast(anime.AnimeID, new () { Role.CreatorRoleType.Studio, Role.CreatorRoleType.Producer });

        var result = new Models.Shoko.WebUI.WebUISeriesExtra
        {
            FirstAirSeason = _filterFactory.GetFirstAiringSeasonGroupFilter(anime),
            Studios = cast.Where(role => role.RoleName == Role.CreatorRoleType.Studio).Select(role => role.Staff).ToList(),
            Producers = cast.Where(role => role.RoleName == Role.CreatorRoleType.Producer).Select(role => role.Staff).ToList(),
            SourceMaterial = _seriesFactory.GetTags(anime, TagFilter.Filter.Invert | TagFilter.Filter.Source, excludeDescriptions: true).FirstOrDefault()?.Name ?? "Original Work",
        };
        return result;
    }
    
    public Models.Shoko.WebUI.WebUIGroupExtra GetWebUIGroupExtra(SVR_AnimeGroup group, SVR_AnimeSeries series, SVR_AniDB_Anime anime,
        TagFilter.Filter filter = TagFilter.Filter.None, bool orderByName = false, int tagLimit = 30)
    {
        var result = new Models.Shoko.WebUI.WebUIGroupExtra{
            ID = group.AnimeGroupID,
            Type = SeriesFactory.GetAniDBSeriesType(anime.AnimeType),
            Rating = new Rating { Source = "AniDB", Value = anime.Rating, MaxValue = 1000, Votes = anime.VoteCount }
        };
        if (anime.AirDate != null)
        {
            var airdate = anime.AirDate.Value;
            if (airdate != DateTime.MinValue)
            {
                result.AirDate = airdate;
            }
        }

        if (anime.EndDate != null)
        {
            var enddate = anime.EndDate.Value;
            if (enddate != DateTime.MinValue)
            {
                result.EndDate = enddate;
            }
        }

        result.Tags = _seriesFactory.GetTags(anime, filter, excludeDescriptions: true, orderByName)
            .Take(tagLimit)
            .ToList();

        return result;
    }
}
