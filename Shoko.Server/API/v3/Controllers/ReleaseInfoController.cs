using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.API.v3.Models.Release.Input;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

using ReleaseInfo = Shoko.Server.API.v3.Models.Release.ReleaseInfo;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing release information. Interacts with the <see cref="IVideoReleaseService"/>.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="pluginManager">Plugin manager.</param>
/// <param name="videoReleaseService">Video release service.</param>
/// <param name="videoRepository">Video repository.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ReleaseInfoController(ISettingsProvider settingsProvider, IPluginManager pluginManager, IVideoReleaseService videoReleaseService, VideoLocalRepository videoRepository) : BaseController(settingsProvider)
{
    /// <summary>
    /// Gets a summary of the release information service's properties.
    /// </summary>
    /// <returns>A <see cref="ReleaseInfoSummary"/> containing the current settings.</returns>
    [HttpGet("Summary")]
    public ActionResult<ReleaseInfoSummary> GetReleaseInfoSummary()
        => new ReleaseInfoSummary
        {
            ParallelMode = videoReleaseService.ParallelMode,
            ProviderCount = videoReleaseService.GetAvailableProviders().Count(),
        };

    /// <summary>
    /// Updates the release information settings, such as the parallel mode.
    /// </summary>
    /// <param name="body">The settings to update.</param>
    /// <returns>An empty <see cref="ActionResult"/>.</returns>
    [HttpPost("Settings")]
    public ActionResult UpdateReleaseInfoSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateReleaseInfoSettingsBody body)
    {
        if (body.ParallelMode.HasValue)
            videoReleaseService.ParallelMode = body.ParallelMode.Value;

        return Ok();
    }

    /// <summary>
    /// Gets all release providers available, with their current enabled and priority states.
    /// </summary>
    /// <param name="pluginID">Optional. Plugin ID to get release providers for.</param>
    /// <returns>A list of <see cref="ReleaseInfoProvider"/>.</returns>
    [HttpGet("Provider")]
    public ActionResult<List<ReleaseInfoProvider>> GetAvailableReleaseProviders([FromQuery] Guid? pluginID = null)
        => pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { } pluginInfo
                ? videoReleaseService.GetProviderInfo(pluginInfo.Plugin)
                    .Select(providerInfo => new ReleaseInfoProvider(providerInfo))
                    .ToList()
                : []
            : videoReleaseService.GetAvailableProviders()
                .Select(providerInfo => new ReleaseInfoProvider(providerInfo))
                .ToList();

    /// <summary>
    /// Update the enabled state and/or priority of one or more release providers in the same request. 
    /// </summary>
    /// <param name="body">The providers to update.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [ProducesResponseType(200)]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleReleaseProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<UpdateMultipleProvidersBody> body)
    {
        var providerInfoDict = videoReleaseService.GetAvailableProviders().ToDictionary(p => p.ID);
        var changedProviders = new List<ReleaseProviderInfo>();
        foreach (var provider in body)
        {
            if (providerInfoDict.TryGetValue(provider.ID, out var p))
            {
                var changed = false;
                if (provider.IsEnabled.HasValue && provider.IsEnabled.Value != p.Enabled)
                {
                    p.Enabled = provider.IsEnabled.Value;
                    changed = true;
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

        if (changedProviders.Count > 0)
            videoReleaseService.UpdateProviders([.. changedProviders]);

        return Ok();
    }

    /// <summary>
    /// Gets a specific release provider, with its current enabled and priority state.
    /// </summary>
    /// <param name="providerID">The ID of the release provider to get.</param>
    /// <returns>A <see cref="ReleaseInfoProvider"/>.</returns>
    [HttpGet("Provider/{providerID}")]
    public ActionResult<ReleaseInfoProvider> GetReleaseProviderByID(Guid providerID)
    {
        if (videoReleaseService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Release Provider '{providerID}' not found!");

        return new ReleaseInfoProvider(providerInfo);
    }

    /// <summary>
    /// Update the enabled state and/or priority of a specific release provider.
    /// </summary>
    /// <param name="providerID">The ID of the release provider to update.</param>
    /// <param name="body">The provider to update.</param>
    /// <returns>The updated <see cref="ReleaseInfoProvider"/>.</returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Provider/{providerID}")]
    public ActionResult<ReleaseInfoProvider> UpdateReleaseProviderByID([FromRoute] Guid providerID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateSingleProviderBody body)
    {
        if (videoReleaseService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Release Provider '{providerID}' not found!");

        var changed = false;
        if (body.IsEnabled.HasValue && body.IsEnabled.Value != providerInfo.Enabled)
        {
            providerInfo.Enabled = body.IsEnabled.Value;
            changed = true;
        }

        if (body.Priority.HasValue && body.Priority.Value != providerInfo.Priority)
        {
            providerInfo.Priority = body.Priority.Value;
            changed = true;
        }

        if (changed)
            videoReleaseService.UpdateProviders(providerInfo);

        return GetReleaseProviderByID(providerID);
    }

    /// <summary>
    /// Preview a release by ID at a specific release provider by ID.
    /// </summary>
    /// <param name="providerID">The ID of the release provider to preview the release for.</param>
    /// <param name="id">The ID of the release to preview.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpGet("Provider/{providerID}/Preview/By-Release")]
    public async Task<ActionResult<ReleaseInfo>> GetReleaseByIDForProviderByID([FromRoute] Guid providerID, [FromQuery] string id)
    {
        if (videoReleaseService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Release Provider '{providerID}' not found!");

        if (await providerInfo.Provider.GetReleaseInfoById(id, HttpContext.RequestAborted) is not { } releaseInfo)
            return NoContent();

        return new ReleaseInfo(new ReleaseInfoWithProvider(releaseInfo, providerInfo.Provider.Name));
    }

    /// <summary>
    /// Preview a release by file ID or ED2K hash at a specific release provider by ID.
    /// </summary>
    /// <param name="providerID">The ID of the release provider to preview the release for.</param>
    /// <param name="fileID">The ID of the file to preview.</param>
    /// <param name="ed2k">The ED2K hash of the file to preview.</param>
    /// <param name="fileSize">The size of the file to preview.</param>
    /// <returns>The previewed release.</returns>
    [Authorize("admin")]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpGet("Provider/{providerID}/Preview/By-File")]
    public async Task<ActionResult<ReleaseInfo>> GetReleaseByIDForProviderByID([FromRoute] Guid providerID, [FromQuery, Range(1, int.MaxValue)] int? fileID = null, [FromQuery] string? ed2k = null, [FromQuery] long? fileSize = null)
    {
        if (videoReleaseService.GetProviderInfo(providerID) is not { } providerInfo)
            return NotFound($"Release Provider '{providerID}' not found!");

        IVideo video;
        if (fileID.HasValue)
        {
            if (videoRepository.GetByID(fileID.Value) is not { } v)
                return NotFound($"File by ID {fileID} not found!");
            video = v;
        }
        else if (ed2k is not null)
        {
            if (ed2k is not { Length: 32 })
                return BadRequest("ED2K must be exactly 32 characters long!");

            if (fileSize.HasValue && fileSize.Value > 0)
            {
                if (videoRepository.GetByEd2kAndSize(ed2k, fileSize.Value) is not { } v)
                    return NotFound($"File by ED2K {ed2k} & file size {fileSize} not found!");
                video = v;
            }
            else
            {
                if (videoRepository.GetByEd2k(ed2k) is not { } v)
                    return NotFound($"File by ED2k {ed2k} not found!");
                video = v;
            }
        }
        else
        {
            return BadRequest("File ID or ED2K must be provided!");
        }

        if (await providerInfo.Provider.GetReleaseInfoForVideo(video, HttpContext.RequestAborted) is not { } releaseInfo)
            return NoContent();

        return Ok(new ReleaseInfo(new ReleaseInfoWithProvider(releaseInfo, providerInfo.Provider.Name)));
    }

    /// <summary>
    /// Get the <see cref="ReleaseInfo"/> for a file using the <paramref name="fileID"/>
    /// </summary>
    /// <param name="fileID">File ID</param>
    /// <returns>The current <see cref="ReleaseInfo"/> for the <paramref name="fileID"/>, or a 204 response if none is available.</returns>
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpGet("File/{fileID}")]
    public ActionResult<ReleaseInfo> GetReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        if (videoReleaseService.GetCurrentReleaseForVideo(video) is not { } releaseInfo)
            return NoContent();

        return new ReleaseInfo(releaseInfo);
    }

    /// <summary>
    /// Remove the current <see cref="ReleaseInfo"/> for a file using the <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpDelete("File/{fileID}")]
    public async Task<ActionResult> RemoveReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        await videoReleaseService.ClearReleaseForVideo(video);

        return Ok();
    }

    /// <summary>
    /// Save a <see cref="ReleaseInfo"/> for a file using the <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">File ID.</param>
    /// <param name="body">The <see cref="ReleaseInfo"/> to save.</param>
    /// <returns>The newly saved <see cref="ReleaseInfo"/>.</returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(201)]
    [HttpPost("File/{fileID}")]
    public async Task<ActionResult<ReleaseInfo>> SaveReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ReleaseInfo body)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        var r = await videoReleaseService.SaveReleaseForVideo(video, body);

        return Created(Url.Action(nameof(GetReleaseInfoByFileID), new { fileID = video.VideoLocalID }), new ReleaseInfo(r));
    }

    /// <summary>
    /// Preview an automatic search for a <see cref="ReleaseInfo"/> for a file, disregarding any existing info, using the <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">File ID</param>
    /// <returns>The current automatic <see cref="ReleaseInfo"/> for the <paramref name="fileID"/>, or a 204 response if none is available.</returns>
    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpPost("File/{fileID}/AutoPreview")]
    public async Task<ActionResult<ReleaseInfo>> AutoPreviewReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        if (await videoReleaseService.FindReleaseForVideo(video, saveRelease: false, cancellationToken: HttpContext.RequestAborted) is not { } releaseInfo)
            return NoContent();

        return Ok(new ReleaseInfo(releaseInfo));
    }
}

