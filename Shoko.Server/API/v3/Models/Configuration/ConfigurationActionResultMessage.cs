using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Config.Enums;

using AbstractConfigurationActionResultMessage = Shoko.Plugin.Abstractions.Config.ConfigurationActionResultMessage;

#nullable enable
namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// A message to display to the user upon completing a configuration action, be
/// it successfully or not.
/// </summary>
/// <param name="message">Abstract message.</param>
public class ConfigurationActionResultMessage(AbstractConfigurationActionResultMessage message)
{
    /// <summary>
    /// The title of the message to display to the user.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; init; } = message.Title;

    /// <summary>
    /// The message to display to the user.
    /// </summary>
    public string Message { get; init; } = message.Message;

    /// <summary>
    /// The color theme to use.
    /// </summary>
    public DisplayColorTheme Theme { get; init; } = message.Theme;
}
