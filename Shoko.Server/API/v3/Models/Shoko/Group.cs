using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Core.Update;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

#pragma warning disable CS0618
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Group object, stores all of the group info. Groups are Shoko Internal Objects, so they are handled a bit differently
/// </summary>
public class Group : BaseModel
{
    /// <summary>
    /// IDs such as the group ID, default series, parent group, etc.
    /// </summary>
    [Required]
    public GroupIDs IDs { get; set; }

    /// <summary>
    /// The sort name for the group. Cannot directly be set by the user.
    /// </summary>
    [Required]
    public string SortName { get; set; }

    /// <summary>
    /// A short description of the group.
    /// </summary>
    [Required]
    public string Description { get; set; }

    /// <summary>
    /// Indicates the group has a custom name set, different from the default
    /// name of the main series in the group.
    /// </summary>
    [Required]
    public bool HasCustomName { get; set; }

    /// <summary>
    /// Indicates the group has a custom description set, different from the
    /// default description of the main series in the group.
    /// </summary>
    [Required]
    public bool HasCustomDescription { get; set; }

    /// <summary>
    /// The default or random pictures for the default series. This allows
    /// the client to not need to get all images and pick one.
    ///
    /// There should always be a poster, but no promises on the rest.
    /// </summary>
    [Required]
    public Images Images { get; set; }

    /// <summary>
    /// Sizes object, has totals
    /// </summary>
    [Required]
    public GroupSizes Sizes { get; set; }

    /// <summary>
    /// Total series count within the group and across all sub-groups, not
    /// affected by the current filtering.
    /// </summary>
    [Required]
    public int TotalSize { get; set; }

    /// <summary>
    /// The time when the group was created.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    [Required]
    public DateTime Created { get; set; }

    /// <summary>
    /// The time when the group was last updated
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    [Required]
    public DateTime Updated { get; set; }

    #region Constructors

    public Group(AnimeGroup group, int userID = 0, bool randomizeImages = false, IReadOnlyList<IReadOnlyList<int>>? groupIDChains = null, IReadOnlySet<int>? seriesIDs = null)
    {
        var allSeries = group.AllSeries;
        var subGroupCount = groupIDChains is null ? group.Children.Count : group.Children.Count(a => groupIDChains.Any(b => b.Contains(a.AnimeGroupID)));
        var filteredSeries = seriesIDs is null ? allSeries : allSeries.Where(a => seriesIDs.Contains(a.AnimeSeriesID)).ToList();
        var mainSeries = group.MainSeries;
        var episodes = filteredSeries.SelectMany(a => a.AllAnimeEpisodes).ToList();
        IDs = new GroupIDs { ID = group.AnimeGroupID };
        if (group.DefaultAnimeSeriesID != null)
            IDs.PreferredSeries = group.DefaultAnimeSeriesID.Value;
        if (mainSeries != null)
        {
            IDs.MainSeries = mainSeries.AnimeSeriesID;
            IDs.MainAnime = mainSeries.AniDB_ID;
        }
        if (group.AnimeGroupParentID.HasValue)
            IDs.ParentGroup = group.AnimeGroupParentID.Value;
        IDs.TopLevelGroup = group.TopLevelAnimeGroup.AnimeGroupID;
        Name = group.GroupName;
        SortName = group.SortName;
        Description = group.Description;
        Sizes = ModelHelper.GenerateGroupSizes(filteredSeries, episodes, subGroupCount, userID);
        Size = filteredSeries.Count(series => series.AnimeGroupID == group.AnimeGroupID);
        TotalSize = allSeries.Count;
        Created = group.DateTimeCreated.ToUniversalTime();
        Updated = group.DateTimeUpdated.ToUniversalTime();
        HasCustomName = group.IsManuallyNamed == 1;
        HasCustomDescription = group.OverrideDescription == 1;
        Images = ((IWithImages)group).GetBestImages().ToDto(
            preferredImages: true,
            randomizeImages: randomizeImages
        );
    }

    #endregion

