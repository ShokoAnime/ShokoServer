using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    public class Hub
    {
        public string Key { get; set; }

        public string Type { get; set; }

        public string HubIdentifier { get; set; }

        public string Size { get; set; }

        public string Title { get; set; }

        public string More { get; set; }

        public Hub()
        {

        }

        public static explicit operator Hub(JMMContracts.PlexAndKodi.Hub hub_in)
        {
            Hub hub_out = new Hub();
            hub_out.Key = hub_in.Key;
            hub_out.Type = hub_in.Type;
            hub_out.HubIdentifier = hub_in.HubIdentifier;
            hub_out.Size = hub_in.Size;
            hub_out.Title = hub_in.Title;
            hub_out.More = hub_in.More;
            return hub_out;
        }
    }
}
