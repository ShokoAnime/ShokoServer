#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

public class Setting
{
    /// <summary>
    /// Name of the setting
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The CLR type of the setting. Not necessary to provide it in mutations
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Value of the setting
    /// </summary>
    public object? Value { get; set; }
}
