using System.Runtime.Serialization;

namespace Shoko.Models.Plex
{
    public class TagHolder
    {
        [DataMember(Name = "tag")] public string Tag { get; set; }
    }
}