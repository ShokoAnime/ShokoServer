using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using JMMServerModels.DB;
using JMMServerModels.DB.Childs;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class CommandRequestRepo : BaseRepo<CommandRequest>
    {
        private Dictionary<string, Dictionary<CommandRequestPriority, HashSet<string>>> Requests = new Dictionary<string, Dictionary<CommandRequestPriority, HashSet<string>>>();

        private string activeuser;



        internal override void InternalSave(CommandRequest obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            Items[obj.Id] = obj;
            Dictionary<CommandRequestPriority, HashSet<string>> dic;
            if (Requests.ContainsKey(obj.JMMUserId))
                dic = Requests[obj.JMMUserId];
            else
            {
                dic=new Dictionary<CommandRequestPriority, HashSet<string>>();
                Requests.Add(obj.JMMUserId, dic);
            }
            HashSet<string> d2;
            if (dic.ContainsKey(obj.Priority))
                d2 = dic[obj.Priority];
            else
            {
                d2=new HashSet<string>();
                dic.Add(obj.Priority, d2);
            }
            if (!d2.Contains(obj.Id))
                d2.Add(obj.Id);

        }

        public CommandRequest Get()
        {

            if (!Requests.ContainsKey(activeuser))
            {
                if (Requests.Count==0)
                    return null;
                activeuser = Requests.ElementAt(0).Key;
            }
            Dictionary<CommandRequestPriority, HashSet<string>> dic = Requests[activeuser];
            CommandRequestPriority p = dic.Keys.OrderByDescending(c => c).First();
            string id = dic[p].ElementAt(0);
            return Find(id);
        }

        public int Count()
        {
            return Items.Count;
        }
        internal override void InternalDelete(CommandRequest obj, IDocumentSession s)
        {
            if (Requests.ContainsKey(obj.JMMUserId))
            {
                Dictionary<CommandRequestPriority, HashSet<string>> dic = Requests[obj.JMMUserId];
                if (dic.ContainsKey(obj.Priority))
                { 
                    HashSet<string> d2 = dic[obj.Priority];
                    if (d2.Contains(obj.Id))
                        d2.Remove(obj.Id);
                    if (d2.Count == 0)
                        dic.Remove(obj.Priority);
                    if (dic.Count == 0)
                        Requests.Remove(obj.JMMUserId);
                }
                
            }
            if (Items.ContainsKey(obj.Id))
                Items.Remove(obj.Id);
        }


        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<CommandRequest>().ToDictionary(a => a.Id, a => a);
            Requests=Items.Values.GroupBy(a=>a.JMMUserId).ToDictionary(a=>a.Key,a=>a.GroupBy(b=>b.Priority).ToDictionary(c=>c.Key,c=>c.Select(d=>d.Id).ToHashSet()));
            JMMUser user = Store.JmmUserRepo.AsQueryable().FirstOrDefault(a => a.IsMasterAccount);
            if (user != null)
                activeuser = user.Id;

        }
    }
}
