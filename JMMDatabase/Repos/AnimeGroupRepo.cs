using System;
using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMModels.ClientExtensions;
using JMMModels.Extensions;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class AnimeGroupRepo : BaseRepo<AnimeGroup>
    {
        public Dictionary<string, HashSet<string>> GroupAnimeSeries { get; set; } = new Dictionary<string, HashSet<string>>();
        public Dictionary<string, HashSet<string>> GroupSubGroups { get; set; } = new Dictionary<string, HashSet<string>>();
        public Dictionary<string, string> GroupParent { get; set; } = new Dictionary<string, string>();

        public HashSet<string> GetAnimeSeriesIds(string groupid)
        {
            return GroupAnimeSeries.Find(groupid);
        }
        public List<AnimeSerie> GetAnimeSeries(string groupid)
        {
            return GroupAnimeSeries.Find(groupid).SelectOrDefault(a=>Store.AnimeSerieRepo.Find(a)).ToList();
        }

        public HashSet<string> GetSubGroupsIds(string groupid)
        {
            return GroupSubGroups.Find(groupid);
        }
        public List<AnimeGroup> GetSubGroups(string groupid)
        {
            return GroupSubGroups.Find(groupid).SelectOrDefault(a=>Items.Find(a)).ToList();
        }


        internal override void InternalSave(AnimeGroup grp, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[grp.Id] = grp;
            if ((type & UpdateType.Properties) > 0)
                UpdateProperties(grp);
            if ((type & UpdateType.User) > 0)
                UpdateUserStats(grp);
            if ((type & (UpdateType.User | UpdateType.GroupFilter)) > 0)
                UpdateGroupFilters(grp);
            if ((type & UpdateType.ParentGroup) > 0)
                UpdateParentGroups(grp, s);
            s.Store(grp);
        }

        private void UpdateParentGroups(AnimeGroup g, IDocumentSession session)
        {

            if (GroupParent.ContainsKey(g.Id))
            {
                string original = GroupParent[g.Id];
                GroupParent[g.Id] = g.ParentId;
                if (original != g.ParentId)
                {
                    if (original != null)
                    {
                        HashSet<string> k = GroupSubGroups[original];
                        if (k.Contains(g.Id))
                            k.Remove(g.Id);
                        Update(original,session);
                    }
                }
            }
            else
                GroupParent[g.Id] = g.ParentId;
            if (g.ParentId != null)
            {
                HashSet<string> k2 = GroupSubGroups[g.ParentId];
                if (!k2.Contains(g.Id))
                    k2.Add(g.Id);
                Update(g.ParentId,session);
            }
        }
        internal override void InternalDelete(AnimeGroup grp, IDocumentSession session)
        {
            grp.SubGroupsIDs.Select(a => Store.AnimeGroupRepo.Find(a)).ToList().ForEach(a =>
            {
                a.ParentId = null;
                Save(a,session, UpdateType.ParentGroup);
            });
            grp.AnimeSerieIDs.Select(a => Store.AnimeSerieRepo.Find(a)).ToList().ForEach(a =>
            {
                a.GroupId = null;
                Store.AnimeSerieRepo.Save(a, session, UpdateType.ParentGroup);
            });
            session.Delete(grp);
            GroupAnimeSeries.Remove(grp.Id);
            GroupParent.Remove(grp.Id);
            Items.Remove(grp.Id);
            if (grp.ParentId != null)
            {
                HashSet<string> s = GroupSubGroups[grp.ParentId];
                s.Remove(grp.Id);
                Update(grp.ParentId,session);
            }
            session.Delete(grp);
        }



        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<AnimeGroup>().ToDictionary(a => a.Id, a => a);
            GroupAnimeSeries = Items.ToDictionary(a => a.Key, a => Store.AnimeSerieRepo.GetAll().Where(b => a.Value.AnimeSerieIDs.Contains(b.Id)).Select(c => c.Id).ToHashSet());
            GroupSubGroups = Items.ToDictionary(a => a.Key, a => Items.Where(b => a.Value.SubGroupsIDs.Contains(b.Key)).Select(c => c.Value.Id).ToHashSet());
            GroupParent = Items.ToDictionary(a => a.Key, a => a.Value.ParentId);

        }









        internal static float? UserVotePermanent(AnimeGroup grp, string userid)
        {
            return UserVote(grp, userid, AniDB_Vote_Type.Anime);
        }
        internal static float? UserVoteTemporary(AnimeGroup grp, string userid)
        {
            return UserVote(grp, userid, AniDB_Vote_Type.AnimeTemp);
        }

        private static float? UserVote(AnimeGroup grp, string userid, AniDB_Vote_Type tp)
        {
            List<AniDB_Vote> votes = Store.AnimeGroupRepo.GetAnimeSeries(grp.Id).Select(a => a.AniDB_Anime).SelectMany(a => a.MyVotes).Where(a => a.JMMUserId == userid && a.Type == tp).ToList();
            if (votes.Count == 0)
                return null;
            return votes.Sum(a => a.Vote) / votes.Count;
        }




        private void UpdateUserStats(AnimeGroup grp)
        {
            foreach (JMMUser j in Store.JmmUserRepo.GetUsers())
            {
                float? uvp = UserVotePermanent(grp, j.Id);
                float? uvt = UserVoteTemporary(grp, j.Id);
                float? uv = null;
                bool hasvotes = true;
                if (uvp != null && uvt != null)
                    uv = (uvp.Value + uvt.Value) / 2;
                else if (uvp != null)
                    uv = uvp.Value;
                else if (uvt != null)
                    uv = uvt.Value;
                else
                    hasvotes = false;
                GroupUserStats s = grp.UsersStats.FirstOrDefault(a => a.JMMUserId == j.Id);
                if (s == null)
                {
                    s = new GroupUserStats();
                    s.GroupFilters = new HashSet<string>();
                    s.JMMUserId = j.Id;
                    grp.UsersStats.Add(s);
                }
                s.UserVotes = uv;
                s.UserVotesPermanent = uvp;
                s.UserVotesTemporary = uvt;
                s.HasVotes = hasvotes;
            }
        }
        private void UpdateProperties(AnimeGroup grp)
        {
            List<AnimeGroup> grps = Store.AnimeGroupRepo.GetSubGroups(grp.Id);
            List<AnimeSerie> series = Store.AnimeGroupRepo.GetAnimeSeries(grp.Id);
            List<AniDB_Anime> ans = series.Select(a => a.AniDB_Anime).ToList();
            grp.HasTvDB = series.Any(a => a.HasTvDB()) || grps.Any(a => a.HasTvDB);
            grp.HasMAL = series.Any(a => a.HasMAL()) || grps.Any(a => a.HasMAL);
            grp.HasMovieDB = series.Any(a => a.HasMovieDB()) || grps.Any(a => a.HasMovieDB);
            grp.HasTrakt = series.Any(a => a.HasTrakt()) || grps.Any(a => a.HasTrakt);
            Dictionary<string, AniDB_Anime_Tag> tm = new Dictionary<string, AniDB_Anime_Tag>();
            foreach (AniDB_Anime_Tag t in ans.SelectMany(a => a.Tags).Union(grps.SelectMany(a => a.Tags)))
            {
                if (tm.ContainsKey(t.TagId))
                {
                    tm[t.TagId].Approval += t.Approval;
                    tm[t.TagId].Weight += t.Weight;
                }
                else
                {
                    AniDB_Anime_Tag nt = new AniDB_Anime_Tag();
                    t.CopyTo(nt);
                    tm.Add(nt.TagId, nt);
                }
            }
            grp.Tags = tm.Values.ToList();
            int completed = series.Count(a => a.IsComplete());
            grp.HasCompletedSeries = completed > 0 || grps.Any(a => a.HasCompletedSeries);
            grp.IsCompletedGroup = (completed == series.Count) && grps.All(a => a.IsCompletedGroup);
            int finishing = series.Count(a => a.HasFinishedAiring());
            grp.HasSeriesFinishingAiring = finishing > 0 || grps.Any(a => a.HasSeriesFinishingAiring);
            grp.IsGroupFinishingAiring = (finishing == series.Count) && grps.All(a => a.IsGroupFinishingAiring);
            grp.HasSeriesCurrentlyAiring = series.Any(a => a.IsCurrentlyAiring()) || grps.Any(a => a.HasSeriesCurrentlyAiring);
            grp.FirstSerieAirDate = null;
            grp.LastSerieAirDate = null;
            grp.LastSerieEndDate = null;
            foreach (AniDB_Anime a in ans)
            {
                if (a.AirDate.Date != 0)
                {
                    DateTime adate = a.AirDate.Date.ToDateTime();
                    if ((grp.FirstSerieAirDate == null) || (grp.FirstSerieAirDate > adate))
                        grp.FirstSerieAirDate = adate;
                    if ((grp.LastSerieAirDate == null) || (grp.LastSerieAirDate < adate))
                        grp.LastSerieAirDate = adate;
                }
                if (a.EndDate.Date != 0)
                {
                    DateTime edate = a.EndDate.Date.ToDateTime();
                    if ((grp.LastSerieEndDate == null) || (grp.LastSerieEndDate < edate))
                        grp.LastSerieEndDate = edate;
                }
            }
            grp.FirstSerieCreationDate = series.Min(a => a.DateTimeCreated);
            foreach (AnimeGroup g in grps)
            {
                if (g.FirstSerieAirDate != null && g.FirstSerieAirDate < grp.FirstSerieAirDate)
                    grp.FirstSerieAirDate = g.FirstSerieAirDate;
                if (g.LastSerieEndDate != null && g.LastSerieEndDate > grp.LastSerieEndDate)
                    grp.LastSerieEndDate = g.LastSerieEndDate;
                if (g.FirstSerieCreationDate != null && g.FirstSerieCreationDate < grp.FirstSerieCreationDate)
                    grp.FirstSerieCreationDate = g.FirstSerieCreationDate;
            }

            grp.SeriesCount = series.Count + grps.Sum(a => a.SeriesCount);
            grp.NormalEpisodeCount = ans.Sum(a => a.EpisodeCountNormal) + grps.Sum(a => a.NormalEpisodeCount);
            grp.SpecialEpisodeCount = ans.Sum(a => a.EpisodeCountSpecial) + grps.Sum(a => a.SpecialEpisodeCount);
            grp.SumSeriesRating = ans.Sum(a => a.Rating) + grps.Sum(a => a.SumSeriesRating);
            grp.SumSeriesTempRating = ans.Sum(a => a.TempRating) + grps.Sum(a => a.SumSeriesTempRating);
            grp.CountSeriesRating = ans.Count + grps.Sum(a => a.CountSeriesRating);
            grp.AniDB_Types = ans.Select(a => a.AnimeType).Union(grps.SelectMany(a => a.AniDB_Types)).Distinct().ToHashSet();
            grp.AvailableVideoQualities = series.SelectMany(a => a.AvailableVideoQualities).Union(grps.SelectMany(a => a.AvailableVideoQualities)).Distinct().ToHashSet();
            grp.AvailableReleaseQualities = new HashSet<string>();
            foreach (string s in grp.AvailableVideoQualities)
            {
                bool found = true;
                foreach (AnimeSerie sss in series)
                {
                    if (!sss.AvailableReleaseQualities.Contains(s))
                        found = false;
                }
                foreach (AnimeGroup g in grps)
                {
                    if (!g.AvailableReleaseQualities.Contains(s))
                        found = false;
                }
                if (found)
                    grp.AvailableReleaseQualities.Add(s);
            }
            grp.Languages = series.SelectMany(a => a.Languages).Union(grps.SelectMany(a => a.Languages)).Distinct().ToHashSet();
            grp.Subtitles = series.SelectMany(a => a.Subtitles).Union(grps.SelectMany(a => a.Subtitles)).Distinct().ToHashSet();
            grp.Fanarts = series.SelectMany(a => a.GetFanarts()).Union(grps.SelectMany(a => a.Fanarts)).ToList();
            grp.Banners = series.SelectMany(a => a.GetBanners()).Union(grps.SelectMany(a => a.Banners)).ToList();
            grp.Posters = series.SelectMany(a => a.GetCovers()).Union(grps.SelectMany(a => a.Posters)).ToList();

        }

        public void UpdateGroupFilters(AnimeGroup grp)
        {
            foreach (JMMUser j in Store.JmmUserRepo.GetAll())
            {
                GroupUserStats u = grp.UsersStats.First(a => a.JMMUserId == j.Id);
                u.GroupFilters = new HashSet<string>();
                foreach (GroupFilter f in Store.GroupFilterRepo.GetAll())
                {
                    HashSet<string> fgr = Store.GroupFilterRepo.GetFilterGroupsIds(f.Id, j.Id);
                    if (f.EvaluateGroup(grp, j))
                    {
                        u.GroupFilters.Add(f.Id);
                        if (!fgr.Contains(grp.Id))
                            fgr.Add(grp.Id);
                    }
                    else if (fgr.Contains(grp.Id))
                        fgr.Remove(grp.Id);
                }
            }
        }
    }
}
