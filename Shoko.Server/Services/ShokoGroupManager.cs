using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Update;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.Services;

public class ShokoGroupManager : IShokoGroupManager
{
    private readonly ILogger<ShokoGroupManager> _logger;
    private readonly AnimeGroupService _animeGroupService;
    private readonly AnimeSeriesService _animeSeriesService;
    private readonly AnimeGroupRepository _animeGroupRepo;
    private readonly AnimeSeriesRepository _animeSeriesRepo;
    private readonly ISettingsProvider _settingsProvider;
    private readonly AnimeGroupCreator _animeGroupCreator;
    private readonly MetadataTextManager _textManager;

    public ShokoGroupManager(
        ILogger<ShokoGroupManager> logger,
        AnimeGroupService animeGroupService,
        AnimeSeriesService animeSeriesService,
        AnimeGroupRepository animeGroupRepo,
        AnimeSeriesRepository animeSeriesRepo,
        ISettingsProvider settingsProvider,
        AnimeGroupCreator animeGroupCreator,
        IMetadataTextManager textManager)
    {
        _logger = logger;
        _animeGroupService = animeGroupService;
        _animeSeriesService = animeSeriesService;
        _animeGroupRepo = animeGroupRepo;
        _animeSeriesRepo = animeSeriesRepo;
        _settingsProvider = settingsProvider;
        _animeGroupCreator = animeGroupCreator;
        _textManager = (MetadataTextManager)textManager;

        ShokoEventHandler.Instance.GroupUpdated += (_, e) =>
        {
            var evt = e.Reason switch
            {
                UpdateReason.Added => GroupAdded,
                UpdateReason.Removed => GroupRemoved,
                _ => GroupUpdated,
            };
            evt?.Invoke(this, e);
        };
        ShokoEventHandler.Instance.SeriesMoved += (_, e) => SeriesMoved?.Invoke(this, e);
        ShokoEventHandler.Instance.GroupsRecreated += (_, e) => GroupsRecreated?.Invoke(this, e);
    }

    #region Events

    public event EventHandler<GroupInfoUpdatedEventArgs>? GroupAdded;
    public event EventHandler<GroupInfoUpdatedEventArgs>? GroupUpdated;
    public event EventHandler<GroupInfoUpdatedEventArgs>? GroupRemoved;
    public event EventHandler<SeriesMovedEventArgs>? SeriesMoved;
    public event EventHandler? GroupsRecreated;

    #endregion

    #region CRUD

    public IEnumerable<IShokoGroup> GetAllGroups()
        => _animeGroupRepo.GetAll();

    public IShokoGroup? GetGroupByID(int groupID)
        => _animeGroupRepo.GetByID(groupID);

    public IShokoGroup CreateGroup(GroupData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var now = DateTime.Now;
        var group = new AnimeGroup { DateTimeCreated = now, DateTimeUpdated = now };
        var updateData = new GroupUpdateData
        {
            Groups = data.Groups,
            Series = data.Series,
            MainSeries = data.MainSeries,
            Name = data.Name,
            Overview = data.Overview,
        };
        if (data.ParentGroup is not null)
            updateData.ParentGroup = data.ParentGroup;

        UpdateGroupInternal(group, updateData, isNew: true);

        return group;
    }

    public void SetMainSeries(IShokoGroup group, IShokoSeries? series)
        => UpdateGroupInternal(AsAnimeGroup(group, nameof(group)), new() { MainSeries = series });

