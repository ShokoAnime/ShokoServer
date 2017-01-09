using System.Collections.Generic;

namespace Shoko.Server.API.Model.common
{
    public class Series
    {
        public List<Serie> series { get; set; }
        public int size { get; set; }
        public int viewed { get; set; }
        public string title { get; set; }
        public ArtCollection art { get; set; }
    }
}
