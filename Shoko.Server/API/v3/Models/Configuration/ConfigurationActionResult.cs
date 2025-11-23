using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch.Operations;
using AbstractConfigurationActionResult = Shoko.Plugin.Abstractions.Config.ConfigurationActionResult;

#nullable enable
namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// The result of a configuration action.
/// </summary>
/// <param name="actionResult">The abstract result.</param>
/// <param name="patches">JSON Patch operations to apply to the live configuration.</param>
public class ConfigurationActionResult(AbstractConfigurationActionResult actionResult, IReadOnlyList<Operation>? patches = null)
{
    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    public bool ShowSaveMessage { get; init; } = actionResult.ShowSaveMessage;

    /// <summary>
    /// Indicates that the configuration should be refreshed by the client
    /// because we've modified it.
    /// </summary>
    public bool Refresh { get; init; } = actionResult.Refresh;

    /// <summary>
    /// JSON Patch operations to apply to the live configuration without saving.
    /// </summary>
    public IReadOnlyList<Operation>? PatchOperations { get; init; } = patches;

    /// <summary>
    /// Any additional messages to show to the user.
    /// </summary>
    public IReadOnlyList<ConfigurationActionResultMessage> Messages { get; init; } = actionResult.Messages.Select(m => new ConfigurationActionResultMessage(m)).ToList();

    /// <summary>
    /// The redirect to perform as part of the result of the action.
    /// </summary>
    public ConfigurationActionRedirect? Redirect { get; init; } = actionResult.Redirect is not null ? new(actionResult.Redirect) : null;
}
