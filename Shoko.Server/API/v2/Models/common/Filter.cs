using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Models.Client;
using Shoko.Models.Enums;
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

        internal new static Filter GenerateFromGroupFilter(NancyContext ctx, SVR_GroupFilter gf, int uid, bool nocast, bool notag, int level,
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

                    List<CL_AniDB_Anime> arts = null;
                    if (gf.ApplyToSeries == 1 && gf.SeriesIds.ContainsKey(uid))
                    {
                        var seriesList = gf.SeriesIds[uid].Select(RepoFactory.AnimeSeries.GetByID).ToList();
                        groupsList = seriesList.Select(a => a.AnimeGroupID).Distinct()
                            .Select(RepoFactory.AnimeGroup.GetByID).ToList();
                        if (pic == 1)
                        {
                            arts = seriesList.Where(SeriesHasCompleteArt)
                                .Select(a => a?.GetAnime()?.Contract?.AniDBAnime)
                                .Where(a => a != null).ToList();
                            if (arts.Count == 0)
                                arts = seriesList.Where(SeriesHasMostlyCompleteArt)
                                    .Select(a => a?.GetAnime()?.Contract?.AniDBAnime).Where(a => a != null).ToList();
                            if (arts.Count == 0)
                                arts = seriesList.Select(a => a?.GetAnime()?.Contract?.AniDBAnime).Where(a => a != null)
                                    .ToList();
                        }
                    }
                    else
                    {
                        groupsList = groupsh.Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .ToList();
                        if (pic == 1)
                        {
                            arts = groupsList.Where(GroupHasCompleteArt).Select(GetAnimeContractFromGroup).ToList();
                            if (arts.Count == 0)
                                arts = groupsList.Where(GroupHasMostlyCompleteArt).Select(GetAnimeContractFromGroup)
                                    .ToList();
                            if (arts.Count == 0)
                                arts = groupsList.Where(a => (a.Anime?.Count ?? 0) > 0)
                                    .Select(GetAnimeContractFromGroup)
                                    .ToList();
                        }
                    }

                    if (arts?.Count > 0)
                    {
                        Random rand = new Random();
                        var anime = arts[rand.Next(arts.Count)];

                        if (anime.Fanarts?.Count > 0)
                        {
                            var fanart = anime.Fanarts[rand.Next(anime.Fanarts.Count)];
                            filter.art.fanart.Add(new Art
                            {
                                index = 0,
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, fanart.ImageType,
                                    fanart.AniDB_Anime_DefaultImageID)
                            });
                        }

                        if (anime.Banners?.Count > 0)
                        {
                            var banner = anime.Banners[rand.Next(anime.Banners.Count)];
                            filter.art.banner.Add(new Art()
                            {
                                index = 0,
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, banner.ImageType,
                                    banner.AniDB_Anime_DefaultImageID)
                            });
                        }

                        filter.art.thumb.Add(new Art()
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.AniDB_Cover,
                                anime.AnimeID),
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
            filter.url = APIHelper.ConstructFilterIdUrl(ctx, filter.id);

            return filter;
        }

        private static CL_AniDB_Anime GetAnimeContractFromGroup(SVR_AnimeGroup grp)
        {
            var anime = grp.Anime.OrderBy(a => a.BeginYear)
                .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
                .FirstOrDefault();
            return anime?.Contract.AniDBAnime;
        }

        private static bool GroupHasCompleteArt(SVR_AnimeGroup series)
        {
            var anime = series.Anime.OrderBy(a => a.BeginYear)
                .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
                .FirstOrDefault();
            var fanarts = anime?.Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count <= 0) return false;
            fanarts = anime.Contract.AniDBAnime.Banners;
            if (fanarts == null || fanarts.Count <= 0) return false;
            return true;
        }

        private static bool GroupHasMostlyCompleteArt(SVR_AnimeGroup grp)
        {
            var anime = grp.Anime.OrderBy(a => a.BeginYear)
                .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
                .FirstOrDefault();
            var fanarts = anime?.Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count <= 0) return false;
            fanarts = anime.Contract.AniDBAnime.Banners;
            if (fanarts == null || fanarts.Count <= 0) return false;
            return true;
        }

        private static bool SeriesHasCompleteArt(SVR_AnimeSeries series)
        {
            var anime = series?.GetAnime();
            var fanarts = anime?.Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count <= 0) return false;
            fanarts = anime.Contract.AniDBAnime.Banners;
            if (fanarts == null || fanarts.Count <= 0) return false;
            return true;
        }

        private static bool SeriesHasMostlyCompleteArt(SVR_AnimeSeries series)
        {
            var anime = series?.GetAnime();
            var fanarts = anime?.Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count <= 0) return false;
            fanarts = anime.Contract.AniDBAnime.Banners;
            if (fanarts == null || fanarts.Count <= 0) return false;
            return true;
        }
    }
}