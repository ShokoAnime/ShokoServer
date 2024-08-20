using System.Collections.Generic;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

public class RenamerConfig
{
    /// <summary>
    /// The ID of the renamer
    /// </summary>
    public string RenamerID { get; set; }

    /// <summary>
    /// The name of the renamer. This is a unique ID!
    /// </summary>
    public string Name { get; set; }

    public List<Setting>? Settings { get; set; }
}
