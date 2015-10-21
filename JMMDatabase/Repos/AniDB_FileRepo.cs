using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;
using JMMDatabase.Helpers;
using JMMModels;
using JMMModels.ClientExtensions;
using Raven.Client;
using Raven.Abstractions.Extensions;
namespace JMMDatabase.Repos
{
    public class AniDB_FileRepo : BaseRepo<AniDB_File>
    {
        private BiDictionary<string> FileCross { get; set; } = new BiDictionary<string>();


        public AniDB_File Find(string hash, long filesize)
        {
            return Find(hash + "_" + filesize);
        }

        public AniDB_File FindByFileId(int fileid)
        {
            string k=FileCross.FindInverse(fileid.ToString());
            return k == null ? null : Find(k);
        }
        internal override void InternalSave(AniDB_File obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            FileCross.Update(obj.Id,obj.FileId.ToString());
            s.Store(obj);
            if ((type & UpdateType.LinkedData) > 0)
            {
                List<AnimeSerie> l = Store.AnimeSerieRepo.AnimeSeriesFromAniDBFile(obj.Id);
                foreach (AnimeSerie sa in l)
                {
                    foreach (AniDB_Episode ep in sa.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value))
                    {
                        foreach (AniDB_File v in ep.Files.Where(a => a.Id == obj.Id).ToList())
                        {
                            obj.CopyTo(v);
                        }
                    }
                    Store.AnimeSerieRepo.Save(sa, s);
                }
            }

        }

        internal override void InternalDelete(AniDB_File obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            FileCross.Delete(obj.Id);
            s.Delete(obj);
            List<AnimeSerie> l = Store.AnimeSerieRepo.AnimeSeriesFromAniDBFile(obj.Id);
            foreach (AnimeSerie sa in l)
            {
                foreach (AniDB_Episode ep in sa.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value))
                {
                    foreach (AniDB_File v in ep.Files.Where(a => a.Id == obj.Id).ToList())
                    {
                        ep.Files.Remove(v);
                    }
                }
                Store.AnimeSerieRepo.Save(sa,s);
            }
        }
        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<AniDB_File>().ToDictionary(a => a.Id, a => a);
            foreach(AniDB_File f in Items.Values)
                FileCross.Add(f.Id,f.FileId.ToString());
        }
    }
}
