using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models;

[DataContract]
public class MediaContainer<T>
{
    [DataMember(Name = "MediaContainer")] public T Container { get; set; }
}
