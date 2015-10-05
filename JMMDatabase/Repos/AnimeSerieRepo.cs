using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMDatabase.Helpers;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class AnimeSerieRepo : BaseRepo<AnimeSerie>
    {
        
        private Dictionary<string, string> AnimeSerieGroupParent { get; set; } = new Dictionary<string, string>();
        private BiDictionaryHashSet<string> VideoLocals { get; set; }=new BiDictionaryHashSet<string>();
        private BiDictionary<string> AniDBAnimes { get; set; }=new BiDictionary<string>(); 
        private BiDictionaryHashSet<string> AniDBEpisodes { get; set; }=new BiDictionaryHashSet<string>();
        private BiDictionaryHashSet<string> AniDBFiles { get; set; } = new BiDictionaryHashSet<string>();



        public string GetGroupId(string serieid)
        {
            return AnimeSerieGroupParent.Find(serieid);
        }

        public AnimeGroup GetGroup(string serieid)
        {
            string n = GetGroupId(serieid);
            return n == null ? null : Store.AnimeGroupRepo.Find(n);
        }


        private void UpdateParentGroups(AnimeSerie g, IDocumentSession session)
        {
            if (AnimeSerieGroupParent.ContainsKey(g.Id))
            {
                string original = AnimeSerieGroupParent[g.Id];
                AnimeSerieGroupParent[g.Id] = g.GroupId;
                if (original != g.GroupId)
                {
                    if (original != null)
                    {
                        HashSet<string> k = Store.AnimeGroupRepo.GetAnimeSeriesIds(original);
                        if (k.Contains(g.Id))
                            k.Remove(g.Id);
                        Store.AnimeGroupRepo.Update(original,session);
                    }
                }
            }
            else
                AnimeSerieGroupParent[g.Id] = g.GroupId;
            if (g.GroupId != null)
            {
                HashSet<string> k2 = Store.AnimeGroupRepo.GetAnimeSeriesIds(g.GroupId);
                if (!k2.Contains(g.Id))
                    k2.Add(g.Id);
                Store.AnimeGroupRepo.Update(g.GroupId, session);
            }

        }
        internal override void InternalDelete(AnimeSerie obj, IDocumentSession s)
        {
            AnimeSerieGroupParent.Remove(obj.Id);
            Items.Remove(obj.Id);
            if (obj.GroupId != null)
            {
                if (Store.AnimeGroupRepo.GroupAnimeSeries.ContainsKey(obj.GroupId))
                {
                    HashSet<string> sa = Store.AnimeGroupRepo.GetAnimeSeriesIds(obj.GroupId);
                    sa.Remove(obj.Id);
                    Store.AnimeGroupRepo.Update(obj.GroupId,s);
                }
            }
            VideoLocals.Delete(obj.Id);
            AniDBAnimes.Delete(obj.Id);
            AniDBEpisodes.Delete(obj.Id);
            AniDBFiles.Delete(obj.Id);
            s.Delete(obj);
        }


        public Dictionary<AnimeSerie,List<VideoLocal>> AnimeSerieFromVideoLocal(string id)
        {
            Dictionary<AnimeSerie, List<VideoLocal>> v = VideoLocals.FindInverse(id).SelectOrDefault(a => Items.Find(a)).ToDictionary(a => a, a=>new List<VideoLocal>());
            foreach (AnimeSerie s in v.Keys)
                v[s].AddRange(s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.VideoLocals));
            return v;
        }

        public Dictionary<AnimeSerie, List<AniDB_File>> AnimeSerieFromAniDBFile(string id)
        {
            Dictionary<AnimeSerie, List<AniDB_File>> v = AniDBFiles.FindInverse(id).SelectOrDefault(a => Items.Find(a)).ToDictionary(a => a, a => new List<AniDB_File>());
            foreach (AnimeSerie s in v.Keys)
            {
                v[s].AddRange(s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.Files));
            }
            return v; 
        }
        public AnimeSerie AnimeSerieFromAniDBEpisode(string epid)
        {
            HashSet<string> hashs=AniDBEpisodes.FindInverse(epid);
            if ((hashs==null) || (hashs.Count==0))
                return null;
            return Items.Find(hashs.ElementAt(0));
        }
        public AnimeSerie AnimeSerieFromAniDBAnime(string animeId)
        {
            string v=AniDBAnimes.FindInverse(animeId);
            if (v != null)
                return Find(v);
            return null;
        }
        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<AnimeSerie>().ToDictionary(a => a.Id, a => a);
            AnimeSerieGroupParent = Items.ToDictionary(a => a.Key, a => a.Value.GroupId);
            VideoLocals=new BiDictionaryHashSet<string>();
            AniDBAnimes=new BiDictionary<string>();
            foreach (AnimeSerie n in Items.Values)
            {
                VideoLocals.Add(n.Id, n.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.VideoLocals).Select(a=>a.Id).ToHashSet());
                AniDBAnimes.Add(n.Id, n.AniDB_Anime.Id);
                AniDBEpisodes.Add(n.Id, n.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).Select(a=>a.Id).ToHashSet());
                AniDBFiles.Add(n.Id, n.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a=>a.Files).Select(a=>a.Id).ToHashSet());
            }
        }



        internal override void InternalSave(AnimeSerie s, IDocumentSession session, UpdateType type = UpdateType.All)
        {
            if ((type & UpdateType.Properties) > 0)
                UpdateProperties(s);
            if ((type & UpdateType.User) > 0)
                UpdateUserStats(s);
            Items[s.Id] = s;
            if ((type & UpdateType.ParentGroup) > 0)
                UpdateParentGroups(s, session);

            VideoLocals.Update(s.Id, s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.VideoLocals).Select(a => a.Id).ToHashSet());
            AniDBAnimes.Update(s.Id, s.AniDB_Anime.Id);
            AniDBEpisodes.Update(s.Id, s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).Select(a=>a.Id).ToHashSet());
            AniDBFiles.Update(s.Id, s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.Files).Select(a => a.Id).ToHashSet());
            session.Store(s);
        }


        private static void UpdateUserStats(AnimeSerie sa)
        {
            foreach (JMMUser j in Store.JmmUserRepo.GetUsers())
            {
                ExtendedUserStats s = sa.UsersStats.FirstOrDefault(a => a.JMMUserId == j.Id);
                if (s == null)
                {
                    s = new ExtendedUserStats();
                    s.JMMUserId = j.Id;
                    sa.UsersStats.Add(s);
                }
            }
        }



        internal static void UpdateProperties(AnimeSerie s)
        {

            List<AniDB_File> allfiles =
            s.Episodes.SelectMany(a => a.AniDbEpisodes.Values.SelectMany(b => b)).SelectMany(a => a.Files).ToList();
            s.AvailableVideoQualities = allfiles.Select(a => a.Source).Distinct().ToHashSet();
            s.AvailableReleaseQualities = new HashSet<string>();
            foreach (string sn in s.AvailableVideoQualities)
            {
                bool found = true;
                foreach (AnimeEpisode ep in s.Episodes)
                {
                    if (!ep.AniDbEpisodes.Values.SelectMany(b => b).SelectMany(a => a.Files).Any(a => a.Source == sn))
                        found = false;
                }
                if (found)
                    s.AvailableReleaseQualities.Add(sn);
            }
            s.Languages = allfiles.SelectMany(a => a.Languages).Select(a => a.Name).Distinct().ToHashSet();
            s.Subtitles = allfiles.SelectMany(a => a.Subtitles).Select(a => a.Name).Distinct().ToHashSet();
        }

    }
}
