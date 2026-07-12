using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Action.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
///   Job that executes a registered plugin action by its identifier,
///   optionally scoped to a specific user and/or entity (series, group,
///   episode).
///   Enqueued by <see cref="IActionService.ScheduleExecuteOfGlobalAction"/>
///   and its overloads.
/// </summary>
[DatabaseRequired]
[DisallowConcurrentExecution]
internal class ExecuteActionJob(
    IActionService actionService,
    IUserService userService,
    IMetadataService metadataService
) : BaseJob
{
    /// <summary>
    ///   The stable identifier of the action to execute.
    /// </summary>
    public Guid ActionID { get; set; }

    /// <summary>
    ///   The user ID to scope the action to, if applicable.
    /// </summary>
    public int? UserID { get; set; }

    /// <summary>
    ///   The group ID to scope the action to, if applicable.
    /// </summary>
    public int? GroupID { get; set; }

    /// <summary>
    ///   The AniDB anime ID of the series to scope the action to, if applicable.
    /// </summary>
    public int? AnimeID { get; set; }

    /// <summary>
    ///   The episode ID to scope the action to, if applicable.
    /// </summary>
    public int? EpisodeID { get; set; }

    private string? _actionName;

    private string? _userName;

    private string? _entityName;

    public override string TypeName => "Execute Action";

    public override string Title => "Executing Action";

    public override void PostInit()
    {
        var actionInfo = actionService.GetActionById(ActionID);
        _actionName = actionInfo?.Name;

        if (UserID.HasValue)
        {
            var user = userService.GetUserByID(UserID.Value);
            _userName = user?.Username;
        }

        if (GroupID.HasValue)
        {
            var group = metadataService.GetShokoGroupByID(GroupID.Value);
            _entityName = group?.Title;
        }
        else if (AnimeID.HasValue)
        {
            var series = metadataService.GetShokoSeriesByAnidbID(AnimeID.Value);
            _entityName = series?.Title;
        }
        else if (EpisodeID.HasValue)
        {
            var episode = metadataService.GetShokoEpisodeByID(EpisodeID.Value);
            _entityName = episode?.Title;
        }
    }

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (_actionName is not null) details.Add("Action", _actionName);
            else details.Add("Action ID", ActionID);
            details.Add("Scope", ScopeToName());
            if (UserID.HasValue)
                if (_userName is not null) details.Add("User", _userName);
                else details.Add("User ID", UserID);
            if (GroupID.HasValue)
                if (_entityName is not null && !AnimeID.HasValue) details.Add("Group", _entityName);
                else if (_entityName is not null) details.Add("Group", _entityName);
                else details.Add("Group ID", GroupID);
            else if (AnimeID.HasValue)
                if (_entityName is not null) details.Add("Series", _entityName);
                else details.Add("Anime ID", AnimeID);
            else if (EpisodeID.HasValue)
                if (_entityName is not null && !AnimeID.HasValue && !GroupID.HasValue) details.Add("Episode", _entityName);
                else if (_entityName is not null) details.Add("Episode", _entityName);
                else details.Add("Episode ID", EpisodeID);
            return details;
        }
    }

    public override async Task Execute()
    {
        var action = actionService.GetActionById(ActionID);
        if (action is null)
        {
            _logger.LogWarning("ExecuteActionJob: Action not found: {ActionID}", ActionID);
            return;
        }

        _logger.LogInformation("Executing action \"{Action}\"", _actionName ?? ActionID.ToString());

        var user = UserID.HasValue ? userService.GetUserByID(UserID.Value) : null;

        if (AnimeID.HasValue)
        {
            var series = metadataService.GetShokoSeriesByAnidbID(AnimeID.Value);
            if (series is not null && user is not null)
                await actionService.ExecuteSeriesUserAction(action, series, user);
            else if (series is not null)
                await actionService.ExecuteSeriesAction(action, series);
        }
        else if (GroupID.HasValue)
        {
            var group = metadataService.GetShokoGroupByID(GroupID.Value);
            if (group is not null && user is not null)
                await actionService.ExecuteGroupUserAction(action, group, user);
            else if (group is not null)
                await actionService.ExecuteGroupAction(action, group);
        }
        else if (EpisodeID.HasValue)
        {
            var episode = metadataService.GetShokoEpisodeByID(EpisodeID.Value);
            if (episode is not null && user is not null)
                await actionService.ExecuteEpisodeUserAction(action, episode, user);
            else if (episode is not null)
                await actionService.ExecuteEpisodeAction(action, episode);
        }
        else if (user is not null)
        {
            await actionService.ExecuteGlobalUserAction(action, user);
        }
        else
        {
            await actionService.ExecuteGlobalAction(action);
        }

        _logger.LogInformation("Finished executing action \"{Action}\"", _actionName ?? ActionID.ToString());
    }

    private string ScopeToName() => (UserID.HasValue, GroupID.HasValue ? 1 : AnimeID.HasValue ? 2 : EpisodeID.HasValue ? 3 : 0) switch
    {
        (false, 0) => "User",
        (true, 0) => "Global User",
        (false, 1) => "Group",
        (true, 1) => "Group User",
        (false, 2) => "Series",
        (true, 2) => "Series User",
        (false, 3) => "Episode",
        (true, 3) => "Episode User",
        _ => "Unknown",
    };
}
