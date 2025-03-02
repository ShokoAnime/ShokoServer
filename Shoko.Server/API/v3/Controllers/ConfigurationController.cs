using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing configurations.
/// </summary>
/// <param name="settingsProvider"></param>
/// <param name="configurationService"></param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class ConfigurationController(ISettingsProvider settingsProvider, IConfigurationService configurationService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get a list with information about all configurations.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ConfigurationInfo>> GetAllConfigurations() => configurationService.GetAllConfigurationInfos()
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
            foreach (var (path, messages) in ex.ValidationErrors)
                foreach (var message in messages)
                    ModelState.AddModelError(path, message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Overwrite the contents of the configuration with the given id.
    /// </summary>
    /// <param name="id">Configuration id</param>
    /// <param name="body">Configuration data</param>
    /// <returns></returns>
    [HttpPut("{id:guid}")]
    public ActionResult UpdateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JValue body)
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
            foreach (var (path, messages) in ex.ValidationErrors)
                foreach (var message in messages)
                    ModelState.AddModelError(path, message);
            return ValidationProblem(ModelState);
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
            foreach (var (path, messages) in ex.ValidationErrors)
                foreach (var message in messages)
                    ModelState.AddModelError(path, message);
            return ValidationProblem(ModelState);
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
    /// <param name="data">Configuration data</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [HttpPost("{id:guid}/Validate")]
    public ActionResult ValidateConfiguration(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] string data)
    {
        if (configurationService.GetConfigurationInfo(id) is not { } configInfo)
            return NotFound($"Configuration '{id}' not found!");

        var errors = configurationService.Validate(configInfo, data);
        if (errors.Count is 0)
            return Ok();

        foreach (var (path, messages) in errors)
            foreach (var message in messages)
                ModelState.AddModelError(path, message);
        return ValidationProblem(ModelState);
    }
}
