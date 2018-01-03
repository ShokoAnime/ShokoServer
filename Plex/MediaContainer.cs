using System.Runtime.Serialization;

namespace Shoko.Models.Plex
{
    [DataContract]
    public class MediaContainer<T>
    {
        [DataMember(Name = "MediaContainer")] public T Container { get; set; }
    }
}