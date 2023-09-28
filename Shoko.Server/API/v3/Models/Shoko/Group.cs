using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Group object, stores all of the group info. Groups are Shoko Internal Objects, so they are handled a bit differently
/// </summary>
public class Group : BaseModel
{
    /// <summary>
    /// IDs such as the group ID, default series, parent group, etc.
    /// </summary>
    public GroupIDs IDs { get; set; }

    /// <summary>
    /// The sort name for the group.
    /// </summary>
    public string SortName { get; set; }

    /// <summary>
    /// A short description of the group.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Indicates the group has a custom name set, different from the default
    /// name of the main series in the group.
    /// </summary>
    public bool HasCustomName { get; set; }

    /// <summary>
    /// Indicates the group has a custom description set, different from the
    /// default description of the main series in the group.
    /// </summary>
    public bool HasCustomDescription { get; set; }

    /// <summary>
    /// The default or random pictures for the default series. This allows
    /// the client to not need to get all images and pick one.
    ///
    /// There should always be a poster, but no promises on the rest.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// Sizes object, has totals
    /// </summary>
    public GroupSizes Sizes { get; set; }

    #region Constructors

    public Group(HttpContext ctx, SVR_AnimeGroup group, bool randomiseImages = false)
    {
        var subGroupCount = group.GetChildGroups().Count;
        var userID = ctx.GetUser()?.JMMUserID ?? 0;
        var allSeries = group.GetAllSeries();
        var mainSeries = allSeries.FirstOrDefault();
        var episodes = allSeries.SelectMany(a => a.GetAnimeEpisodes()).ToList();

        IDs = new GroupIDs { ID = group.AnimeGroupID };
        if (group.DefaultAnimeSeriesID != null)
        {
            IDs.DefaultSeries = group.DefaultAnimeSeriesID.Value;
        }

        if (mainSeries != null)
        {
            IDs.MainSeries = mainSeries.AnimeSeriesID;
        }

        if (group.AnimeGroupParentID.HasValue)
        {
            IDs.ParentGroup = group.AnimeGroupParentID.Value;
        }

        IDs.TopLevelGroup = group.TopLevelAnimeGroup.AnimeGroupID;

        Name = group.GroupName;
        SortName = group.SortName;
        Description = group.Description;
        Sizes = ModelHelper.GenerateGroupSizes(allSeries, episodes, subGroupCount, userID);
        Size = allSeries.Count(series => series.AnimeGroupID == group.AnimeGroupID);
        HasCustomName = group.IsManuallyNamed == 1;
        HasCustomDescription = group.OverrideDescription == 1;

        // TODO make a factory for this file. Not feeling it rn
        var factory = ctx.RequestServices.GetRequiredService<SeriesFactory>();
        Images = mainSeries == null ? new Images() : factory.GetDefaultImages(mainSeries, randomiseImages);
    }

    #endregion

    public class GroupIDs : IDs
    {
        /// <summary>
        /// The ID of the user selected default series, if it has one.
        ///
        /// The value of this field will be reflected in <see cref="MainSeries"/> if it is set.
        /// </summary>
        public int? DefaultSeries { get; set; }

        /// <summary>
        /// The ID of the main series for the group.
        /// </summary>
        /// <value></value>
        public int MainSeries { get; set; }

        /// <summary>
        /// The ID of the direct parent group, if it has one.
        /// </summary>
        public int? ParentGroup { get; set; }

