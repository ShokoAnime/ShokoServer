using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.Settings;

using HashProvider = Shoko.Server.API.v3.Models.Hashing.HashProvider;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class HashingController(ISettingsProvider settingsProvider, IVideoHashingService videoHashingService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Gets all hash providers available, with their current enabled and priority states.
    /// </summary>
    /// <returns>A list of <see cref="HashProvider"/>.</returns>
    [HttpGet("Provider")]
    public ActionResult<List<HashProvider>> GetAvailableHashProviders()
        => videoHashingService.GetAvailableProviders()
        .Select(p => new HashProvider
        {
            ID = p.ID,
            Name = p.Provider.Name,
            Version = p.Provider.Version,
            EnabledHashTypes = p.EnabledHashTypes,
            Priority = p.Priority,
        })
        .ToList();

    /// <summary>
    /// Update the enabled state and/or priority of one or more hash providers in the same request. 
    /// </summary>
    /// <param name="body">The providers to update.</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleHashProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<HashProvider.Input.UpdateMultipleProvidersBody> body)
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

                if (provider.Priority.HasValue && provider.Priority.Value != p.Priority)
                {
                    p.Priority = provider.Priority.Value;
                    changed = true;
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

        return new HashProvider
        {
            ID = providerInfo.ID,
            Name = providerInfo.Provider.Name,
            Version = providerInfo.Provider.Version,
            EnabledHashTypes = providerInfo.EnabledHashTypes,
            Priority = providerInfo.Priority,
        };
    }

    /// <summary>
    /// Update the enabled state and/or priority of a specific release provider.
    /// </summary>
    /// <param name="providerID">The ID of the hash provider to update.</param>
    /// <param name="body">The provider to update.</param>
    /// <returns>The updated <see cref="HashProvider"/>.</returns>
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Provider/{providerID}")]
    public ActionResult<HashProvider> UpdateHashProviderByID([FromRoute] Guid providerID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] HashProvider.Input.UpdateSingleProviderBody body)
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
        if (body.Priority.HasValue && body.Priority.Value != providerInfo.Priority)
        {
            providerInfo.Priority = body.Priority.Value;
            changed = true;
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (changed)
            videoHashingService.UpdateProviders(providerInfo);

        return GetHashProviderByID(providerID);
    }
}

