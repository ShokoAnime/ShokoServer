using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    public class Extras
    {
            public string Size { get; set; }

            public List<Video> Videos { get; set; }

        public Extras()
        {
        
        }

        public static explicit operator Extras(JMMContracts.PlexAndKodi.Extras extra_in)
        {
            Extras extra_out = new Extras();

            extra_out.Size = extra_in.Size;
            if (extra_in.Videos != null)
            {
                foreach (JMMContracts.PlexAndKodi.Video video in extra_in.Videos)
                {
                    extra_out.Videos.Add((Video)video);
                }
            }

            return extra_out;
        }
    }
}
