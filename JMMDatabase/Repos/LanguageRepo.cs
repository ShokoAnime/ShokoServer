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
    public class LanguageRepo : BaseRepo<Language>
    {
        internal override void InternalSave(Language obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            s.Store(obj);
        }

        public void CheckLanguage(string language)
        {
            if (Find(language) == null)
            {
                Language l=new Language();
                l.Id = language;
                Save(l);
            }
        }
        internal override void InternalDelete(Language obj, IDocumentSession s)
        {
            Items.Remove(obj.Id);
            s.Delete(obj);
        }

        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<Language>().ToDictionary(a => a.Id, a => a);
        }
    }
}
