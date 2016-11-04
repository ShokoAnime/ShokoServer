using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class ObjectList
    {
        public List<object> list { get; set; }
        public string name { get; set; }
        public int size { get; set; }
        public string type { get; set; }
        public int viewed { get; set; }
        public int id { get; set; }

        public ObjectList()
        {
            list = new List<object>();
        }
    } 
}
