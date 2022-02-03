using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common
{
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

        internal new static Filter GenerateFromGroupFilter(HttpContext ctx, SVR_GroupFilter gf, int uid, bool nocast, bool notag, int level,
            bool all, bool allpic, int pic, TagFilter.Filter tagfilter)
        {
            List<Group> groups = new List<Group>();
            Filter filter = new Filter
            {
                name = gf.GroupFilterName,
                id = gf.GroupFilterID,
                size = 0
            };
            if (gf.GroupsIds.ContainsKey(uid))
            {
                HashSet<int> groupsh = gf.GroupsIds[uid];
                if (groupsh.Count != 0)
                {
                    filter.size = groupsh.Count;

                    // Populate Random Art
                    List<SVR_AnimeGroup> groupsList;

                    List<SVR_AnimeSeries> arts = null;
                    if (gf.SeriesIds.ContainsKey(uid))
                    {
                        var seriesList = gf.SeriesIds[uid].Select(RepoFactory.AnimeSeries.GetByID).ToList();
                        groupsList = seriesList.Select(a => a.AnimeGroupID).Distinct()
                            .Select(RepoFactory.AnimeGroup.GetByID).ToList();
                        if (pic == 1)
                        {
                            arts = seriesList.Where(SeriesHasCompleteArt).Where(a => a != null).ToList();
                            if (arts.Count == 0)
                                arts = seriesList.Where(SeriesHasMostlyCompleteArt).Where(a => a != null).ToList();
                            if (arts.Count == 0)
                                arts = seriesList;
                        }
                    }
                    else
                    {
                        groupsList = new List<SVR_AnimeGroup>();
                    }

                    if (arts?.Count > 0)
                    {
                        Random rand = new Random();
                        var anime = arts[rand.Next(arts.Count)];

                        var fanarts = GetFanartFromSeries(anime);
                        if (fanarts.Any())
                        {
                            var fanart = fanarts[rand.Next(fanarts.Count)];
                            filter.art.fanart.Add(new Art
                            {
                                index = 0,
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.TvDB_FanArt,
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
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.TvDB_Banner,
                                    banner.TvDB_ImageWideBannerID)
                            });
                        }

                        filter.art.thumb.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.AniDB_Cover,
                                anime.AniDB_ID),
                            index = 0
                        });
                    }

                    Dictionary<CL_AnimeGroup_User, Group> order = new Dictionary<CL_AnimeGroup_User, Group>();
                    if (level > 0)
                    {
                        foreach (SVR_AnimeGroup ag in groupsList)
                        {
                            Group group =
                                Group.GenerateFromAnimeGroup(ctx, ag, uid, nocast, notag, (level - 1), all,
                                    filter.id, allpic, pic, tagfilter);
                            groups.Add(group);
                            order.Add(ag.GetUserContract(uid), group);
                        }
                    }

                    if (groups.Count > 0)
                    {
                        // Proper Sorting!
                        IEnumerable<CL_AnimeGroup_User> grps = order.Keys;
                        grps = gf.SortCriteriaList.Count != 0
                            ? GroupFilterHelper.Sort(grps, gf)
                            : grps.OrderBy(a => a.GroupName);
                        groups = grps.Select(a => order[a]).ToList();
                        filter.groups = groups;
                    }
                }
            }

            filter.viewed = 0;
            filter.url = APIV2Helper.ConstructFilterIdUrl(ctx, filter.id);

            return filter;
        }

        private static bool SeriesHasCompleteArt(SVR_AnimeSeries series)
        {
            var anime = series?.GetAnime();
            if (anime == null) return false;
            var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
            if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID).Any())) return false;
            if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID).Any())) return false;
            return true;
        }

        private static bool SeriesHasMostlyCompleteArt(SVR_AnimeSeries series)
        {
            var anime = series?.GetAnime();
            if (anime == null) return false;
            var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
            if (!tvdbIDs.Any(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID).Any())) return false;
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
}
