using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

/// <summary>
/// A definition to build UI for a setting
/// </summary>
public class SettingDefinition
{
    /// <summary>
    /// Name of the setting
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Description of the setting
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Language for the editor to use for highlighting and intellisense
    /// </summary>
    public CodeLanguage? Language { get; set; }
    /// <summary>
    /// The type of the setting. This is both for the webui to display and an easy lookup for what type to send and receive as a value
    /// </summary>
    public RenamerSettingType SettingType { get; set; }
}
