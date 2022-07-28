using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Tasks;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shoko.Server.API.v3.Models.Shoko
{
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
        /// Marked as true when you rename a group to something custom. Different from using a default Series's name
        /// </summary>
        public bool HasCustomName { get; set; }

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

        public Group() { }

        public Group(HttpContext ctx, SVR_AnimeGroup group, bool randomiseImages = false)
        {
            var subGroupCount = group.GetChildGroups().Count;
            int userID = ctx.GetUser()?.JMMUserID ?? 0;
            var allSeries = group.GetAllSeries(skipSorting: true);
            var mainSeries = group.GetMainSeries();
            var episodes = allSeries.SelectMany(a => a.GetAnimeEpisodes()).ToList();

            IDs = new GroupIDs { ID = group.AnimeGroupID };
            if (group.DefaultAnimeSeriesID != null)
                IDs.DefaultSeries = group.DefaultAnimeSeriesID.Value;
            if (mainSeries != null)
                IDs.MainSeries = mainSeries.AnimeSeriesID;
            if (group.AnimeGroupParentID.HasValue)
                IDs.ParentGroup = group.AnimeGroupParentID.Value;
            IDs.TopLevelGroup = group.TopLevelAnimeGroup.AnimeGroupID;

            Name = group.GroupName;
            SortName = group.SortName;
            Description = group.Description;
            Sizes = ModelHelper.GenerateGroupSizes(allSeries, episodes, subGroupCount, userID);
            Size = group.GetSeries().Count;
            HasCustomName = group.IsManuallyNamed == 1;

            Images = mainSeries == null ? new Images() : Series.GetDefaultImages(ctx, mainSeries, randomiseImages);
        }

        #endregion

        public class GroupIDs : IDs
        {
            /// <summary>
            /// The ID of the Default Series, if it has one.
            /// </summary>
            public int? DefaultSeries { get; set; }

            /// <summary>
            /// The ID of the main series for the group, unless the group is empty.
            /// </summary>
            /// <value></value>
            public int? MainSeries { get; set; }

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
        /// For the moment, there are no differences between the normal Group model and this, but for consistency and future, it exists.
        /// </summary>
        public class FullGroup : Group, IFullModel<SVR_AnimeGroup>
        {
            public SVR_AnimeGroup ToServerModel(SVR_AnimeGroup existingModel = null)
            {
                if (existingModel != null)
                {
                    existingModel.GroupName = existingModel.SortName = Name;
                    existingModel.DateTimeUpdated = DateTime.Now;
                    if (IDs.DefaultSeries != 0) existingModel.DefaultAnimeSeriesID = IDs.DefaultSeries;
                    if (IDs.ParentGroup != 0) existingModel.AnimeGroupParentID = IDs.ParentGroup;

                    return existingModel;
                }

                SVR_AnimeGroup group = new SVR_AnimeGroup();
                group.GroupName = group.SortName = Name;
                group.DateTimeCreated = group.DateTimeUpdated = DateTime.Now;

                if (IDs.DefaultSeries != 0) group.DefaultAnimeSeriesID = IDs.DefaultSeries;
                if (IDs.ParentGroup != 0) group.AnimeGroupParentID = IDs.ParentGroup;

                return group;
            }

            /// <summary>
            /// All the series to initially put into the group.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] SeriesIDs { get; set; }
        }

        /// <summary>
        /// Input models.
        /// </summary>
        public class Input
        {
            /// <summary>
            ///
            /// </summary>
            public class CreateGroupBody
            {
                /// <summary>
                /// The group id for merging with an existing series.
                /// /// </summary>
                /// <value></value>
                public int? ID { get; set; } = 0;

                /// <summary>
                /// The group name.
                /// </summary>
                /// <value></value>
                [Required]
                public string Name { get; set; }

                /// <summary>
                ///
                /// </summary>
                /// <value></value>
                public string SortName { get; set; } = null;

                /// <summary>
                /// The group description.
                /// </summary>
                /// <value></value>
                public string Description { get; set; } = null;

                /// <summary>
                /// True if the group will have a persistant custom name.
                /// </summary>
                /// <value></value>
                public bool HasCustomName { get; set; } = false;

                /// <summary>
                /// The <see cref="Group"/> parent ID. Omit it or set it to 0 to
                /// not use a parent group.
                /// </summary>
                /// <value></value>
                public int? ParentID { get; set; } = 0;

                /// <summary>
                /// Manually select the default series for the group.
                /// </summary>
                /// <value></value>
                public int? DefaultSeriesID { get; set; }

                /// <summary>
                /// All the series to initially put into the group.
                /// </summary>
                /// <value></value>
                [Required]
                public int[] SeriesIDs { get; set; }
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
            SeriesTypes = new();
        }

        public GroupSizes(SeriesSizes sizes)
        {
            FileSources = sizes.FileSources;
            Local = sizes.Local;
            Watched = sizes.Watched;
            Total = sizes.Total;
            SeriesTypes = new();
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
        public int SubGroups { get; set;}

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
}
