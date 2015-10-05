using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_Creator : ImageInfo
    {
        public string Id { get; set; }
        public string KanjiName { get; set; }
        public string RomajiName { get; set; }
        public AniDB_Creator_Type CreatorType { get; set; }
        public string PicName { get; set; }
        public string UrlEnglish { get; set; }
        public string UrlJapanese { get; set; }
        public string WikiEnglish { get; set; }
        public string WikiJapanese { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
