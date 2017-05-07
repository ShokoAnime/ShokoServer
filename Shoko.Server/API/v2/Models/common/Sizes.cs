using System.Runtime.Serialization;

namespace Shoko.Server.API.v2.Models
{
    [DataContract]
    public class Sizes
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Episodes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Specials { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Credits { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Trailers { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Parodies { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int Others { get; set; }
    }
}