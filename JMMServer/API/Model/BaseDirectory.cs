using System;
using System.Collections.Generic;
using JMMContracts.PlexAndKodi;
using JMMServer.API.Model.common;
using Tag = JMMServer.API.Model.common.Tag;

namespace JMMServer.API.Model
{
    public abstract class BaseDirectory
    {
        public int id { get; set; }

        public string name { get; set; }
        public List<AnimeTitle> titles { get; set; }
        public string summary { get; set; }
        public string url { get; set; }

        public DateTime added { get; set; }
        public DateTime edited { get; set; }

        public string year { get; set; }
        public string air { get; set; }

        public int size { get; set; }
        public int localsize { get; set; }

        public int viewed { get; set; }

        public string rating { get; set; }
        public string userrating { get; set; }

        public List<Role> roles { get; set; }
        public List<Tag> tags { get; set; }
        public ArtCollection art { get; set; }

        public abstract string type { get; }

    }
}