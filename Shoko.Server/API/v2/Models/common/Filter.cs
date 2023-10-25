using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Filters;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Filter : Filters
{
    public override string type => string.Intern("filter");

    // We need to rethink this
    // There is too much duplicated info.
    // example:
    // groups { { name="the series" air="a date" year="2017" ... series { { name="the series" air="a date" year="2017" ... }, {...} } }
    // my plan is:
    // public List<BaseDirectory> subdirs;
    // structure:
    // subdirs { { type="group" name="the group" ... series {...} }, { type="serie" name="the series" ... eps {...} } }
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<Group> groups { get; set; }

    public Filter()
    {
        art = new ArtCollection();
        groups = new List<Group>();
    }

    internal static Filter GenerateFromGroupFilter(HttpContext ctx, FilterPreset gf, int uid, bool nocast,
        bool notag, int level,
        bool all, bool allpic, int pic, TagFilter.Filter tagfilter, List<IGrouping<int, int>> evaluatedResults = null)
    {
        var groups = new List<Group>();
        var filter = new Filter { name = gf.Name, id = gf.FilterPresetID, size = 0 };
        if (evaluatedResults == null)
        {
            var evaluator = ctx.RequestServices.GetRequiredService<FilterEvaluator>();
            evaluatedResults = evaluator.EvaluateFilter(gf, uid).ToList();
        }

        if (evaluatedResults.Count != 0)
        {
            filter.size = evaluatedResults.Count;

            // Populate Random Art

            List<SVR_AnimeSeries> arts = null;
            var seriesList = evaluatedResults.SelectMany(a => a).Select(RepoFactory.AnimeSeries.GetByID).ToList();
            var groupsList = evaluatedResults.Select(r => RepoFactory.AnimeGroup.GetByID(r.Key)).ToList();
            if (pic == 1)
            {
                arts = seriesList.Where(SeriesHasCompleteArt).Where(a => a != null).ToList();
                if (arts.Count == 0)
                {
                    arts = seriesList.Where(SeriesHasMostlyCompleteArt).Where(a => a != null).ToList();
                }

                if (arts.Count == 0)
                {
                    arts = seriesList;
                }
            }

            if (arts?.Count > 0)
            {
                var rand = new Random();
                var anime = arts[rand.Next(arts.Count)];

                var fanarts = GetFanartFromSeries(anime);
                if (fanarts.Any())
                {
                    var fanart = fanarts[rand.Next(fanarts.Count)];
                    filter.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_FanArt,
                            fanart.TvDB_ImageFanartID)
                    });
                }

                var banners = GetBannersFromSeries(anime);
                if (banners.Any())
                {
                    var banner = banners[rand.Next(banners.Count)];
                    filter.art.banner.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_Banner,
                            banner.TvDB_ImageWideBannerID)
                    });
                }

                filter.art.thumb.Add(new Art
                {
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.AniDB_Cover,
                        anime.AniDB_ID),
                    index = 0
                });
            }

            if (level > 0)
            {
                groups.AddRange(groupsList.Select(ag =>
                    Group.GenerateFromAnimeGroup(ctx, ag, uid, nocast, notag, level - 1, all, filter.id, allpic, pic, tagfilter,
                        evaluatedResults?.FirstOrDefault(a => a.Key == ag.AnimeGroupID)?.ToList())));
            }

            if (groups.Count > 0)
            {
                filter.groups = groups;
            }
        }

        filter.viewed = 0;
        filter.url = APIV2Helper.ConstructFilterIdUrl(ctx, filter.id);

        return filter;
    }

    private static bool SeriesHasCompleteArt(SVR_AnimeSeries series)
    {
        var anime = series?.GetAnime();
        if (anime == null)
        {
            return false;
        }

        var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
        if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID).Any()))
        {
            return false;
        }

        if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID).Any()))
        {
            return false;
        }

        return true;
    }

    private static bool SeriesHasMostlyCompleteArt(SVR_AnimeSeries series)
    {
        var anime = series?.GetAnime();
        if (anime == null)
        {
            return false;
        }

        var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
        if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID).Any()))
        {
            return false;
        }

        return true;
    }

    private static List<TvDB_ImageFanart> GetFanartFromSeries(SVR_AnimeSeries ser)
    {
        var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID).ToList();
        return tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
    }

    private static List<TvDB_ImageWideBanner> GetBannersFromSeries(SVR_AnimeSeries ser)
    {
        var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID).ToList();
        return tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();
    }
}