        /// <summary>
        /// The ID of the top-level (ancestor) group this group belongs to.
        /// If the current group is a top-level group then it refers to
        /// itself.
        /// </summary>
        public int TopLevelGroup { get; set; }
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        public class CreateOrUpdateGroupBody
        {
            /// <summary>
            /// The <see cref="Group"/> parent ID. Omit it or set it to 0 to
            /// create a new top-level group.
            /// </summary>
            public int? ParentID { get; set; } = null;

            /// <summary>
            /// Manually select the default series for the group.
            /// </summary>
            public int? DefaultSeriesID { get; set; } = null;

            /// <summary>
            /// All the series to put into the group.
            /// </summary>
            public List<int> SeriesIDs { get; set; } = new();

            /// <summary>
            /// All groups to put into the group as sub-groups.
            /// </summary>
            /// <remarks>
            /// If the parent group is a sub-group of any of the groups in this
            /// array, then the request will be aborted.
            /// </remarks>
            public List<int> ChildIDs { get; set; } = new();

            /// <summary>
            /// The group's custom name.
            /// </summary>
            /// <remarks>
            /// The name will only be modified if either
            /// <see cref="Group.HasCustomName"/> or <see cref="HasCustomName"/>
            /// is set to true, and the value is not set to <c>null</c>.
            /// </remarks>
            public string? Name { get; set; } = null;

            /// <summary>
            /// The group's custom sort name.
            /// </summary>
            /// <remarks>
            /// The sort name will only be modified if either
            /// <see cref="Group.HasCustomName"/> or <see cref="HasCustomName"/>
            /// is set to true, and the value is not set to <c>null</c>.
            /// </remarks>
            public string? SortName { get; set; } = null;

            /// <summary>
            /// The group's custom description.
            /// </summary>
            /// <remarks>
            /// The description will only be modified if either
            /// <see cref="Group.HasCustomDescription"/> or <see cref="HasCustomDescription"/>
            /// is set to true, and the value is not set to <c>null</c>.
            /// </remarks>
            public string? Description { get; set; } = null;

            /// <summary>
            /// Indicates the group should use a custom name.
            /// </summary>
            /// <remarks>
            /// Leave it as <c>null</c> to conditionally set the value if
            /// <see cref="Name"/> or <see cref="SortName"/> is set, or
            /// explictly set it to <c>true</c> to lock in the new/current
            /// names, or set it to <c>false</c> to reset the names back to the
            /// automatic naming based on the main series.
            /// </remarks>
            public bool? HasCustomName { get; set; } = null;

            /// <summary>
            /// Indicates the group should use a custom description.
            /// </summary>
            /// <remarks>
            /// Leave it as <c>null</c> to conditionally set the value if
            /// <see cref="Description"/> is set, or explictly set it to
            /// <c>true</c> to lock in the new/current description, or set it to
            /// <c>false</c> to reset the names back to the automatic naming
            /// based on the main series.
            /// </remarks>
            public bool? HasCustomDescription { get; set; } = null;

            public CreateOrUpdateGroupBody() { }

            public CreateOrUpdateGroupBody(SVR_AnimeGroup group)
            {
                Name = group.GroupName;
                SortName = group.SortName;
                ParentID = group.AnimeGroupParentID;
                DefaultSeriesID = group.DefaultAnimeSeriesID;
                SeriesIDs = group.GetSeries().Select(series => series.AnimeSeriesID).ToList();
                ChildIDs = group.GetChildGroups().Select(group => group.AnimeGroupID).ToList();
            }

            public Group? MergeWithExisting(HttpContext ctx, SVR_AnimeGroup group, ModelStateDictionary modelState)
            {
                // Validate if the parent exists if a parent id is set.
                SVR_AnimeGroup? parent = null;
                if (ParentID.HasValue && ParentID.Value != 0)
                {
                    parent = RepoFactory.AnimeGroup.GetByID(ParentID.Value);
                    if (parent == null)
                    {
                        modelState.AddModelError(nameof(ParentID), $"Unable to get parent group with id \"{ParentID.Value}\".");
                    }
                    else
                    {
                        if (parent.IsDescendantOf(ChildIDs))
                            modelState.AddModelError(nameof(ParentID), "Infinite recursion detected between selected parent group and child groups.");
                        if (group.AnimeGroupID != 0 && parent.IsDescendantOf(group.AnimeGroupID))
                            modelState.AddModelError(nameof(ParentID), "Infinite recursion detected between selected parent group and current group.");
                    }
                }

                // Get the groups and validate the group ids.
                var childGroups = ChildIDs
                    .Select(groupID => RepoFactory.AnimeGroup.GetByID(groupID))
                    .Where(childGroup => childGroup != null)
                    .ToList();
                if (childGroups.Count != ChildIDs.Count)
                {
                    var unknownGroupIDs = ChildIDs
                        .Where(id => !childGroups.Any(childGroup => childGroup.AnimeGroupID == id))
                        .ToList();
                    modelState.AddModelError(nameof(ChildIDs), $"Unable to get child groups with ids \"{string.Join("\", \"", unknownGroupIDs)}\".");
                }

