using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class ReleaseGroupRepo : BaseRepo<AniDB_ReleaseGroup>
    {

        internal override void InternalSave(AniDB_ReleaseGroup obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        internal override void InternalDelete(AniDB_ReleaseGroup obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
        }


        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<AniDB_ReleaseGroup>().ToDictionary(a=>a.Id,a=>a);
        }
    }
}
