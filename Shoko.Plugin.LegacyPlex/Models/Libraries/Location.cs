using System.Runtime.Serialization;

namespace Shoko.Plugin.LegacyPlex.Models.Libraries;

public class Location
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "path")] public string Path { get; set; } = null!;
}
