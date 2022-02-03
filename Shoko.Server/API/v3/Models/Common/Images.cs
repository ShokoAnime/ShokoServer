using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Common
{
    public class Images
    {
        public List<Image> Posters { get; set; } = new List<Image>();
        public List<Image> Fanarts { get; set; } = new List<Image>();
        public List<Image> Banners { get; set; } = new List<Image>();
    }
}