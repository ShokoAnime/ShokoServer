using System.Runtime.Serialization;

namespace Shoko.Plugin.LegacyPlex.Models;

[DataContract]
public class MediaContainer<T>
{
    [DataMember(Name = "MediaContainer")] public T Container { get; set; } = default!;
}
