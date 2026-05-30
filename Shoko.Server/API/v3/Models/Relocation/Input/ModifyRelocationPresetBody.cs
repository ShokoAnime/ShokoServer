
#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation.Input;

/// <summary>
///   Represents the information required to modify a relocation preset.
/// </summary>
public class ModifyRelocationPresetBody
{
    /// <summary>
    ///   Update the name of the preset.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   Whether or not to set this preset as the default preset.
    /// </summary>
    public bool? IsDefault { get; set; }
}