    public class GroupIDs : IDs
    {
        /// <summary>
        /// The ID of the user selected preferred series, if one is set.
        ///
        /// The value of this field will be reflected in <see cref="MainSeries"/> if it is set.
        /// </summary>
        public int? PreferredSeries { get; set; }

        /// <summary>
        /// The ID of the main Shoko series for the group.
        /// </summary>
        /// <value></value>
        [Required]
        public int MainSeries { get; set; }

        /// <summary>
        /// The ID of the main AniDB anime for the group.
        /// </summary>
        [Required]
        public int MainAnime { get; set; }

        /// <summary>
        /// The ID of the direct parent group, if it has one.
        /// </summary>
        public int? ParentGroup { get; set; }

        /// <summary>
        /// The ID of the top-level (ancestor) group this group belongs to.
        /// If the current group is a top-level group then it refers to
        /// itself.
        /// </summary>
        [Required]
        public int TopLevelGroup { get; set; }
    }

    #region User Data

    /// <summary>
    ///   The user data for the group.
    /// </summary>
    public class GroupUserData
    {
        /// <summary>
        ///   The unique tags assigned to the group by the user.
        /// </summary>
        [Required]
        public IReadOnlyList<string> UserTags { get; set; }

        /// <summary>
        /// When the entry was last updated.
        /// </summary>
        [Newtonsoft.Json.JsonConverter(typeof(IsoDateTimeConverter))]
        [Required]
        public DateTime LastUpdatedAt { get; set; }

        public GroupUserData()
        {
            UserTags = [];
            LastUpdatedAt = DateTime.UtcNow;
        }

        public GroupUserData(IGroupUserData userData)
        {
            UserTags = userData.UserTags;
            LastUpdatedAt = userData.LastUpdatedAt;
        }

        public GroupUserData MergeWithExisting(JMMUser user, AnimeGroup group)
        {
            var userDataService = ISystemService.StaticServices.GetRequiredService<IUserDataService>();
            var userData = userDataService.SaveGroupUserData(group, user, new()
            {
                UserTags = UserTags,
            }).GetAwaiter().GetResult();
            return new(userData);
        }
    }

