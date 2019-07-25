using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.API.v2.Models.common
{
    public class ArtCollection
    {
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Art> banner { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Art> fanart { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
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
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string url { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public int index { get; set; }
    }
}