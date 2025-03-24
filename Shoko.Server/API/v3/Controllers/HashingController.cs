using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Hashing;
using Shoko.Server.API.v3.Models.Hashing.Input;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing hashing. Interacts with the <see cref="IVideoHashingService"/>.
/// </summary>
/// <param name="settingsProvider"></param>
/// <param name="pluginManager"></param>
/// <param name="videoHashingService"></param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class HashingController(ISettingsProvider settingsProvider, IPluginManager pluginManager, IVideoHashingService videoHashingService) : BaseController(settingsProvider)
{
    [HttpGet("Summary")]
    public ActionResult<HashingSummary> GetHashSummary()
        => new HashingSummary
        {
            ParallelMode = videoHashingService.ParallelMode,
            ProviderCount = videoHashingService.GetAvailableProviders().Count(),
            AllAvailableHashTypes = videoHashingService.AllAvailableHashTypes,
            AllEnabledHashTypes = videoHashingService.AllEnabledHashTypes,
        };

    [Authorize("admin")]
    [HttpPost("Settings")]
    public ActionResult UpdateHashingSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateHashingSettingsBody body)
    {
        if (body.ParallelMode != null)
            videoHashingService.ParallelMode = body.ParallelMode.Value;

        return Ok();
    }

    /// <summary>
    /// Gets all hash providers available, with their current enabled and priority states.
    /// </summary>
    /// <param name="pluginID">Optional. Plugin ID to get hash providers for.</param>
    /// <returns>A list of <see cref="HashProvider"/>.</returns>
    [HttpGet("Provider")]
    public ActionResult<List<HashProvider>> GetAvailableHashProviders([FromQuery] Guid? pluginID = null)
        => pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { } pluginInfo
                ? videoHashingService.GetProviderInfo(pluginInfo.Plugin)
                    .Select(providerInfo => new HashProvider(providerInfo))
                    .ToList()
                : []
            : videoHashingService.GetAvailableProviders()
                .Select(providerInfo => new HashProvider(providerInfo))
                .ToList();

    /// <summary>
    /// Update the enabled state and/or priority of one or more hash providers in the same request. 
    /// </summary>
    /// <param name="body">The providers to update.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [ProducesResponseType(200)]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleHashProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<UpdateMultipleProvidersBody> body)
    {
        var providerInfoDict = videoHashingService.GetAvailableProviders().ToDictionary(p => p.ID);
        var changedProviders = new List<HashProviderInfo>();
        foreach (var provider in body)
        {
            if (providerInfoDict.TryGetValue(provider.ID, out var p))
            {
                var changed = false;
                if (provider.EnabledHashTypes is not null)
                {
                    var invalidHashes = provider.EnabledHashTypes
                        .Except(p.Provider.AvailableHashTypes)
                        .ToHashSet();
                    if (invalidHashes.Count > 0)
                    {
                        foreach (var at in invalidHashes)
                            ModelState.AddModelError($"EnabledHashTypes.{at}", $"Hash type '{at}' is not available for this provider.");
                    }
                    else
                    {
                        p.EnabledHashTypes = provider.EnabledHashTypes;
                        changed = true;
                    }
                }

                if (changed)
                    changedProviders.Add(p);
            }
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (changedProviders.Count > 0)
            videoHashingService.UpdateProviders([.. changedProviders]);

        return Ok();
    }

    /// <summary>
    /// Gets a specific hash provider, with its current enabled and priority state.
    /// </summary>
    /// <param name="providerID">The ID of the hash provider to get.</param>
    /// <returns>A <see cref="HashProvider"/>.</returns>
    [HttpGet("Provider/{providerID}")]
    public ActionResult<HashProvider> GetHashProviderByID(Guid providerID)
    {
        if (videoHashingService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Hash Provider '{providerID}' not found!");

        return new HashProvider(providerInfo);
    }

    /// <summary>
    /// Update the enabled state and/or priority of a specific release provider.
    /// </summary>
    /// <param name="providerID">The ID of the hash provider to update.</param>
    /// <param name="body">The provider to update.</param>
    /// <returns>The updated <see cref="HashProvider"/>.</returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Provider/{providerID}")]
    public ActionResult<HashProvider> UpdateHashProviderByID([FromRoute] Guid providerID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateSingleProviderBody body)
    {
        if (videoHashingService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Hash Provider '{providerID}' not found!");

        var changed = false;
        if (body.EnabledHashTypes is not null)
        {
            var invalidHashes = body.EnabledHashTypes
                .Except(providerInfo.Provider.AvailableHashTypes)
                .ToHashSet();
            if (invalidHashes.Count > 0)
            {
                foreach (var at in invalidHashes)
                    ModelState.AddModelError($"EnabledHashTypes[\"{at}\"]", $"Hash type '{at}' is not available for this provider.");
            }
            else
            {
                providerInfo.EnabledHashTypes = body.EnabledHashTypes;
                changed = true;
            }
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (changed)
            videoHashingService.UpdateProviders(providerInfo);

        return GetHashProviderByID(providerID);
    }
}

