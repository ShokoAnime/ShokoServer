using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;
using JMMModels;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class VideoInfoRepo : BaseRepo<VideoInfo>
    {
        public VideoInfo Find(string hash, long filesize)
        {
            return Find(hash + "_" + filesize);
        }
        internal override void InternalSave(VideoInfo obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        internal override void InternalDelete(VideoInfo obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
        }

        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<VideoInfo>().ToDictionary(a => a.Id, a => a);
        }
    }
}
