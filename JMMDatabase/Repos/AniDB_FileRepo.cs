using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.ClientExtensions;
using Raven.Client;
using Raven.Abstractions.Extensions;
namespace JMMDatabase.Repos
{
    public class AniDB_FileRepo : BaseRepo<AniDB_File>
    {
        public AniDB_File Find(string hash, long filesize)
        {
            return Find(hash + "_" + filesize);
        }
        internal override void InternalSave(AniDB_File obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
            Dictionary<AnimeSerie, List<AniDB_File>> l = Store.AnimeSerieRepo.AnimeSerieFromAniDBFile(obj.Id);
            foreach (AnimeSerie sa in l.Keys)
            {
                foreach (AniDB_File v in l[sa])
                    obj.CopyTo(v);
                Store.AnimeSerieRepo.Save(sa);
            }
        }

        internal override void InternalDelete(AniDB_File obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
            Dictionary<AnimeSerie, List<AniDB_File>> l = Store.AnimeSerieRepo.AnimeSerieFromAniDBFile(obj.Id);
            foreach (AnimeSerie sa in l.Keys)
            {
                foreach (AniDB_File v in l[sa])
                    sa.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).Where(a => a.Files.Contains(v)).ForEach(a => a.Files.Remove(v));
                Store.AnimeSerieRepo.Save(sa);
            }
        }
        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<AniDB_File>().ToDictionary(a => a.Id, a => a);
        }
    }
}
