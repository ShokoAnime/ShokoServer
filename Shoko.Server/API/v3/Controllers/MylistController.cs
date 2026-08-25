using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Mylist;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services.Mylist;
using Shoko.Server.Settings;

using AbstractMylistSyncPlan = Shoko.Abstractions.Metadata.Anidb.Models.MylistSyncPlan;
using MylistSyncPlan = Shoko.Server.API.v3.Models.Mylist.MylistSyncPlan;
using V3Action = Shoko.Server.API.v3.Models.Mylist.MylistSyncAction;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Inspect what a MyList sync would do before letting it do anything.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/AniDB/MyList")]
[ApiV3]
[Authorize("admin")]
public class MylistController(
    ISettingsProvider settingsProvider,
    IMylistService mylistService,
    AnimeGroupRepository groups,
    AnimeSeriesRepository series,
    AnimeEpisodeRepository episodes,
    AniDB_EpisodeRepository anidbEpisodes,
    MylistCache mylistCache,
    JMMUserRepository users,
    VideoLocal_UserRepository videoLocalUsers,
    VideoLocalRepository videos
) : BaseController(settingsProvider)
{
    private const string SyncInProgress = "A MyList sync is already running.";

    /// <summary>
    /// Work out what a full MyList sync would do, without doing any of it.
    /// Nothing local is written and nothing is sent to AniDB, though the MyList
    /// itself is still fetched, since the plan is derived from it.
    /// </summary>
    /// <param name="options">
    /// Optional. Sync options to plan against. Null fields fall back to the
    /// configured server settings.
    /// </param>
    /// <returns>The steps the sync would take, and the totals behind them.</returns>
    [HttpPost("Sync/Plan")]
    public Task<ActionResult<MylistSyncPlan>> PlanSync([FromBody] MylistSyncOptions? options = null)
        => PlanAsync(o => mylistService.SyncAsync(o, HttpContext.RequestAborted), options);

    /// <inheritdoc cref="PlanSync"/>
    /// <param name="groupID">The group to confine the preview to.</param>
    /// <param name="options">
    /// Optional. Sync options to plan against. Null fields fall back to the
    /// configured server settings.
    /// </param>
    [HttpPost("Sync/Plan/Group/{groupID}")]
    public Task<ActionResult<MylistSyncPlan>> PlanSyncForGroup(
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromBody] MylistSyncOptions? options = null
        )
        => groups.GetByID(groupID) is not { } group
            ? NotFoundTask<MylistSyncPlan>($"No group with ID {groupID}.")
            : PlanAsync(o => mylistService.SyncAsync(((IShokoGroup)group).AllSeries.SelectMany(a => a.Episodes), o, HttpContext.RequestAborted), options);

    /// <inheritdoc cref="PlanSync"/>
    /// <param name="seriesID">The series to confine the preview to.</param>
    /// <param name="options">
    /// Optional. Sync options to plan against. Null fields fall back to the
    /// configured server settings.
    /// </param>
    [HttpPost("Sync/Plan/Series/{seriesID}")]
    public Task<ActionResult<MylistSyncPlan>> PlanSyncForSeries([FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody] MylistSyncOptions? options = null)
        => series.GetByID(seriesID) is not { } shokoSeries
            ? NotFoundTask<MylistSyncPlan>($"No series with ID {seriesID}.")
            : PlanAsync(o => mylistService.SyncAsync(((IShokoSeries)shokoSeries).Episodes, o, HttpContext.RequestAborted), options);

    /// <inheritdoc cref="PlanSync"/>
    /// <param name="episodeID">The episode to confine the preview to.</param>
    /// <param name="options">
    /// Optional. Sync options to plan against. Null fields fall back to the
    /// configured server settings.
    /// </param>
    [HttpPost("Sync/Plan/Episode/{episodeID}")]
    public Task<ActionResult<MylistSyncPlan>> PlanSyncForEpisode([FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromBody] MylistSyncOptions? options = null)
        => episodes.GetByID(episodeID) is not { } episode
            ? NotFoundTask<MylistSyncPlan>($"No episode with ID {episodeID}.")
            : PlanAsync(o => mylistService.SyncAsync([(IShokoEpisode)episode], o, HttpContext.RequestAborted), options);

    /// <inheritdoc cref="PlanSync"/>
    /// <param name="fileID">The file to confine the preview to.</param>
    /// <param name="options">
    /// Optional. Sync options to plan against. Null fields fall back to the
    /// configured server settings.
    /// </param>
    [HttpPost("Sync/Plan/File/{fileID}")]
    public Task<ActionResult<MylistSyncPlan>> PlanSyncForFile([FromRoute, Range(1, int.MaxValue)] int fileID,
        [FromBody] MylistSyncOptions? options = null)
        => videos.GetByID(fileID) is not { } video
            ? NotFoundTask<MylistSyncPlan>($"No file with ID {fileID}.")
            : PlanAsync(o => mylistService.SyncAsync([(IVideo)video], o, HttpContext.RequestAborted), options);

    /// <summary>
    /// Carry out a plan from one of the endpoints above, after whatever review
    /// or narrowing the caller wants to do. Steps can be dropped from the plan
    /// before sending it back; they are independent of each other.
    /// </summary>
    /// <param name="plan">The plan to carry out.</param>
    /// <returns>The plan that was applied.</returns>
    [HttpPost("Sync/Plan/Apply")]
    public async Task<ActionResult<MylistSyncPlan>> ApplySyncPlan([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApplyMylistSyncPlanBody plan)
    {
        // the ids only exist out here, so unresolvable ones are caught here;
        // whether the steps themselves make sense is the service's judgement
        if (!TryResolvePlan(plan, out var resolved))
            return ValidationProblem(ModelState);

        try
        {
            var result = await mylistService.ApplySyncPlanAsync(resolved, HttpContext.RequestAborted);
            return result is null ? new ConflictObjectResult(SyncInProgress) : ToV3(result.Plan);
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Forces the plan-only flag on rather than trusting the body, so no request
    /// to this controller can change anything, and maps the "already running"
    /// answer onto a conflict.
    /// </summary>
    private async Task<ActionResult<MylistSyncPlan>> PlanAsync(Func<MylistSyncOptions, Task<MylistSyncResult?>> sync, MylistSyncOptions? options)
    {
        var result = await sync((options ?? new()) with { PlanOnly = true });
        return result is null ? new ConflictObjectResult(SyncInProgress) : ToV3(result.Plan);
    }

    /// <summary>
    /// Projects a plan for the wire. Each step carries the entities it acts on
    /// so a client need not look them up, and their ids so the same body can be
    /// posted straight back to apply it.
    /// </summary>
    private MylistSyncPlan ToV3(AbstractMylistSyncPlan plan)
        => new(plan, [.. plan.Actions.Select(action => new V3Action
        {
            Kind = action.Kind,
            Direction = action.Direction,
            Description = action.Description,
            FileID = action.Video?.ID,
            AnidbEpisodeID = action.AnidbEpisode?.ID,
            MylistID = action.Entry is { MylistID: not 0 } ? action.Entry.MylistID : null,
            File = action.Video is VideoLocal video ? new File(HttpContext, video) : null,
            Episode = action.ShokoEpisode is AnimeEpisode episode ? new Episode(HttpContext, episode) : null,
            Entry = action.Entry,
            FileUserData = action.VideoUserData is { } videoUserData ? new File.FileUserData(videoUserData) : null,
            EpisodeUserData = action.EpisodeUserData is { } episodeUserData ? new Episode.EpisodeUserData(episodeUserData) : null,
        })]);

    /// <summary>
    /// Resolves a posted plan back into one the service can carry out, adding a
    /// model error for anything that does not add up. Only ids cross the wire,
    /// so an id that no longer resolves is reported rather than quietly
    /// producing a step with too little to act on.
    /// </summary>
    private bool TryResolvePlan(ApplyMylistSyncPlanBody plan, out AbstractMylistSyncPlan resolved)
    {
        // stamped here rather than taken from the body: this plan is being
        // built now, out of the caller's selection
        resolved = new() { CreatedAt = DateTime.UtcNow, Actions = [] };
        if (plan.Actions.Count is 0)
        {
            ModelState.AddModelError(nameof(plan.Actions), "Provide at least one action to apply.");
            return false;
        }

        var settings = SettingsProvider.GetSettings();
        var anidbUser = users.GetAniDBUser();
        var actions = new List<Abstractions.Metadata.Anidb.Models.MylistSyncAction>(plan.Actions.Count);
        for (var index = 0; index < plan.Actions.Count; index++)
        {
            var action = plan.Actions[index];
            var prefix = $"{nameof(plan.Actions)}[{index}]";

            var video = (IVideo?)null;
            if (action.FileID is { } fileID)
            {
                video = videos.GetByID(fileID);
                if (video is null)
                    ModelState.AddModelError($"{prefix}.{nameof(action.FileID)}", $"No file with id {fileID}");
            }

            var anidbEpisode = (IAnidbEpisode?)null;
            var shokoEpisode = (IShokoEpisode?)null;
            if (action.AnidbEpisodeID is { } anidbEpisodeID)
            {
                anidbEpisode = anidbEpisodes.GetByEpisodeID(anidbEpisodeID);
                // paired deliberately: the local episode is only carried
                // alongside the AniDB one it belongs to
                shokoEpisode = anidbEpisode is null ? null : episodes.GetByAniDBEpisodeID(anidbEpisodeID);
                if (anidbEpisode is null)
                    ModelState.AddModelError($"{prefix}.{nameof(action.AnidbEpisodeID)}", $"No AniDB episode with id {anidbEpisodeID}");
            }

            // one of the two is required: without either there is nothing to act
            // on, and the list id alone cannot say which file or episode is meant
            if (action.FileID is null && action.AnidbEpisodeID is null)
                ModelState.AddModelError(prefix, "Provide a file id or an AniDB episode id.");

            var entry = (MylistEntry?)null;
            if (action.MylistID is { } listID and not 0)
            {
                entry = mylistCache.GetByLid(listID);
                if (entry is null)
                    ModelState.AddModelError($"{prefix}.{nameof(action.MylistID)}", $"No known MyList entry with list id {listID}");
            }

            // every kind needs something it can act on, and which of them will
            // do differs by kind — an import writes locally, the rest address an
            // entry on AniDB
            var resolvedAction = new Abstractions.Metadata.Anidb.Models.MylistSyncAction
            {
                Kind = action.Kind,
                // the caller does not send one, and the one it was shown may no
                // longer describe what the step does if it changed the kind
                Description = Describe(action.Kind, video, anidbEpisode),
                Video = video,
                AnidbEpisode = anidbEpisode,
                ShokoEpisode = shokoEpisode,
                Entry = entry,
                // the wire carries no values, so they come from current state
                // here rather than from the caller. Null cannot mean "work it
                // out" further in: on a sync-built plan a null watched date is
                // an unwatch, and deriving one would invert it
                WatchedAt = action.Kind switch
                {
                    MylistSyncActionKind.ImportWatchedState => entry?.ViewedAt,
                    MylistSyncActionKind.ExportWatchedState or MylistSyncActionKind.ExportEntryAddition
                        => LocalWatchedDate(video, shokoEpisode),
                    _ => null,
                },
                State = action.Kind is MylistSyncActionKind.ExportWatchedState
                    ? settings.AniDb.MyList_StorageState
                    : null,
                DeleteType = action.Kind is MylistSyncActionKind.ExportEntryRemoval
                    ? settings.AniDb.MyList_DeleteType
                    : null,
            };

            actions.Add(resolvedAction);
        }

        if (!ModelState.IsValid)
            return false;

        resolved = new() { CreatedAt = DateTime.UtcNow, Actions = actions };
        return true;
    }

    /// <summary>
    /// A line describing a step assembled by a caller, since only the kind and
    /// the ids come over the wire.
    /// </summary>
    private static string Describe(MylistSyncActionKind kind, IVideo? video, IAnidbEpisode? episode)
    {
        var what = kind switch
        {
            MylistSyncActionKind.ImportWatchedState => "Import watched state",
            MylistSyncActionKind.ExportWatchedState => "Update the MyList entry",
            MylistSyncActionKind.ExportEntryAddition => "Add a MyList entry",
            MylistSyncActionKind.ExportEntryRemoval => "Remove the MyList entry",
            _ => "Leave the MyList entry alone",
        };
        return video is not null ? $"{what} for file {video.ID}"
            : episode is not null ? $"{what} for episode {episode.ID}"
            : what;
    }

    /// <summary>
    /// When the user watched it locally, at the precision AniDB carries. The
    /// database stores local time and AniDB works in UTC, so it converts.
    /// </summary>
    private DateTime? LocalWatchedDate(IVideo? video, IShokoEpisode? episode)
    {
        var anidbUser = users.GetAniDBUser();
        if (anidbUser is null)
            return null;

        var watchedDate = video is not null
            ? videoLocalUsers.GetByUserAndVideoLocalID(anidbUser.JMMUserID, video.ID)?.WatchedDate
            : episode is AnimeEpisode animeEpisode ? animeEpisode.GetUserRecord(anidbUser.JMMUserID)?.WatchedDate : null;
        return AniDBExtensions.TruncateToAniDBPrecision(watchedDate?.ToUniversalTime());
    }

    private Task<ActionResult<T>> NotFoundTask<T>(string message)
        => Task.FromResult<ActionResult<T>>(NotFound(message));
}
