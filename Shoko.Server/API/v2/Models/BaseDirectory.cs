using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Shoko.Server.API.v2.Models.common;

namespace Shoko.Server.API.v2.Models
{
    [DataContract]
    public abstract class BaseDirectory
    {
        [DataMember]
        public int id { get; set; }

        [DataMember]
        public string name { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Shoko.Models.PlexAndKodi.AnimeTitle> titles { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string summary { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string url { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public DateTime added { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public DateTime edited { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string year { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string air { get; set; }

        [DataMember]
        public int size { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int localsize { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Sizes total_sizes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Sizes local_sizes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Sizes watched_sizes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int viewed { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string rating { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string userrating { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Role> roles { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<string> tags { get; set; }

        [DataMember(IsRequired = false)]
        public ArtCollection art { get; set; }

        [DataMember(IsRequired = true)]
        public abstract string type { get; }
    }
}