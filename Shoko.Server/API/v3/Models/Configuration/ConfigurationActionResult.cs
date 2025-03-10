
using System.Collections.Generic;
using System.Linq;
using AbstractConfigurationActionResult = Shoko.Plugin.Abstractions.Config.ConfigurationActionResult;

#nullable enable
namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// The result of a configuration action.
/// </summary>
/// <param name="actionResult">The abstract result.</param>
public class ConfigurationActionResult(AbstractConfigurationActionResult actionResult)
{
    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    public bool ShowDefaultSaveMessage { get; init; } = actionResult.ShowDefaultSaveMessage;

    /// <summary>
    /// Indicates that the configuration should be refreshed.
    /// </summary>
    public bool RefreshConfiguration { get; init; } = actionResult.RefreshConfiguration;

    /// <summary>
    /// Any additional messages to show to the user.
    /// </summary>
    public List<ConfigurationActionResultMessage> Messages { get; init; } = actionResult.Messages.Select(m => new ConfigurationActionResultMessage(m)).ToList();
}
