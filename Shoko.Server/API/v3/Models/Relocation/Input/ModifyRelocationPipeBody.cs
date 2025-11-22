
#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation.Input;

/// <summary>
///   Represents the information required to modify a relocation pipe.
/// </summary>
public class ModifyRelocationPipeBody
{
    /// <summary>
    ///   Update the name of the pipe.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   Whether or not to set this pipe as the default pipe.
    /// </summary>
    public bool? IsDefault { get; set; }
}