    #endregion

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        public class CreateOrUpdateGroupBody
        {
            /// <summary>
            /// The <see cref="Group"/> parent ID.
            /// </summary>
            /// <remarks>
            /// Omit it or set it to 0 to create a new top-level group when
            /// creating a new group, and omit it to keep the current parent or
            /// set it to 0 to move the group under a new parent group when modifying a series.
            /// </remarks>
            public int? ParentGroupID { get; set; }

            /// <summary>
            /// Manually select the preferred series for the group. Set to 0 to
            /// remove the preferred series.
            /// </summary>
            public int? PreferredSeriesID { get; set; }

            /// <summary>
            /// All the series to put into the group.
            /// </summary>
            public List<int>? SeriesIDs { get; set; }

            /// <summary>
            /// All groups to put into the group as sub-groups.
            /// </summary>
            /// <remarks>
            /// If the parent group is a sub-group of any of the groups in this
            /// array, then the request will be aborted.
            /// </remarks>
            public List<int>? GroupIDs { get; set; }

            /// <summary>
            /// The group's custom name.
            /// </summary>
            /// <remarks>
            /// The name will only be modified if either
            /// <see cref="Group.HasCustomName"/> or <see cref="HasCustomName"/>
            /// is set to true, and the value is not set to <c>null</c>.
            /// </remarks>
            public string? Name { get; set; }

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
            /// <see cref="Name"/> is set, or
            /// explicitly set it to <c>true</c> to lock in the new/current
            /// names, or set it to <c>false</c> to reset the names back to the
            /// automatic naming based on the main series.
            /// </remarks>
            public bool? HasCustomName { get; set; } = null;

            /// <summary>
            /// Indicates the group should use a custom description.
            /// </summary>
            /// <remarks>
            /// Leave it as <c>null</c> to conditionally set the value if
            /// <see cref="Description"/> is set, or explicitly set it to
            /// <c>true</c> to lock in the new/current description, or set it to
            /// <c>false</c> to reset the names back to the automatic naming
            /// based on the main series.
            /// </remarks>
            public bool? HasCustomDescription { get; set; } = null;

            public CreateOrUpdateGroupBody() { }

            public Group? MergeWithExisting(AnimeGroup? group, int userID, ModelStateDictionary modelState)
            {
                group ??= new() { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
                var parent = ParentGroupID is > 0 ? RepoFactory.AnimeGroup.GetByID(ParentGroupID.Value) : null;
                var groupList = GroupIDs is null ? [] : GroupIDs
                    .Select(groupID => groupID > 0 ? RepoFactory.AnimeGroup.GetByID(groupID) : null)
                    .WhereNotNull()
                    .ToList();
                var seriesList = SeriesIDs is null ? [] : SeriesIDs
                    .Select(id => id > 0 ? RepoFactory.AnimeSeries.GetByID(id) : null)
                    .WhereNotNull()
                    .ToList();
                var allSeriesList = seriesList
                        .Concat(groupList.SelectMany(childGroup => childGroup.AllSeries))
                        .Concat(group.AllSeries)
                        .DistinctBy(series => series.AnimeSeriesID)
                        .ToList();
                var preferredSeries = PreferredSeriesID is > 0 ? allSeriesList.FirstOrDefault(series => series.AnimeSeriesID == PreferredSeriesID.Value) : null;

                // Determine if this is a new or existing group.
                var groupManager = ISystemService.StaticServices.GetRequiredService<IShokoGroupManager>();
                try
                {
                    var isNew = group.AnimeGroupID == 0;
                    if (isNew)
                    {
                        group = (AnimeGroup)groupManager.CreateGroup(new()
                        {
                            Groups = groupList,
                            Series = seriesList,
                            Name = !string.IsNullOrWhiteSpace(Name) ? Name : (preferredSeries?.Title ?? "New Group"),
                            Description = Description,
                            ParentGroup = parent,
                            MainSeries = preferredSeries,
                        });
                    }
                    else
                    {
                        var update = new GroupUpdateData
                        {
                            Groups = groupList,
                            Series = seriesList,
                        };

                        if (HasCustomName is true && string.IsNullOrWhiteSpace(Name))
                            update.Name = group.GroupName; // lock current name without changing value
                        else if (HasCustomName is false)
                            update.Name = null;
                        else if (!string.IsNullOrWhiteSpace(Name))
                            update.Name = Name;

                        if (HasCustomDescription is true && string.IsNullOrWhiteSpace(Description))
                            update.Description = group.Description;
                        else if (HasCustomDescription is false)
                            update.Description = null;
                        else if (Description is not null)
                            update.Description = Description;

                        if (ParentGroupID.HasValue)
                            update.ParentGroup = ParentGroupID.Value is 0 ? null : parent;

                        if (PreferredSeriesID.HasValue)
                            update.MainSeries = preferredSeries;

                        group = (AnimeGroup)groupManager.UpdateGroup(group, update);
                    }
                }
                catch (GenericValidationException ex)
                {
                    foreach (var (key, values) in ex.ValidationErrors)
                        foreach (var value in values)
                            modelState.AddModelError(key, value);
                    return null;
                }

                // Return a new representation of the group.
                return new Group(group, userID);
            }
        }
    }
}

/// <summary>
/// Downloaded, Watched, Total, etc
/// </summary>
public class GroupSizes : SeriesSizes
{
    public GroupSizes()
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
    [Required]
    public int SubGroups { get; set; }

    public class SeriesTypeCounts
    {
        [Required]
        public int Unknown { get; set; }
        [Required]
        public int Other { get; set; }
        [Required]
        public int TV { get; set; }
        [Required]
        public int TVSpecial { get; set; }
        [Required]
        public int Web { get; set; }
        [Required]
        public int Movie { get; set; }
        [Required]
        public int OVA { get; set; }
        [Required]
        public int MusicVideo { get; set; }
    }
}
