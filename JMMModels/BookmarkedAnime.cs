using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels
{
    public class BookmarkedAnime
    {
        public string Id { get; set; }
        public string AnimeId { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
        public bool Downloading { get; set; }
    }
}