                // Get the series and validate the series ids.
                var seriesList = SeriesIDs
                    .Select(id => RepoFactory.AnimeSeries.GetByID(id))
                    .Where(s => s != null)
                    .ToList();
                if (seriesList.Count != SeriesIDs.Count)
                {
                    var unknownSeriesIDs = SeriesIDs
                        .Where(id => !seriesList.Any(series => series.AnimeSeriesID == id))
                        .ToList();
                    throw new Exception($"Unable to get series with ids \"{string.Join("\", \"", unknownSeriesIDs)}\".");
                }

                // Get a list of all the series across the new inputs and the existing group.
                var allSeriesList = seriesList
                        .Concat(childGroups.SelectMany(childGroup => childGroup.GetAllSeries()))
                        .Concat(group.GetAllSeries())
                        .DistinctBy(series => series.AnimeSeriesID)
                        .ToList();
                if (allSeriesList.Count == 0)
                {
                    modelState.AddModelError(nameof(SeriesIDs), "Unable to create an empty group without any series or child groups.");
                    modelState.AddModelError(nameof(ChildIDs), "Unable to create an empty group without any series or child groups.");
                }

                // Find the default series among the list of seris.
                SVR_AnimeSeries? defaultSeries = null;
                if (DefaultSeriesID.HasValue)
                {
                    defaultSeries = allSeriesList
                        .FirstOrDefault(series => series.AnimeSeriesID == DefaultSeriesID.Value);
                    if (defaultSeries == null)
                        modelState.AddModelError(nameof(DefaultSeriesID), $"Unable to find the default series with id \"{DefaultSeriesID.Value}\" within the newly created group.");
                }

                // Return now if we encountered any validation errors.
                if (!modelState.IsValid)
                    return null;

                // Save the group now if it's a new group, so we can get a valid
                // id to use.
                if (group.AnimeGroupID == 0)
                    RepoFactory.AnimeGroup.Save(group);

                // Check if the names have changed if we omit the value, or if
                // we set it to true.
                if (!HasCustomName.HasValue || HasCustomName.Value)
                {
                    // Lock the name if it's set to true.
                    if (HasCustomName.HasValue)
                        group.IsManuallyNamed = 1;

                    // The group name and/or the group sort name changed.
                    var overrideName = !string.IsNullOrWhiteSpace(Name) && !string.Equals(group.GroupName, Name);
                    if (overrideName)
                    {
                        group.IsManuallyNamed = 1;
                        group.GroupName = Name;
                        group.SortName = string.IsNullOrWhiteSpace(SortName) ? Name : SortName;
                    }

                    // The group sort name changed.
                    var overrideSortName = !overrideName && group.IsManuallyNamed == 1 && !string.IsNullOrEmpty(SortName) && !string.Equals(group.SortName, SortName);
                    if (overrideSortName)
                    {
                        group.IsManuallyNamed = 1;
                        group.SortName = SortName;
                    }
                }
                // Reset the name.
                else
                {
                    group.IsManuallyNamed = 0;
                }

                // Same as above, but for the description.
                if (!HasCustomDescription.HasValue || HasCustomDescription.Value)
                {
                    if (HasCustomDescription.HasValue)
                        group.OverrideDescription = 1;

                    // The description changed.
                    var overrideDescription = !string.IsNullOrWhiteSpace(Description) && !string.Equals(group.Description, Description);
                    if (overrideDescription)
                    {
                        group.OverrideDescription = 1;
                        group.Description = Description;
                    }
                }
                // Reset the description.
                else
                {
                    group.OverrideDescription = 0;
                }

                // Move the child groups under the new group.
                foreach (var childGroup in childGroups)
                {
                    // Skip adding child groups already part of the group.
                    if (childGroup.AnimeGroupParentID.HasValue && childGroup.AnimeGroupParentID.Value == group.AnimeGroupID)
                        continue;

                    childGroup.AnimeGroupParentID = group.AnimeGroupID;
                    RepoFactory.AnimeGroup.Save(group, false, false);
                }

                // Move the series over to the new group.
                foreach (var series in seriesList)
                {
                    // Skip adding series already part of the group.
                    if (series.AnimeGroupID == group.AnimeGroupID)
                        continue;

                    series.MoveSeries(group);
                }

                // Set the main series and maybe update the group
                // name/description.
                group.SetMainSeries(defaultSeries);

                // Update stats for all groups in the chain.
                group.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true);

                // Return a new representation of the group.
                return new Group(ctx, group);
            }
        }
    }
}

/// <summary>
/// Downloaded, Watched, Total, etc
/// </summary>
public class GroupSizes : SeriesSizes
{
    public GroupSizes() : base()
    {
        SubGroups = 0;
        SeriesTypes = new SeriesTypeCounts();
    }

    public GroupSizes(SeriesSizes sizes)
    {
        FileSources = sizes.FileSources;
        Local = sizes.Local;
        Watched = sizes.Watched;
        Total = sizes.Total;
        SeriesTypes = new SeriesTypeCounts();
        SubGroups = 0;
    }

    /// <summary>
    /// Count of the different series types within the group.
    /// </summary>
    [Required]
    public SeriesTypeCounts SeriesTypes { get; set; }

    /// <summary>
    /// Number of direct sub-groups within the group.
    /// </summary>
    /// <value></value>
    public int SubGroups { get; set; }

    public class SeriesTypeCounts
    {
        public int Unknown;
        public int Other;
        public int TV;
        public int TVSpecial;
        public int Web;
        public int Movie;
        public int OVA;
    }
}
