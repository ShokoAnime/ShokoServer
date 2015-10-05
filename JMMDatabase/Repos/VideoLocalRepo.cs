using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMModels.ClientExtensions;
using Raven.Abstractions.Extensions;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class VideoLocalRepo : BaseRepo<VideoLocal>
    {
        public VideoLocal Find(string hash, long filesize)
        {
            return Find(hash + "_" + filesize);
        }
        internal override void InternalSave(VideoLocal obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
            Dictionary<AnimeSerie,List<VideoLocal>> l=Store.AnimeSerieRepo.AnimeSerieFromVideoLocal(obj.Id);
            foreach (AnimeSerie sa in l.Keys)
            {
                foreach (VideoLocal v in l[sa])
                {
                    obj.CopyTo(v);                    
                }
                Store.AnimeSerieRepo.Save(sa);
            }
        }

        public List<string> GetVideolocalsWithoutAniDBFile()
        {
            List<string> rets=new List<string>();
            foreach (string n in Items.Keys)
            {
                if (Store.AniDB_FileRepo.Find(n) == null)
                    rets.Add(n);
            }
            return rets;
        }
        public List<string> GetVideolocalsWithAniDBFile()
        {
            List<string> rets = new List<string>();
            foreach (string n in Items.Keys)
            {
                if (Store.AniDB_FileRepo.Find(n) != null)
                    rets.Add(n);
            }
            return rets;
        }

        internal override void InternalDelete(VideoLocal obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
            Dictionary<AnimeSerie, List<VideoLocal>> l = Store.AnimeSerieRepo.AnimeSerieFromVideoLocal(obj.Id);
            foreach (AnimeSerie sa in l.Keys)
            {
                foreach (VideoLocal v in l[sa])
                    sa.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).Where(a => a.VideoLocals.Contains(v)).ForEach(a=>a.VideoLocals.Remove(v));
                Store.AnimeSerieRepo.Save(sa);
            }
        }

        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<VideoLocal>().ToDictionary(a => a.Id, a => a);
        }
    }
}
