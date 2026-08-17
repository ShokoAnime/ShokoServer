using System.Diagnostics;
using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Libraries;

[DebuggerDisplay("Directory, Title = {Title}, Agent = {Agent}, Scanner = {Scanner}")]
public class Directory
{
    [DataMember(Name = "allowSync")] public bool AllowSync { get; set; }
    [DataMember(Name = "art")] public string Art { get; set; } = null!;
    [DataMember(Name = "composite")] public string Composite { get; set; } = null!;
    [DataMember(Name = "filters")] public bool Filters { get; set; }
    [DataMember(Name = "refreshing")] public bool Refreshing { get; set; }
    [DataMember(Name = "thumb")] public string Thumb { get; set; } = null!;
    [DataMember(Name = "key")] public int Key { get; set; }
    [DataMember(Name = "type")] public PlexType Type { get; set; }
    [DataMember(Name = "title")] public string Title { get; set; } = null!;
    [DataMember(Name = "agent")] public string Agent { get; set; } = null!;
    [DataMember(Name = "scanner")] public string Scanner { get; set; } = null!;
    [DataMember(Name = "language")] public string Language { get; set; } = null!;
    [DataMember(Name = "uuid")] public string Uuid { get; set; } = null!;
    [DataMember(Name = "updatedAt")] public long UpdatedAt { get; set; }
    [DataMember(Name = "createdAt")] public long CreatedAt { get; set; }
    [DataMember(Name = "Location")] public Location[] Location { get; set; } = null!;
}
