using System.Runtime.Serialization;

namespace Shoko.Plugin.LegacyPlex.Models;

public class TagHolder
{
    [DataMember(Name = "tag")] public string Tag { get; set; } = null!;
}