    public void MoveSeries(IShokoSeries series, IShokoGroup targetGroup)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series is not AnimeSeries)
            throw new ArgumentException("Series must be an AnimeSeries instance", nameof(series));

        UpdateGroupInternal(AsAnimeGroup(targetGroup, nameof(targetGroup)), new() { Series = [series] });
    }

    public IShokoGroup UpdateGroup(IShokoGroup group, GroupUpdateData updateData)
        => UpdateGroupInternal(AsAnimeGroup(group, nameof(group)), updateData);

    public async Task DeleteGroup(IShokoGroup group, bool deleteSeries = false, bool deleteFiles = false)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group is not AnimeGroup animeGroup)
            throw new ArgumentException("Group must be an AnimeGroup instance", nameof(group));

        // If the group has any series, delete or move them. Moving the last
        // one out removes the emptied groups already.
        if (animeGroup.AllSeries is { Count: > 0 } seriesList)
        {
            if (deleteSeries)
            {
                foreach (var series in seriesList)
                    await _animeSeriesService.DeleteSeries(series, deleteFiles, false);
            }
            else
            {
                foreach (var series in seriesList)
                    CreateGroup(new() { Series = [series] });
            }
        }

        // A group has no name without a series, so none is left empty.
        RemoveEmptyGroups(animeGroup);
    }

    #region CRUD | Internals

    private static AnimeGroup AsAnimeGroup(IShokoGroup group, string paramName)
    {
        ArgumentNullException.ThrowIfNull(group, paramName);
        return group as AnimeGroup ?? throw new ArgumentException("Group must be an AnimeGroup instance", paramName);
    }

    private IShokoGroup UpdateGroupInternal(AnimeGroup group, GroupUpdateData updateData, bool? isNew = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(updateData);
        isNew ??= group.AnimeGroupID is 0;

        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (updateData.HasParentGroup && updateData.ParentGroup is { } pg && (pg.LocalID == group.AnimeGroupID || IsDescendant(pg, group.AnimeGroupID)))
            errors["ParentGroup"] = ["Infinite recursion detected between the selected parent group and the current group."];

        if (updateData.HasParentGroup && updateData.ParentGroup is { } pg2 && updateData.Groups is { Count: > 0 } childGroupsForCheck)
        {
            foreach (var childGroup in childGroupsForCheck)
            {
                if (childGroup.LocalID == pg2.LocalID || IsDescendant(pg2, childGroup.LocalID))
                {
                    errors["ParentGroup"] = ["Infinite recursion detected between the selected parent group and the child groups."];
                    break;
                }
            }
        }

        if (updateData.HasMainSeries && updateData.MainSeries is { } && updateData.MainSeries is not AnimeSeries)
            errors["PreferredSeries"] = ["The preferred series must be an AnimeSeries instance."];

        if (updateData.HasParentGroup && updateData.ParentGroup is { } && updateData.ParentGroup is not AnimeGroup)
            errors["ParentGroup"] = ["The parent group must be an AnimeGroup instance."];

        if (updateData.Series.Any(series => series is not AnimeSeries))
            errors["Series"] = ["Every series must be an AnimeSeries instance."];

        if (updateData.Groups.Any(childGroup => childGroup is not AnimeGroup))
            errors["Groups"] = ["Every child group must be an AnimeGroup instance."];

        var allSeries = group.AllSeries
            .Concat(updateData.Series.OfType<AnimeSeries>())
            .Concat(updateData.Groups.OfType<AnimeGroup>().SelectMany(g => g.AllSeries))
            .DistinctBy(s => s.AnimeSeriesID)
            .ToHashSet();
        if (allSeries.Count == 0)
        {
            errors["Series"] = ["At least one series or child group with series is required."];
            errors["Groups"] = ["At least one series or child group with series is required."];
        }

        var mainSeriesID = updateData.HasMainSeries ? updateData.MainSeries?.LocalID : group.DefaultAnimeSeriesID;
        if (mainSeriesID.HasValue && !allSeries.Any(s => s.AnimeSeriesID == mainSeriesID.Value))
            errors.TryAdd("PreferredSeries", ["The preferred series must exist within the group."]);

        if (errors.Count > 0)
            throw new GenericValidationException("One or more validation errors occurred.", errors);

        if (group.AnimeGroupID is 0)
            _animeGroupRepo.Save(group, false);

        var existingGroups = new HashSet<int>(group.Children.Select(c => c.AnimeGroupID));
        var existingSeries = new HashSet<int>(group.Series.Select(s => s.AnimeSeriesID));
        var oldSeriesDict = updateData.Series.ToDictionary(s => s.LocalID, s => s.ParentGroupID);

        var updated = false;
        if (updateData.Groups is { Count: > 0 } childGroups0)
        {
            var existingChildren = new HashSet<int>(group.AllChildren.Select(c => c.AnimeGroupID));
            foreach (var childGroup in childGroups0.ExceptBy(existingChildren, c => c.LocalID))
            {
                var child = (AnimeGroup)childGroup;
                child.AnimeGroupParentID = group.AnimeGroupID;
                child.DateTimeUpdated = DateTime.Now;
                _animeGroupRepo.Save(child, false);
                updated = true;
            }
        }

        if (updateData.Series is { Count: > 0 })
        {
            foreach (var series in updateData.Series.ExceptBy(existingSeries, s => s.LocalID))
            {
                MoveSeries((AnimeSeries)series, group, updateGroupStats: false);
                updated = true;
            }
        }

        if (updateData.HasMainSeries)
        {
            if (updateData.MainSeries is { } ps)
            {
                if (group.DefaultAnimeSeriesID != ps.LocalID)
                {
                    group.DefaultAnimeSeriesID = ps.LocalID;
                    updated = true;
                }
            }
            else if (group.DefaultAnimeSeriesID.HasValue)
            {
                group.DefaultAnimeSeriesID = null;
                updated = true;
            }
        }

        // A name or overview given is kept as the group's user text, and one
        // set to null is removed, so the main series' is read again.
        var groupID = ((IMetadata)group).ID;
        if (updateData.HasName)
        {
            _textManager.SetCustomTitle(groupID, updateData.Name);
            updated = true;
        }

        if (updateData.HasOverview)
        {
            _textManager.SetCustomOverview(groupID, updateData.Overview);
            updated = true;
        }

        if (updateData.HasParentGroup)
        {
            group.AnimeGroupParentID = (updateData.ParentGroup as AnimeGroup)?.AnimeGroupID;
            updated = true;
        }

        if (updated || isNew.Value)
        {
            group.DateTimeUpdated = DateTime.Now;
            _animeGroupRepo.Save(group, false);
            _animeGroupService.UpdateStatsFromTopLevel(group.TopLevelAnimeGroup, true, true);

            ShokoEventHandler.Instance.OnGroupUpdated(group, isNew.Value ? UpdateReason.Added : UpdateReason.Updated);

            if (updateData.Groups is { Count: > 0 } childGroups1)
            {
                foreach (var childGroup in childGroups1.ExceptBy(existingGroups, c => c.LocalID))
                {
                    ShokoEventHandler.Instance.OnGroupUpdated(childGroup, UpdateReason.Updated);
                }
            }

            if (updateData.Series is { Count: > 0 } seriesList1)
            {
                foreach (var series in seriesList1.ExceptBy(existingSeries, s => s.LocalID))
                {
                    var oldGroupID = oldSeriesDict[series.LocalID];
                    ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
                    ShokoEventHandler.Instance.OnSeriesMoved(series, oldGroupID, group.AnimeGroupID);
                }
            }
        }

        return group;
    }

    /// <summary>
    ///   Removes a group left without series, with the groups below it, and
    ///   then every group above it left without series too, since a group
    ///   takes its name from its series.
    /// </summary>
    /// <param name="group">The group to start from.</param>
    private void RemoveEmptyGroups(AnimeGroup group)
    {
        var current = group;
        while (current is not null && current.AllSeries.Count == 0)
        {
            var parent = current.Parent;
            RemoveGroupTree(current);
            current = parent;
        }

        if (current is not null)
            _animeGroupService.UpdateStatsFromTopLevel(current.TopLevelAnimeGroup, true, true);
    }

    /// <summary>
    ///   Removes a group and every group below it, the lowest first.
    /// </summary>
    /// <param name="group">The group, holding no series.</param>
    private void RemoveGroupTree(AnimeGroup group)
    {
        foreach (var child in group.Children.ToList())
            RemoveGroupTree(child);

        _animeGroupRepo.Delete(group);
        ShokoEventHandler.Instance.OnGroupUpdated(group, UpdateReason.Removed);
    }

    private void MoveSeries(AnimeSeries series, AnimeGroup newGroup, bool updateGroupStats)
    {
        if (series.AnimeGroupID == newGroup.AnimeGroupID)
            return;

        var oldGroupID = series.AnimeGroupID;
        series.AnimeGroupID = newGroup.AnimeGroupID;
        series.DateTimeUpdated = DateTime.Now;
        _animeSeriesService.UpdateStats(series, true, true);
        if (updateGroupStats)
            _animeGroupService.UpdateStatsFromTopLevel(newGroup.TopLevelAnimeGroup, true, true);

        var oldGroup = _animeGroupRepo.GetByID(oldGroupID);
        if (oldGroup is not null)
        {
            if (oldGroup.AllSeries.Count == 0)
            {
                RemoveEmptyGroups(oldGroup);
            }
            else
            {
                var updatedOldGroup = false;
                if (oldGroup.DefaultAnimeSeriesID.HasValue && oldGroup.DefaultAnimeSeriesID.Value == series.AnimeSeriesID)
                {
                    oldGroup.DefaultAnimeSeriesID = null;
                    updatedOldGroup = true;
                }

                if (oldGroup.MainAniDBAnimeID.HasValue && oldGroup.MainAniDBAnimeID.Value == series.AniDB_ID)
                {
                    oldGroup.MainAniDBAnimeID = null;
                    updatedOldGroup = true;
                }

                if (updatedOldGroup)
                    _animeGroupRepo.Save(oldGroup);
            }

            var topGroup = oldGroup.TopLevelAnimeGroup;
            if (topGroup.AnimeGroupID != oldGroup.AnimeGroupID)
                _animeGroupService.UpdateStatsFromTopLevel(topGroup, true, true);
        }
    }

    private static bool IsDescendant(IShokoGroup group, int ancestorGroupID)
    {
        var parent = group.ParentGroup;
        while (parent != null)
        {
            if (parent.LocalID == ancestorGroupID)
                return true;
            parent = parent.ParentGroup;
        }
        return false;
    }

    #endregion

    #endregion

    #region Auto-Grouping

    public bool IsAutoGroupingEnabled
    {
        get => _settingsProvider.GetSettings().AutoGroupSeries;
        set
        {
            var s = _settingsProvider.GetSettings();
            s.AutoGroupSeries = value;
            _settingsProvider.SaveSettings(s);
        }
    }

    public bool UseAutoGroupingRelationWeighting
    {
        get => _settingsProvider.GetSettings().AutoGroupSeriesUseScoreAlgorithm;
        set
        {
            var s = _settingsProvider.GetSettings();
            s.AutoGroupSeriesUseScoreAlgorithm = value;
            _settingsProvider.SaveSettings(s);
        }
    }

    public IReadOnlySet<RelationType> AutoGroupingRelationExclusions
    {
        get
        {
            var raw = _settingsProvider.GetSettings().AutoGroupSeriesRelationExclusions;
            if (raw is null || raw.Count == 0)
                return new HashSet<RelationType>();
            return raw
                .Select(r => Enum.TryParse<RelationType>(r.Replace(" ", string.Empty), true, out var t) ? t : (RelationType?)null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToHashSet();
        }
        set
        {
            var s = _settingsProvider.GetSettings();
            var newExclusions = value
                .Select(RelationTypeToSettingsString)
                .ToList();
            foreach (var entry in s.AutoGroupSeriesRelationExclusions)
            {
                if (!Enum.TryParse<RelationType>(entry.Replace(" ", string.Empty), true, out _))
                    newExclusions.Add(entry);
            }
            s.AutoGroupSeriesRelationExclusions = newExclusions;
            _settingsProvider.SaveSettings(s);
        }
    }

    public bool AllowDissimilarTitleExclusion
    {
        get => _settingsProvider.GetSettings().AutoGroupSeriesRelationExclusions
            .Contains("AllowDissimilarTitleExclusion", StringComparer.OrdinalIgnoreCase);
        set
        {
            var s = _settingsProvider.GetSettings();
            if (value)
            {
                if (!s.AutoGroupSeriesRelationExclusions.Contains("AllowDissimilarTitleExclusion", StringComparer.OrdinalIgnoreCase))
                    s.AutoGroupSeriesRelationExclusions.Add("AllowDissimilarTitleExclusion");
            }
            else
            {
                s.AutoGroupSeriesRelationExclusions.RemoveAll(r => r.Equals("AllowDissimilarTitleExclusion", StringComparison.OrdinalIgnoreCase));
            }
            _settingsProvider.SaveSettings(s);
        }
    }

    public Task RecreateAllGroups(IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
        => _animeGroupCreator.RecreateAllGroups(progress, cancellationToken);

    private static string RelationTypeToSettingsString(RelationType type)
    {
        var name = type.ToString();
        var result = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                result.Append(' ');
            result.Append(char.ToLowerInvariant(name[i]));
        }
        return result.ToString();
    }

    #endregion

    #region Management

    public void RenameAllGroups()
    {
        // A group without a name of its own reads its main series', so the
        // series' titles and overviews are worked out again.
        _logger.LogInformation("Starting RenameAllGroups");
        TextAccess.Manager.ForgetAll();
        _logger.LogInformation("Finished RenameAllGroups");
    }

    #endregion
}
