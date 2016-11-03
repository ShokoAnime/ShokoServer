using System.Collections.Generic;

namespace JMMServer.API.Model.core
{
    public class ArtCollection
    {
        public List<Art> banner { get; set; }
        public List<Art> fanart { get; set; }
        public List<Art> thumb { get; set; }

        public ArtCollection()
        {
            banner = new List<Art>();
            fanart = new List<Art>();
            thumb = new List<Art>();
        }
    }

    public class Art
    {
        public string url { get; set;}
        public int index { get; set; }
    }
}
