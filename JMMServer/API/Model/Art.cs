using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.API.Model
{
    public class ArtCollection
    {
        public List<string> banner { get; set; }
        public List<string> fanart { get; set; }
        public List<string> thumb { get; set; }

        public ArtCollection()
        {
            banner = new List<string>();
            fanart = new List<string>();
            thumb = new List<string>();
        }
    }
}
