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
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

using ReleaseInfo = Shoko.Server.API.v3.Models.Common.ReleaseInfo;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ReleaseInfoController(ISettingsProvider settingsProvider, IVideoReleaseService videoReleaseService, VideoLocalRepository videoRepository) : BaseController(settingsProvider)
{
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

    [Authorize("admin")]
    [HttpPost("Provider")]
    public ActionResult UpdateMultipleReleaseProviders([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<ReleaseInfoProviderUpdateMultipleBody> body)
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

        return NoContent();
    }

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

    [Authorize("admin")]
    [HttpPut("Provider/{name}")]
    public ActionResult UpdateReleaseProviderByName([FromRoute] string name, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ReleaseInfoProviderUpdateSingleBody body)
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

        return Ok();
    }

    [Authorize("admin")]
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

    [Authorize("admin")]
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

    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(204)]
    [HttpDelete("File/{fileID}")]
    public async Task<ActionResult> RemoveReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        await videoReleaseService.ClearReleaseForVideo(video);

        return NoContent();
    }

    [Authorize("admin")]
    [ProducesResponseType(404)]
    [ProducesResponseType(201)]
    [HttpPost("File/{fileID}")]
    public async Task<ActionResult<ReleaseInfo>> SaveReleaseInfoByFileID([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ReleaseInfo body)
    {
        if (videoRepository.GetByID(fileID) is not { } video)
            return NotFound("File not found!");

        var releaseInfo = new ReleaseInfoWithProvider("User");
        // TODO: Copy properties from body to releaseInfo

        var r = await videoReleaseService.SaveReleaseForVideo(video, releaseInfo);

        return Created(Url.Action(nameof(GetReleaseInfoByFileID), new { fileID = video.VideoLocalID }), new ReleaseInfo(r));
    }

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

public class ReleaseInfoProvider
{
    public required string Name { get; set; }

    public required Version Version { get; set; }

    public required bool IsEnabled { get; set; }

    public required int Priority { get; set; }
}

public class ReleaseInfoProviderUpdateMultipleBody
{
    public required string Name { get; set; }

    public required bool? IsEnabled { get; set; }

    public required int? Priority { get; set; }
}

public class ReleaseInfoProviderUpdateSingleBody
{
    public required string Name { get; set; }

    public required bool? IsEnabled { get; set; }

    public required int? Priority { get; set; }
}
