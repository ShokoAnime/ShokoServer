using System;
using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class ImportFolderRepo : BaseRepo<ImportFolder>
    {



        internal override void InternalSave(ImportFolder obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        internal override void InternalDelete(ImportFolder obj, IDocumentSession s)
        {

            throw new NotImplementedException();
        }

        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<ImportFolder>().ToDictionary(a=>a.Id,a=>a);
        }
    }
}
