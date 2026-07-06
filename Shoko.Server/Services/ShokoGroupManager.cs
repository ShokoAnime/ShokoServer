using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Update;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
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

    public ShokoGroupManager(
        ILogger<ShokoGroupManager> logger,
        AnimeGroupService animeGroupService,
        AnimeSeriesService animeSeriesService,
        AnimeGroupRepository animeGroupRepo,
        AnimeSeriesRepository animeSeriesRepo,
        ISettingsProvider settingsProvider,
        AnimeGroupCreator animeGroupCreator)
    {
        _logger = logger;
        _animeGroupService = animeGroupService;
        _animeSeriesService = animeSeriesService;
        _animeGroupRepo = animeGroupRepo;
        _animeSeriesRepo = animeSeriesRepo;
        _settingsProvider = settingsProvider;
        _animeGroupCreator = animeGroupCreator;

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
        UpdateGroupInternal(group, new GroupUpdateData
        {
            Groups = data.Groups,
            Series = data.Series,
            MainSeries = data.MainSeries,
            Name = data.Name,
            Description = data.Description,
        }, isNew: true);

        return group;
    }

    public void SetMainSeries(IShokoGroup group, IShokoSeries? series)
        => UpdateGroupInternal((AnimeGroup)group, new() { MainSeries = series });

    public void MoveSeries(IShokoSeries series, IShokoGroup targetGroup)
        => UpdateGroup(targetGroup, new() { Series = [series] });

    public IShokoGroup UpdateGroup(IShokoGroup group, GroupUpdateData updateData)
        => UpdateGroupInternal((AnimeGroup)group, updateData);

    public async Task DeleteGroup(IShokoGroup group, bool deleteSeries = false, bool deleteFiles = false)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group is not AnimeGroup animeGroup)
            throw new ArgumentException("Group must be an AnimeGroup instance", nameof(group));

        // If the group has any series, delete or move them.
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
        // We'll recurse into this function to delete the group after the last
        // series has been deleted from a group.
        else
        {
            _animeGroupRepo.Delete(animeGroup);
            ShokoEventHandler.Instance.OnGroupUpdated(group, UpdateReason.Removed);

            _animeGroupService.UpdateStatsFromTopLevel(animeGroup.Parent?.TopLevelAnimeGroup, true, true);
        }

    }

    #region CRUD | Internals

    private IShokoGroup UpdateGroupInternal(AnimeGroup group, GroupUpdateData updateData, bool? isNew = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(updateData);
        isNew ??= group.AnimeGroupID is 0;

        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (updateData.HasParentGroup && updateData.ParentGroup is { } pg && (pg.ID == group.AnimeGroupID || IsDescendant(pg, group.AnimeGroupID)))
            errors["ParentGroup"] = ["Infinite recursion detected between the selected parent group and the current group."];

        if (updateData.HasParentGroup && updateData.ParentGroup is { } pg2 && updateData.Groups is { Count: > 0 } childGroupsForCheck)
        {
            foreach (var childGroup in childGroupsForCheck)
            {
                if (childGroup.ID == pg2.ID || IsDescendant(pg2, childGroup.ID))
                {
                    errors["ParentGroup"] = ["Infinite recursion detected between the selected parent group and the child groups."];
                    break;
                }
            }
        }

        if (updateData.HasMainSeries && updateData.MainSeries is { } && updateData.MainSeries is not AnimeSeries)
            errors["PreferredSeries"] = ["The preferred series must be an AnimeSeries instance."];

        var allSeries = group.AllSeries
            .Concat(updateData.Series)
            .Concat(updateData.Groups.SelectMany(g => g.AllSeries))
            .DistinctBy(s => s.ID)
            .ToHashSet();
        if (allSeries.Count == 0)
        {
            errors["Series"] = ["At least one series or child group with series is required."];
            errors["Groups"] = ["At least one series or child group with series is required."];
        }

        if (group.DefaultAnimeSeriesID.HasValue && !allSeries.Any(s => s.ID == group.DefaultAnimeSeriesID.Value))
            throw new GenericValidationException("The preferred series was not found within the group.", new Dictionary<string, IReadOnlyList<string>>
            {
                ["PreferredSeries"] = ["The preferred series must exist within the group."],
            });

        if (errors.Count > 0)
            throw new GenericValidationException("One or more validation errors occurred.", errors);

        var existingGroups = new HashSet<int>(group.Children.Select(c => c.AnimeGroupID));
        var existingSeries = new HashSet<int>(group.Series.Select(s => s.AnimeSeriesID));
        var oldSeriesDict = updateData.Series.ToDictionary(s => s.ID, s => s.ParentGroupID);

        var updated = false;
        if (updateData.Groups is { Count: > 0 } childGroups0)
        {
            var existingChildren = new HashSet<int>(group.AllChildren.Select(c => c.AnimeGroupID));
            foreach (var childGroup in childGroups0.ExceptBy(existingChildren, c => c.ID))
            {
                if (childGroup is not AnimeGroup child)
                    continue;

                child.AnimeGroupParentID = group.AnimeGroupID;
                child.DateTimeUpdated = DateTime.Now;
                _animeGroupRepo.Save(child, false);
                updated = true;
            }
        }

        if (updateData.Series is { Count: > 0 })
        {
            foreach (var series in updateData.Series.ExceptBy(existingSeries, s => s.ID))
            {
                if (series is not AnimeSeries s)
                    continue;

                MoveSeries(s, group, updateGroupStats: false);
                updated = true;
            }
        }

        var needsAutoName = isNew.Value;
        var needsAutoDescription = isNew.Value;
        if (updateData.HasMainSeries)
        {
            if (updateData.MainSeries is { } ps)
            {
                if (group.DefaultAnimeSeriesID != ps.ID)
                {
                    group.DefaultAnimeSeriesID = ps.ID;
                    updated = true;
                    needsAutoName = group.IsManuallyNamed == 0;
                    needsAutoDescription = group.OverrideDescription == 0;
                }
            }
            else if (group.DefaultAnimeSeriesID.HasValue)
            {
                group.DefaultAnimeSeriesID = null;
                needsAutoName = true;
                needsAutoDescription = true;
                updated = true;
            }
        }

        // HasName is true whenever Name was explicitly set (including to null).
        if (updateData.HasName)
        {
            // The group name was explicitly provided — mark as custom.
            if (updateData.Name is { } name)
            {
                group.IsManuallyNamed = 1;
                needsAutoName = false;
                if (!string.Equals(group.GroupName, name))
                    group.GroupName = name;
            }
            // Name was explicitly set to null — reset to automatic naming.
            else
            {
                group.IsManuallyNamed = 0;
                needsAutoName = true;
            }
            updated = true;
        }

        // Same as above, but for the description.
        if (updateData.HasDescription)
        {
            // The description was explicitly provided — mark as custom.
            if (updateData.Description is { } description)
            {
                group.OverrideDescription = 1;
                needsAutoDescription = false;
                if (!string.Equals(group.Description, description))
                    group.Description = description;
            }
            // Description was explicitly set to null — reset to automatic.
            else
            {
                group.OverrideDescription = 0;
                needsAutoDescription = true;
            }
            updated = true;
        }

        // Set auto. name/description.
        if (needsAutoName && group.IsManuallyNamed == 0)
        {
            var main = updateData.MainSeries ?? group.MainSeries;
            group.GroupName = (main as AnimeSeries)?.Title ?? group.GroupName;
        }
        if (needsAutoDescription && group.OverrideDescription == 0)
        {
            var main = updateData.MainSeries ?? group.MainSeries;
            group.Description = (main as AnimeSeries)?.PreferredOverview?.Value ?? group.Description;
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
                foreach (var childGroup in childGroups1.ExceptBy(existingGroups, c => c.ID))
                {
                    ShokoEventHandler.Instance.OnGroupUpdated(childGroup, UpdateReason.Updated);
                }
            }

            if (updateData.Series is { Count: > 0 } seriesList1)
            {
                foreach (var series in seriesList1.ExceptBy(existingSeries, s => s.ID))
                {
                    var oldGroupID = oldSeriesDict[series.ID];
                    ShokoEventHandler.Instance.OnSeriesUpdated(series, UpdateReason.Updated);
                    ShokoEventHandler.Instance.OnSeriesMoved(series, oldGroupID, group.AnimeGroupID);
                }
            }
        }

        return group;
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
                _animeGroupRepo.Delete(oldGroup);
                ShokoEventHandler.Instance.OnGroupUpdated(oldGroup, UpdateReason.Removed);
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
            if (parent.ID == ancestorGroupID)
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

    public Task RecreateAllGroups() => _animeGroupCreator.RecreateAllGroups();

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
        _logger.LogInformation("Starting RenameAllGroups");
        foreach (var grp in _animeGroupRepo.GetAll())
        {
            if (grp.IsManuallyNamed == 1 && grp.OverrideDescription == 1)
                continue;

            var series = grp.MainSeries;
            if (series != null)
            {
                if (grp.IsManuallyNamed == 0)
                    grp.GroupName = series.Title;
                if (grp.OverrideDescription == 0)
                    grp.Description = series.PreferredOverview?.Value ?? string.Empty;

                grp.DateTimeUpdated = DateTime.Now;
                _animeGroupRepo.Save(grp, false);
            }
        }
        _logger.LogInformation("Finished RenameAllGroups");
    }

    #endregion
}
