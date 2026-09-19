using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using JsonDiffPatchDotNet;
using JsonDiffPatchDotNet.Formatters.JsonPatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Server.Services.Configuration;

using AbstractConfigurationActionResult = Shoko.Abstractions.Config.ConfigurationActionResult;
using Operation = Microsoft.AspNetCore.JsonPatch.Operations.Operation;

namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// The result of a configuration action.
/// </summary>
public class ConfigurationActionResult
{
    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    [Required]
    public bool ShowSaveMessage { get; init; }

    /// <summary>
    /// Indicates that the configuration should be refreshed by the client
    /// because we've modified it.
    /// </summary>
    [Required]
    public bool Refresh { get; init; }

    /// <summary>
    /// JSON Patch operations to apply to the live configuration without saving.
    /// </summary>
    public IReadOnlyList<Operation>? PatchOperations { get; init; }

    /// <summary>
    /// Any additional messages to show to the user.
    /// </summary>
    [Required]
    public IReadOnlyList<ConfigurationActionResultMessage> Messages { get; init; } = [];

    /// <summary>
    /// Any validation errors to show to the user.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors { get; init; }

    /// <summary>
    /// Indicates that existing validation errors should be kept.
    /// </summary>
    [Required]
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
            // Both sides of the diff are masked, so a secret can never end up in
            // a patch operation, and a secret that neither side changed reads as
            // unchanged instead of as a move from a real value to a sentinel.
            if (configurationService is not ConfigurationService service)
                throw new InvalidOperationException("Masking a configuration needs the server's own configuration service.");

            var before = service.MaskSecrets(JToken.Parse(json), result.GetType()).ToString(Formatting.None);
            var after = configurationService.SerializeWithMasking(result);
            var diff = new JsonDiffPatch(new() { TextDiff = TextDiffMode.Simple, DiffArrayOptions = new() { DetectMove = true, IncludeValueOnMove = true } })
                .Diff(before, after) ?? "{}";
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
