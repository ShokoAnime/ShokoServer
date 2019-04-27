using System.Collections.Generic;

namespace Shoko.Server.API.v3
{
    public class Images
    {
        public List<Image> Posters { get; set; }
        public List<Image> Fanarts { get; set; }
        public List<Image> Banners { get; set; }
    }
}