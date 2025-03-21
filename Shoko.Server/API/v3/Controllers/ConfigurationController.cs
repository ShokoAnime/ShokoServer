using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.Settings;

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
    /// Get a list with information about all configurations.
    /// </summary>
    /// <param name="pluginID">Optional. Plugin ID to get configurations for.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ConfigurationInfo>> GetConfigurations([FromQuery] Guid? pluginID = null)
        => pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { } pluginInfo
                ? configurationService.GetConfigurationInfo(pluginInfo.Plugin)
                    .Select(i => new ConfigurationInfo(i))
                    .ToList()
                : []
            : configurationService.GetAllConfigurationInfos()
                .Select(i => new ConfigurationInfo(i))
                .ToList();

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
    /// <param name="action">Action to perform</param>
    /// <param name="path">Path to the configuration</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/PerformAction")]
    public ActionResult<ConfigurationActionResult> PerformActionOnConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken? body, [Required, FromQuery] string action, [Required, FromQuery] string path)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        try
        {
            var data = body?.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            if (!string.IsNullOrEmpty(data) && configurationService.Validate(configInfo, data) is { Count: > 0 } errors)
                return ValidationProblem(errors);

            var config = string.IsNullOrEmpty(data) ? configurationService.Load(configInfo) : configurationService.Deserialize(configInfo, data);
            var result = configurationService.PerformAction(configInfo, config, path, action);
            return Ok(new ConfigurationActionResult(result));
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
