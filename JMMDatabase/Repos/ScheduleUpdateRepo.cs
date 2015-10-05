using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMServerModels.DB;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class ScheduleUpdateRepo : BaseRepo<ScheduledUpdate>
    {
        internal override void InternalSave(ScheduledUpdate obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        public ScheduledUpdate GetByUpdateType(ScheduledUpdateType type)
        {
            return Items.Values.FirstOrDefault(a => a.Type == type);
        }
        internal override void InternalDelete(ScheduledUpdate obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
        }

        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<ScheduledUpdate>().ToDictionary(a => a.Id, a => a);
        }
    }
}
