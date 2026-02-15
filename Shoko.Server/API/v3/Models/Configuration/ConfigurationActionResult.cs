using System.Collections.Generic;
using System.Linq;
using JsonDiffPatchDotNet;
using JsonDiffPatchDotNet.Formatters.JsonPatch;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Services;
using AbstractConfigurationActionResult = Shoko.Abstractions.Config.ConfigurationActionResult;
using Operation = Microsoft.AspNetCore.JsonPatch.Operations.Operation;

#nullable enable
namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// The result of a configuration action.
/// </summary>
public class ConfigurationActionResult
{
    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    public bool ShowSaveMessage { get; init; } = false;

    /// <summary>
    /// Indicates that the configuration should be refreshed by the client
    /// because we've modified it.
    /// </summary>
    public bool Refresh { get; init; } = false;

    /// <summary>
    /// JSON Patch operations to apply to the live configuration without saving.
    /// </summary>
    public IReadOnlyList<Operation>? PatchOperations { get; init; }

    /// <summary>
    /// Any additional messages to show to the user.
    /// </summary>
    public IReadOnlyList<ConfigurationActionResultMessage> Messages { get; init; } = [];

    /// <summary>
    /// Any validation errors to show to the user.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors { get; init; }

    /// <summary>
    /// Indicates that existing validation errors should be kept.
    /// </summary>
    public bool KeepExistingValidationErrors { get; init; }

    /// <summary>
    /// The redirect to perform as part of the result of the action.
    /// </summary>
    public ConfigurationActionRedirect? Redirect { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    public ConfigurationActionResult() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    /// <param name="actionResult">The abstract result.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="json">The JSON representation of the original configuration.</param>
    public ConfigurationActionResult(AbstractConfigurationActionResult actionResult, IConfigurationService configurationService, string? json)
    {
        ShowSaveMessage = actionResult.ShowSaveMessage;
        Refresh = actionResult.Refresh;
        if (!string.IsNullOrEmpty(json) && actionResult.Configuration is { } result)
        {
            var diff = new JsonDiffPatch(new() { TextDiff = TextDiffMode.Simple, DiffArrayOptions = new() { DetectMove = true, IncludeValueOnMove = true } })
                .Diff(json, configurationService.Serialize(result)) ?? "{}";
            PatchOperations = new JsonDeltaFormatter()
                .Format(JToken.Parse(diff))
                .Select(op => new Operation(op.Op, op.Path, op.From, op.Value))
                .ToList();
        }
        Messages = actionResult.Messages.Select(m => new ConfigurationActionResultMessage(m)).ToList();
        ValidationErrors = actionResult.ValidationErrors;
        KeepExistingValidationErrors = actionResult.KeepExistingValidationErrors;
        Redirect = actionResult.Redirect is not null ? new(actionResult.Redirect) : null;
    }
}
