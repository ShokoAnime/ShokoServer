using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Streaming;
using Shoko.Server.API.v3.Models.Streaming.Input;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for managing video stream transforms and playback
/// observers. Interacts with the <see cref="IVideoStreamPipelineService"/>.
/// </summary>
/// <param name="settingsProvider">Settings provider.</param>
/// <param name="streamPipelineService">Video stream pipeline service.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class VideoStreamController(ISettingsProvider settingsProvider, IVideoStreamPipelineService streamPipelineService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Gets all video stream transforms available, with their current enabled and priority states.
    /// </summary>
    /// <returns>A list of <see cref="VideoStreamTransform"/>.</returns>
    [HttpGet("Transforms")]
    public ActionResult<List<VideoStreamTransform>> GetAvailableTransforms()
        => streamPipelineService.GetAvailableTransforms()
            .Select(info => new VideoStreamTransform(info))
            .ToList();

    /// <summary>
    /// Update the enabled state and/or priority of one or more video stream transforms in the same request.
    /// </summary>
    /// <param name="body">The transforms to update.</param>
    [Authorize(Roles = "admin,init")]
    [ProducesResponseType(200)]
    [HttpPost("Transforms")]
    public ActionResult UpdateMultipleTransforms([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<UpdateMultipleTransformsBody> body)
    {
        var infoDict = streamPipelineService.GetAvailableTransforms().ToDictionary(t => t.ID);
        var changedTransforms = new List<VideoStreamTransformInfo>();
        foreach (var transform in body)
        {
            if (!infoDict.TryGetValue(transform.ID, out var info))
                continue;

            var changed = false;
            if (transform.IsEnabled.HasValue && transform.IsEnabled.Value != info.Enabled)
            {
                info.Enabled = transform.IsEnabled.Value;
                changed = true;
            }

            if (transform.Priority.HasValue && transform.Priority.Value != info.Priority)
            {
                info.Priority = transform.Priority.Value;
                changed = true;
            }

            if (changed)
                changedTransforms.Add(info);
        }

        if (changedTransforms.Count > 0)
            streamPipelineService.UpdateTransforms([.. changedTransforms]);

        return Ok();
    }

    /// <summary>
    /// Gets a specific video stream transform, with its current enabled and priority state.
    /// </summary>
    /// <param name="transformID">The ID of the video stream transform to get.</param>
    /// <returns>A <see cref="VideoStreamTransform"/>.</returns>
    [HttpGet("Transforms/{transformID}")]
    public ActionResult<VideoStreamTransform> GetTransformByID(Guid transformID)
    {
        if (streamPipelineService.GetTransformInfo(transformID) is not { } info)
            return NotFound($"Video Stream Transform '{transformID}' not found!");

        return new VideoStreamTransform(info);
    }

    /// <summary>
    /// Update the enabled state and/or priority of a specific video stream transform.
    /// </summary>
    /// <param name="transformID">The ID of the video stream transform to update.</param>
    /// <param name="body">The transform update.</param>
    /// <returns>The updated <see cref="VideoStreamTransform"/>.</returns>
    [Authorize(Roles = "admin,init")]
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Transforms/{transformID}")]
    public ActionResult<VideoStreamTransform> UpdateTransformByID([FromRoute] Guid transformID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateSingleTransformBody body)
    {
        if (streamPipelineService.GetTransformInfo(transformID) is not { } info)
            return NotFound($"Video Stream Transform '{transformID}' not found!");

        var changed = false;
        if (body.IsEnabled.HasValue && body.IsEnabled.Value != info.Enabled)
        {
            info.Enabled = body.IsEnabled.Value;
            changed = true;
        }

        if (body.Priority.HasValue && body.Priority.Value != info.Priority)
        {
            info.Priority = body.Priority.Value;
            changed = true;
        }

        if (changed)
            streamPipelineService.UpdateTransforms(info);

        return GetTransformByID(transformID);
    }

    /// <summary>
    /// Gets all playback observers available, with their current enabled state.
    /// </summary>
    /// <returns>A list of <see cref="PlaybackObserver"/>.</returns>
    [HttpGet("Observers")]
    public ActionResult<List<PlaybackObserver>> GetAvailableObservers()
        => streamPipelineService.GetAvailableObservers()
            .Select(info => new PlaybackObserver(info))
            .ToList();

    /// <summary>
    /// Update the enabled state of a specific playback observer.
    /// </summary>
    /// <param name="observerID">The ID of the playback observer to update.</param>
    /// <param name="body">The observer update.</param>
    /// <returns>The updated <see cref="PlaybackObserver"/>.</returns>
    [Authorize(Roles = "admin,init")]
    [ProducesResponseType(404)]
    [ProducesResponseType(200)]
    [HttpPut("Observers/{observerID}")]
    public ActionResult<PlaybackObserver> UpdateObserverByID([FromRoute] Guid observerID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateObserverBody body)
    {
        if (streamPipelineService.GetObserverInfo(observerID) is not { } info)
            return NotFound($"Playback Observer '{observerID}' not found!");

        if (body.IsEnabled.HasValue && body.IsEnabled.Value != info.Enabled)
        {
            info.Enabled = body.IsEnabled.Value;
            streamPipelineService.UpdateObservers(info);
        }

        if (streamPipelineService.GetObserverInfo(observerID) is not { } updated)
            return NotFound($"Playback Observer '{observerID}' not found!");

        return new PlaybackObserver(updated);
    }
}
