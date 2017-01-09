using System.Collections.Generic;

namespace Shoko.Server.API.Model.common
{
    [System.Obsolete]
    public class ObjectList
    {
        public List<object> list { get; private set; }
        public string name { get; set; }
        public int size { get; private set; }
        public string type { get; set; }

        public ObjectList()
        {
            list = new List<object>();
        }

        public ObjectList(string _name, ListType _type)
        {
            name = _name;
            type = _type.ToString().ToLower();
            list = new List<object>();
        }

        public void Add(List<object> new_list)
        {
            list = new_list;
            size = list.Count;
        }

        public enum ListType
        {
            FILE = 1,
            EPISODE = 2,
            SERIE = 3                       
        }
    } 
}
