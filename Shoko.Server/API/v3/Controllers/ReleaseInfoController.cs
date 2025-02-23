using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

using ReleaseInfo = Shoko.Server.API.v3.Models.Release.ReleaseInfo;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class ReleaseInfoController(ISettingsProvider settingsProvider, IVideoReleaseService videoReleaseService, VideoLocalRepository videoRepository) : BaseController(settingsProvider)
{
    /// <summary>
    /// Gets all release providers available, with their current enabled and priority states.
    /// </summary>
    /// <returns>A list of <see cref="ReleaseInfoProvider"/>.</returns>
    [HttpGet("Provider")]
    public ActionResult<List<ReleaseInfoProvider>> GetAvailableReleaseProviders()
        => videoReleaseService.GetAvailableProviders()
        .Select(p => new ReleaseInfoProvider
        {
            Name = p.Provider.Name,
            Version = p.Provider.Version,
            IsEnabled = p.Enabled,
            Priority = p.Priority,
        })
        .ToList();

    /// <summary>
    /// Update the enabled state and/or priority of one or more release providers in the same request. 
    /// </summary>
    /// <param name="body">The providers to update.</param>
    /// <returns></returns>
    [ProducesResponseType(200)]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleReleaseProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<ReleaseInfoProvider.Input.UpdateMultipleProvidersBody> body)
    {
        var providers = videoReleaseService.GetAvailableProviders().ToDictionary(p => p.Provider.Name);
        var changedProviders = new List<ReleaseInfoProviderInfo>();
        foreach (var provider in body)
        {
            if (providers.TryGetValue(provider.Name, out var p))
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
    /// <param name="name">The name of the release provider to get.</param>
    /// <returns>A <see cref="ReleaseInfoProvider"/>.</returns>
    [HttpGet("Provider/{name}")]
    public ActionResult<ReleaseInfoProvider> GetReleaseProviderByName(string name)
    {
        if (videoReleaseService.GetAvailableProviders().FirstOrDefault(p => p.Provider.Name == name) is not { } provider)
            return NotFound($"Release Provider '{name}' not found!");

        return new ReleaseInfoProvider
        {
            Name = provider.Provider.Name,
            Version = provider.Provider.Version,
            IsEnabled = provider.Enabled,
            Priority = provider.Priority,
        };
    }

    /// <summary>
    /// Update the enabled state and/or priority of a specific release provider.
    /// </summary>
    /// <param name="name">The name of the release provider to update.</param>
    /// <param name="body">The provider to update.</param>
    /// <returns>The updated <see cref="ReleaseInfoProvider"/>.</returns>
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Provider/{name}")]
    public ActionResult<ReleaseInfoProvider> UpdateReleaseProviderByName([FromRoute] string name, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ReleaseInfoProvider.Input.UpdateSingleProviderBody body)
    {
        if (videoReleaseService.GetAvailableProviders().FirstOrDefault(p => p.Provider.Name == name) is not { } provider)
            return NotFound($"Release Provider '{name}' not found!");

        var changed = false;
        if (body.IsEnabled.HasValue && body.IsEnabled.Value != provider.Enabled)
        {
            provider.Enabled = body.IsEnabled.Value;
            changed = true;
        }

        if (body.Priority.HasValue && body.Priority.Value != provider.Priority)
        {
            provider.Priority = body.Priority.Value;
            changed = true;
        }

        if (changed)
            videoReleaseService.UpdateProviders(provider);

        return GetReleaseProviderByName(name);
    }

    /// <summary>
    /// Preview a release by ID at a specific release provider by name.
    /// </summary>
    /// <param name="name">The name of the release provider to preview the release for.</param>
    /// <param name="id">The ID of the release to preview.</param>
    /// <returns></returns>
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpGet("Provider/{name}/Preview/By-Release")]
    public async Task<ActionResult<ReleaseInfo>> GetReleaseByIDForProviderByName([FromRoute] string name, [FromQuery] string id)
    {
        if (videoReleaseService.GetProviderByName(name) is not { } provider)
            return NotFound($"Release Provider '{name}' not found!");

        if (await provider.GetReleaseInfoById(id, HttpContext.RequestAborted) is not { } releaseInfo)
            return NoContent();

        return new ReleaseInfo(new ReleaseInfoWithProvider(releaseInfo, provider.Name));
    }

    /// <summary>
    /// Preview a release by file ID or ED2K hash at a specific release provider by name.
    /// </summary>
    /// <param name="name">The name of the release provider to preview the release for.</param>
    /// <param name="fileID">The ID of the file to preview.</param>
    /// <param name="ed2k">The ED2K hash of the file to preview.</param>
    /// <param name="fileSize">The size of the file to preview.</param>
    /// <returns>The previewed release.</returns>
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ReleaseInfo), 200)]
    [HttpGet("Provider/{name}/Preview/By-File")]
    public async Task<ActionResult<ReleaseInfo>> GetReleaseByIDForProviderByName([FromRoute] string name, [FromQuery, Range(1, int.MaxValue)] int? fileID = null, [FromQuery] string? ed2k = null, [FromQuery] long? fileSize = null)
    {
        if (videoReleaseService.GetProviderByName(name) is not { } provider)
            return NotFound($"Release Provider '{name}' not found!");

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

        if (await provider.GetReleaseInfoForVideo(video, HttpContext.RequestAborted) is not { } releaseInfo)
            return NoContent();

        return Ok(new ReleaseInfo(new ReleaseInfoWithProvider(releaseInfo, provider.Name)));
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

