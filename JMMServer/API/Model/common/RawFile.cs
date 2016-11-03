using System;
using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class RawFile
    {
        public Dictionary<string, string> audio { get; set; }

        public Dictionary<string, string> video { get; set; }

        public string crc32 { get; set; }
        public string ed2khash { get; set; }
        public string md5 { get; set; }
        public string sha1 { get; set; }

        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public long duration { get; set; }

        public string filename { get; set; }
        public long size { get; set; }
        public string hash { get; set; }
        public int hashsource { get; set; }

        public string info { get; set; }
        public int isignored { get; set; }

        public JMMContracts.PlexAndKodi.Media media { get; set; }
        public int mediasize { get; set; }

        public RawFile()
        {
            audio = new Dictionary<string, string>();
            video = new Dictionary<string, string>();
        }
    }
}
