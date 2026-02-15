using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using ConfigurationActionType = Shoko.Abstractions.Config.Enums.ConfigurationActionType;

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
    private Uri? _baseUri;

    public Uri BaseUri => _baseUri ??= new UriBuilder(
                Request.Scheme,
                Request.Host.Host,
                Request.Host.Port ?? (Request.Scheme == "https" ? 443 : 80),
                Request.PathBase,
                null
            ).Uri;

    /// <summary>
    ///   Get a list with information about all registered configurations.
    /// </summary>
    /// <param name="query">
    ///   An optional query to filter configurations by name.
    /// </param>
    /// <param name="pluginID">
    ///   Whether to include only configurations for a specific plugin, or all
    ///   configurations if omitted.
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
    /// <param name="customSave">
    ///   Whether to include all configurations with custom save, include only
    ///   configurations with custom save, or exclude all configurations with
    ///   custom save.
    /// </param>
    /// <param name="customLoad">
    ///   Whether to include all configurations with custom load, include only
    ///   configurations with custom load, or exclude all configurations with
    ///   custom load.
    /// </param>
    /// <param name="liveEdit">
    ///   Whether to include all configurations with live edit support, include
    ///   only configurations with live edit support, or exclude all
    ///   configurations with live edit support.
    /// </param>
    /// <returns>
    ///   A list of <see cref="ConfigurationInfo"/> for the configurations
    ///   matching the query.
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
        [FromQuery] IncludeOnlyFilter customSave = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter customLoad = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter liveEdit = IncludeOnlyFilter.True
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
                if (customSave is not IncludeOnlyFilter.True)
                {
                    var shouldHideCustomSave = customSave is IncludeOnlyFilter.False;
                    if (shouldHideCustomSave == configurationInfo.HasCustomSave)
                        return false;
                }
                if (customLoad is not IncludeOnlyFilter.True)
                {
                    var shouldHideCustomLoad = customLoad is IncludeOnlyFilter.False;
                    if (shouldHideCustomLoad == configurationInfo.HasCustomLoad)
                        return false;
                }
                if (liveEdit is not IncludeOnlyFilter.True)
                {
                    var shouldHideReactiveActions = liveEdit is IncludeOnlyFilter.False;
                    if (shouldHideReactiveActions == configurationInfo.HasLiveEdit)
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
            if (configInfo.HasCustomLoad)
            {
                var tempConfig = configurationService.New(configInfo);
                var result = configurationService.PerformReactiveAction(configInfo, tempConfig, "", ConfigurationActionType.Load, default, User, BaseUri);
                if (result.ValidationErrors is { Count: > 0 })
                    return ValidationProblem(result.ValidationErrors);
                if (result.Configuration is not null)
                    return Content(configurationService.Serialize(result.Configuration), "application/json");
                return Conflict("Unable to load custom configuration object for the user.");
            }

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
    public ActionResult<ConfigurationActionResult> UpdateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JToken body)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var json = body.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            if (configInfo.HasCustomSave)
            {
                if (configurationService.Validate(configInfo, json) is { Count: > 0 } errors)
                    return Ok(new ConfigurationActionResult() { ValidationErrors = errors });

                var config = configurationService.Deserialize(configInfo, json);
                var result = configurationService.PerformReactiveAction(configInfo, config, "", ConfigurationActionType.Save, default, User, BaseUri);
                return Ok(new ConfigurationActionResult(result, configurationService, json));
            }

            var modified = configurationService.Save(configInfo, json);
            return Ok(new ConfigurationActionResult() { ShowSaveMessage = modified, Refresh = modified });
        }
        catch (ConfigurationValidationException ex)
        {
            return Ok(new ConfigurationActionResult() { ValidationErrors = ex.ValidationErrors });
        }
    }

    /// <summary>
    /// Patches the configuration with the given id using a JSON patch document.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="patchDocument">JSON patch document with operations to apply.</param>
    /// <returns></returns>
    [HttpPatch("{id:guid}")]
    public ActionResult<ConfigurationActionResult> PartiallyUpdateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument patchDocument)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var config = configurationService.Load(configInfo, copy: true);
            patchDocument.ApplyTo(config);

            if (configInfo.HasCustomSave)
            {
                var json = configurationService.Serialize(config);
                var result = configurationService.PerformReactiveAction(configInfo, config, "", ConfigurationActionType.Save, default, User, BaseUri);
                return Ok(new ConfigurationActionResult(result, configurationService, json));
            }

            var modified = configurationService.Save(configInfo, config);
            return Ok(new ConfigurationActionResult() { ShowSaveMessage = modified, Refresh = modified });
        }
        catch (ConfigurationValidationException ex)
        {
            return Ok(new ConfigurationActionResult() { ValidationErrors = ex.ValidationErrors });
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

        var config = configurationService.New(configInfo);
        var json = configurationService.Serialize(config);
        if (configInfo.HasCustomNewFactory)
        {
            var result = configurationService.PerformReactiveAction(configInfo, config, "", ConfigurationActionType.New, default, User, BaseUri);
            if (result.ValidationErrors is { Count: > 0 })
                return ValidationProblem(result.ValidationErrors);
            if (result.Configuration is not null)
                return Content(configurationService.Serialize(result.Configuration), "application/json");
            return Conflict("Unable to create a new custom configuration object for the user.");
        }

        return Content(json, "application/json");
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
    public ActionResult<ConfigurationActionResult> ValidateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JToken body)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        var json = body.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
        var errors = configurationService.Validate(configInfo, json);
        if (errors.Count > 0)
            return Ok(new ConfigurationActionResult() { ValidationErrors = errors });

        if (configInfo.HasCustomValidation)
        {
            var config = configurationService.Deserialize(configInfo, json);
            var result = configurationService.PerformReactiveAction(configInfo, config, "", ConfigurationActionType.Validate, default, User, BaseUri);
            if (result.ValidationErrors is { Count: > 0 })
                return Ok(new ConfigurationActionResult() { ValidationErrors = result.ValidationErrors });
        }

        return Ok(new ConfigurationActionResult());
    }

    /// <summary>
    /// Perform an action on the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Optional. Configuration data to perform the action on.</param>
    /// <param name="actionName">Action to perform</param>
    /// <param name="path">Path to the configuration</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/PerformAction")]
    public ActionResult<ConfigurationActionResult> PerformActionOnConfiguration(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken? body,
        [FromQuery] string path = "",
        [FromQuery] string actionName = ""
    )
    {
        if (string.IsNullOrEmpty(actionName))
            return ValidationProblem("Missing 'actionName' parameter for custom action!", "actionName");

        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var json = body?.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            json ??= configurationService.Serialize(configurationService.Load(configInfo));
            var config = configurationService.Deserialize(configInfo, json);
            var result = configurationService.PerformCustomAction(configInfo, config, path, actionName, User, BaseUri);
            return Ok(new ConfigurationActionResult(result, configurationService, json));
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

    /// <summary>
    /// Perform an action on the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Optional. Configuration data to perform the action on.</param>
    /// <param name="reactiveEventType">Reactive event type to perform the action on.</param>
    /// <param name="path">Path to the configuration</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/LiveEdit")]
    public ActionResult<ConfigurationActionResult> PerformActionOnConfiguration(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JToken body,
        [FromQuery] ReactiveEventType reactiveEventType = ReactiveEventType.All,
        [FromQuery] string path = ""
    )
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var json = body?.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]) ?? "null";
            var config = configurationService.Deserialize(configInfo, json);
            if (config is null)
                return ValidationProblem("Unable to deserialize configuration!");
            var result = configurationService.PerformReactiveAction(configInfo, config, path, ConfigurationActionType.LiveEdit, reactiveEventType, User, BaseUri);
            return Ok(new ConfigurationActionResult(result, configurationService, json));
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
