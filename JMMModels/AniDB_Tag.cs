using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_Tag
    {
        public string Id { get; set; }
        public bool Spoiler { get; set; }
        public bool LocalSpoiler { get; set; }
        public bool GlobalSpoiler { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
    }
}
