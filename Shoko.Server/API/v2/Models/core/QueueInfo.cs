using System.Runtime.Serialization;

namespace Shoko.Server.API.v2.Models.core
{
    [DataContract]
    public class QueueInfo
    {
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public int count { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string state { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public bool isrunning { get; set; }
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public bool ispause { get; set; }
    }
}