using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class JMMUserRepo : BaseRepo<JMMUser>
    {



        internal override void InternalSave(JMMUser obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            List<string> grps = UpdateAnimeSeries(obj,s);
            UpdateGroups(obj, grps, s);
            UpdateReleaseGroups(obj,s);
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        internal override void InternalDelete(JMMUser obj, IDocumentSession s)
        {
            List<string> grps = DeleteUserFromAnimeSeries(obj,s);
            DeleteUserFromGroups(obj, grps, s);
            DeleteUserFromReleaseGroups(obj,s);
            Items.Remove(obj.Id);
            s.Delete(obj);
        }


        private void UpdateGroups(JMMUser user, List<string> grps, IDocumentSession session)
        {
            foreach (AnimeGroup grp in Store.AnimeGroupRepo.ToList())
            {
                if (!grp.UsersStats.Any(a => a.JMMUserId == user.Id))
                {
                    if (!grps.Contains(grp.Id))
                        grps.Add(grp.Id);
                }
            }
            foreach (AnimeGroup grp in grps.Select(a => Store.AnimeGroupRepo.Find(a)))
            {
                Store.AnimeGroupRepo.Save(grp,session, UpdateType.User);
            }
        }

        private static void DeleteUserFromGroups(JMMUser user, List<string> grps, IDocumentSession session)
        {
            foreach (AnimeGroup grp in Store.AnimeGroupRepo.ToList())
            {
                if (grp.UsersStats.Any(a => a.JMMUserId == user.Id))
                    grps.Add(grp.Id);
            }
            foreach (AnimeGroup grp in grps.Select(a => Store.AnimeGroupRepo.Find(a)))
            {
                Store.AnimeGroupRepo.Save(grp, session, UpdateType.User);
            }
        }
        public static void DeleteUserFromReleaseGroups(JMMUser user, IDocumentSession session)
        {
            foreach (AniDB_ReleaseGroup r in Store.ReleaseGroupRepo.ToList())
            {
                bool change = false;
                foreach (AniDB_Vote v in r.MyVotes.Where(a => a.JMMUserId == user.Id))
                {
                    r.MyVotes.Remove(v);
                    change = true;
                }
                if (change)
                    session.Store(r);
            }
        }
        public static void UpdateReleaseGroups(JMMUser user, IDocumentSession session)
        {
            if (!user.IsRealUserAccount)
                return;
            JMMUser authuser = user.GetAniDBUser();
            bool clone = user.Id != authuser.Id;
            if (clone)
            {
                foreach (AniDB_ReleaseGroup r in Store.ReleaseGroupRepo.ToList())
                {
                    bool change = false;
                    if (!r.MyVotes.Any(a => a.JMMUserId == user.Id))
                    {
                        List<AniDB_Vote> vs = r.MyVotes.Where(a => a.JMMUserId == authuser.Id).ToList();
                        foreach (AniDB_Vote v in vs)
                        {
                            AniDB_Vote nv = new AniDB_Vote();
                            nv.JMMUserId = user.Id;
                            nv.Type = v.Type;
                            nv.Vote = v.Vote;
                            r.MyVotes.Add(nv);
                            change = true;
                        }
                    }
                    if (change)
                        session.Store(r);
                }
            }
        }



        private static List<string> UpdateAnimeSeries(JMMUser user, IDocumentSession session)
        {
            if (!user.IsRealUserAccount)
                return new List<string>();
            JMMUser authuser = user.GetAniDBUser();
            bool clone = user.Id != authuser.Id;
            List<string> changedGroups = new List<string>();
            foreach (AnimeSerie ser in Store.AnimeSerieRepo.ToList())
            {
                bool change = false;
                if (!ser.UsersStats.Any(a => a.JMMUserId == user.Id))
                {
                    ExtendedUserStats n = new ExtendedUserStats();
                    n.JMMUserId = user.Id;
                    ser.UsersStats.Add(n);
                    change = true;
                }
                foreach (AnimeEpisode ep in ser.Episodes)
                {
                    if (!ep.UsersStats.Any(a => a.JMMUserId == user.Id))
                    {
                        UserStats n = new UserStats();
                        n.JMMUserId = user.Id;
                        ep.UsersStats.Add(n);
                        change = true;
                    }
                    if (clone)
                    {
                        foreach (AniDB_Episode aep in ep.AniDbEpisodes.SelectMany(a => a.Value))
                        {
                            if (!aep.MyVotes.Any(a => a.JMMUserId == user.Id))
                            {
                                List<AniDB_Vote> vs = aep.MyVotes.Where(a => a.JMMUserId == authuser.Id).ToList();
                                foreach (AniDB_Vote v in vs)
                                {
                                    AniDB_Vote nv = new AniDB_Vote();
                                    nv.JMMUserId = user.Id;
                                    nv.Type = v.Type;
                                    nv.Vote = v.Vote;
                                    aep.MyVotes.Add(nv);
                                    change = true;
                                }
                            }
                        }
                    }
                }

                if (clone)
                {
                    if (!ser.AniDB_Anime.MyVotes.Any(a => a.JMMUserId == user.Id))
                    {
                        List<AniDB_Vote> vs = ser.AniDB_Anime.MyVotes.Where(a => a.JMMUserId == authuser.Id).ToList();
                        foreach (AniDB_Vote v in vs)
                        {
                            AniDB_Vote nv = new AniDB_Vote();
                            nv.JMMUserId = user.Id;
                            nv.Type = v.Type;
                            nv.Vote = v.Vote;
                            ser.AniDB_Anime.MyVotes.Add(nv);
                            change = true;
                        }
                    }
                }
                if (change)
                {
                    Store.AnimeSerieRepo.Save(ser,session, UpdateType.None);
                    if (!string.IsNullOrEmpty(ser.GroupId))
                    {
                        if (!changedGroups.Contains(ser.GroupId))
                            changedGroups.Add(ser.GroupId);
                    }
                }
            }
            return changedGroups;
        }
        private static List<string> DeleteUserFromAnimeSeries(JMMUser user, IDocumentSession session)
        {

            List<string> changedGroups = new List<string>();
            foreach (AnimeSerie ser in Store.AnimeSerieRepo.ToList())
            {
                bool change = false;
                ExtendedUserStats seu = ser.UsersStats.FirstOrDefault(a => a.JMMUserId == user.Id);
                if (seu != null)
                {
                    ser.UsersStats.Remove(seu);
                    change = true;
                }
                foreach (AnimeEpisode ep in ser.Episodes)
                {
                    UserStats su = ep.UsersStats.FirstOrDefault(a => a.JMMUserId == user.Id);
                    if (su != null)
                    {
                        ep.UsersStats.Remove(su);
                        change = true;
                    }

                    foreach (AniDB_Episode aep in ep.AniDbEpisodes.SelectMany(a => a.Value))
                    {
                        List<AniDB_Vote> vs = aep.MyVotes.Where(a => a.JMMUserId == user.Id).ToList();
                        foreach (AniDB_Vote v in vs)
                        {
                            aep.MyVotes.Remove(v);
                            change = true;
                        }

                    }
                }
                foreach (AniDB_Vote v in ser.AniDB_Anime.MyVotes.Where(a => a.JMMUserId == user.Id))
                {
                    ser.AniDB_Anime.MyVotes.Remove(v);
                    change = true;
                }
                if (change)
                {
                    Store.AnimeSerieRepo.Save(ser, session, UpdateType.None);
                    if (!string.IsNullOrEmpty(ser.GroupId))
                    {
                        if (!changedGroups.Contains(ser.GroupId))
                            changedGroups.Add(ser.GroupId);
                    }
                }
            }
            return changedGroups;
        }






        public List<JMMUser> GetUsers()
        {
            return Items.Values.Where(a=>a.IsRealUserAccount).ToList();
        }

        public List<JMMUser> GetNotUsers()
        {
            return Items.Values.Where(a => !a.IsRealUserAccount).ToList();
        }
        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<JMMUser>().ToDictionary(a => a.Id, a => a);
        }

        public JMMUser GetMasterUser()
        {
            return Items.Values.FirstOrDefault(a => a.IsMasterAccount);
        }
    }
}
