using System;
using System.Collections.Generic;
using System.Linq;
using JsonDiffPatchDotNet;
using JsonDiffPatchDotNet.Formatters.JsonPatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using Operation = Microsoft.AspNetCore.JsonPatch.Operations.Operation;
using ConfigurationActionType = Shoko.Plugin.Abstractions.Config.ConfigurationActionType;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing configurations.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
/// <param name="configurationService">Configuration service.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class ConfigurationController(ISettingsProvider settingsProvider, IPluginManager pluginManager, IConfigurationService configurationService) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Get a list with information about all registered configurations.
    /// </summary>
    /// <param name="query">
    ///   An optional query to filter configurations by name.
    /// </param>
    /// <param name="pluginID">
    ///   Whether to include only configurations for a specific plugin, or
    ///   all configurations if omitted.
    /// </param>
    /// <param name="hidden">
    ///   Whether to include all hidden configurations, include only hidden
    ///   configurations, or exclude all hidden configurations.
    /// </param>
    /// <param name="isBase">
    ///   Whether to include all base configurations, include only base
    ///   configurations, or exclude all base configurations.
    /// </param>
    /// <param name="customNewFactory">
    ///   Whether to include all configurations with a custom new factory,
    ///   include only configurations with a custom new factory, or exclude all
    ///   configurations with a custom new factory.
    /// </param>
    /// <param name="customValidation">
    ///   Whether to include all configurations with custom validation, include
    ///   only configurations with custom validation, or exclude all
    ///   configurations with custom validation.
    /// </param>
    /// <param name="customActions">
    ///   Whether to include all configurations with custom actions, include
    ///   only configurations with custom actions, or exclude all configurations
    ///   with custom actions.
    /// </param>
    /// <param name="reactiveActions">
    ///   Whether to include all configurations with reactive actions, include
    ///   only configurations with reactive actions, or exclude all
    ///   configurations with reactive actions.
    /// </param>
    /// <returns>
    ///   A list of <see cref="ConfigurationInfo"/> for all configurations.
    /// </returns>
    [HttpGet]
    public ActionResult<List<ConfigurationInfo>> GetConfigurations(
        [FromQuery] string? query = null,
        [FromQuery] Guid? pluginID = null,
        [FromQuery] IncludeOnlyFilter hidden = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter isBase = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter customNewFactory = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter customValidation = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter customActions = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter reactiveActions = IncludeOnlyFilter.True
    )
    {
        var enumerable = pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { IsActive: true } pluginInfo
                ? configurationService.GetConfigurationInfo(pluginInfo.Plugin)
                : []
            : configurationService.GetAllConfigurationInfos();
        if (!string.IsNullOrEmpty(query))
            enumerable = enumerable
                .Search(query, c => [c.Name])
                .Select(c => c.Result)
                .OrderByDescending(p => typeof(CorePlugin) == p.PluginInfo.PluginType)
                .ThenBy(p => p.PluginInfo.Name)
                .ThenBy(p => p.Name)
                .ThenBy(p => p.ID);
        return enumerable
            .Where(configurationInfo =>
            {
                if (hidden is not IncludeOnlyFilter.True)
                {
                    var shouldHideHidden = hidden is IncludeOnlyFilter.False;
                    if (shouldHideHidden == configurationInfo.IsHidden)
                        return false;
                }
                if (isBase is not IncludeOnlyFilter.True)
                {
                    var shouldHideBase = isBase is IncludeOnlyFilter.False;
                    if (shouldHideBase == configurationInfo.IsBase)
                        return false;
                }
                if (customNewFactory is not IncludeOnlyFilter.True)
                {
                    var shouldHideCustomNewFactory = customNewFactory is IncludeOnlyFilter.False;
                    if (shouldHideCustomNewFactory == configurationInfo.HasCustomNewFactory)
                        return false;
                }
                if (customValidation is not IncludeOnlyFilter.True)
                {
                    var shouldHideCustomValidation = customValidation is IncludeOnlyFilter.False;
                    if (shouldHideCustomValidation == configurationInfo.HasCustomValidation)
                        return false;
                }
                if (customActions is not IncludeOnlyFilter.True)
                {
                    var shouldHideCustomActions = customActions is IncludeOnlyFilter.False;
                    if (shouldHideCustomActions == configurationInfo.HasCustomActions)
                        return false;
                }
                if (reactiveActions is not IncludeOnlyFilter.True)
                {
                    var shouldHideReactiveActions = reactiveActions is IncludeOnlyFilter.False;
                    if (shouldHideReactiveActions == configurationInfo.HasReactiveActions)
                        return false;
                }
                return true;
            })
            .Select(configurationInfo => new ConfigurationInfo(configurationInfo))
            .ToList();
    }

    /// <summary>
    /// Get the current configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <returns></returns>
    [Produces("application/json")]
    [HttpGet("{id:guid}")]
    public ActionResult GetConfiguration(Guid id)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var config = configurationService.Load(configInfo);
            return Content(configurationService.Serialize(config), "application/json");
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Overwrite the contents of the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Configuration data</param>
    /// <returns></returns>
    [HttpPut("{id:guid}")]
    public ActionResult UpdateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JToken body)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var json = body.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            configurationService.Save(configInfo, json);

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Patches the configuration with the given id using a JSON patch document.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="patchDocument">JSON patch document with operations to apply.</param>
    /// <returns></returns>
    [HttpPatch("{id:guid}")]
    public ActionResult UpdateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument patchDocument)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var config = configurationService.Load(configInfo, copy: true);
            patchDocument.ApplyTo(config);

            configurationService.Save(configInfo, config);

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Get the information about the current configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <returns></returns>
    [HttpGet("{id:guid}/Info")]
    public ActionResult<ConfigurationInfo> GetConfigurationInfo(Guid id)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        return new ConfigurationInfo(configInfo);
    }

    /// <summary>
    /// Get the schema for the current configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <returns></returns>
    [Produces("application/json")]
    [HttpGet("{id:guid}/Schema")]
    public ActionResult SchemaConfiguration(Guid id)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        return Content(configurationService.GetSchema(configInfo), "application/json");
    }

    /// <summary>
    /// Create a new configuration unused instance of the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <returns></returns>
    [Produces("application/json")]
    [HttpPost("{id:guid}/New")]
    public ActionResult NewConfiguration(Guid id)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        return Content(configurationService.Serialize(configurationService.New(configInfo)), "application/json");
    }

    /// <summary>
    /// Validate the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Configuration data</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/Validate")]
    public ActionResult ValidateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JToken body)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        var json = body.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
        var errors = configurationService.Validate(configInfo, json);
        if (errors.Count > 0)
            return ValidationProblem(errors);

        return Ok();
    }

    /// <summary>
    /// Perform an action on the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Optional. Configuration data to perform the action on.</param>
    /// <param name="actionName">Action to perform</param>
    /// <param name="actionType">Action type</param>
    /// <param name="path">Path to the configuration</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/PerformAction")]
    public ActionResult<ConfigurationActionResult> PerformActionOnConfiguration(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken? body,
        [FromQuery] string path = "",
        [FromQuery] string actionName = "",
        [FromQuery] ConfigurationActionType actionType = ConfigurationActionType.Custom
    )
    {
        if (actionType is ConfigurationActionType.Custom && string.IsNullOrEmpty(actionName))
            return ValidationProblem("Missing 'actionName' parameter for custom action!", "actionName");

        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var data = body?.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            if (!string.IsNullOrEmpty(data) && actionType is not ConfigurationActionType.Changed and not ConfigurationActionType.Saved && configurationService.Validate(configInfo, data) is { Count: > 0 } errors)
                return ValidationProblem(errors);

            var uri = new UriBuilder(
                Request.Scheme,
                Request.Host.Host,
                Request.Host.Port ?? (Request.Scheme == "https" ? 443 : 80),
                Request.PathBase,
                null
            );
            data ??= configurationService.Serialize(configurationService.Load(configInfo));
            var config = configurationService.Deserialize(configInfo, data);
            var result = actionType is ConfigurationActionType.Custom
                ? configurationService.PerformCustomAction(configInfo, config, path, actionName, User, uri.Uri)
                : configurationService.PerformReactiveAction(configInfo, config, path, actionType, User, uri.Uri);
            if (result.Configuration is not null)
            {
                var diff = new JsonDiffPatch(new() { TextDiff = TextDiffMode.Simple, DiffArrayOptions = new() { DetectMove = true, IncludeValueOnMove = true } }).Diff(data, configurationService.Serialize(result.Configuration)) ?? "{}";
                var operations = new JsonDeltaFormatter().Format(JToken.Parse(diff)).Select(op => new Operation(op.Op, op.Path, op.From, op.Value)).ToList();
                return Ok(new ConfigurationActionResult(result, operations));
            }
            return Ok(new ConfigurationActionResult(result, null));
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
        catch (InvalidConfigurationActionException ex)
        {
            return ValidationProblem(ex.Message, ex.ParamName);
        }
    }
}
