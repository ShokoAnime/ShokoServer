using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Tasks;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shoko.Server.API.v3
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
        /// Marked as true when you rename a group to something custom. Different from using a default Series's name
        /// </summary>
        public bool HasCustomName { get; set; }

        #region Constructors and Helper Methods

        public Group() {}

        public Group(HttpContext ctx, SVR_AnimeGroup grp)
        {
            int uid = ctx.GetUser()?.JMMUserID ?? 0;
            var allSeries = grp.GetAllSeries(skipSorting: true);
            List<SVR_AnimeEpisode> ael = allSeries.SelectMany(a => a.GetAnimeEpisodes()).ToList();

            IDs = new GroupIDs {ID = grp.AnimeGroupID};
            if (grp.DefaultAnimeSeriesID != null) IDs.DefaultSeries = grp.DefaultAnimeSeriesID.Value;

            Name = grp.GroupName;
            Sizes = ModelHelper.GenerateSizes(ael, uid);
            Size = grp.GetSeries().Count;

            HasCustomName = GetHasCustomName(grp);
        }

        #endregion

        private bool GetHasCustomName(SVR_AnimeGroup grp)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var groupCalculator = AutoAnimeGroupCalculator.Create(session.Wrap(), AutoGroupExclude.None);
                int id = grp.GetSeries().FirstOrDefault()?.AniDB_ID ?? 0;
                if (id == 0) return true;
                var ids = groupCalculator.GetIdsOfAnimeInSameGroup(id);
                return !ids.Select(aid => RepoFactory.AniDB_Anime.GetByAnimeID(aid)).Where(anime => anime != null)
                    .Any(anime => anime.GetAllTitles().Contains(grp.GroupName));
            }
        }

        public class GroupIDs : IDs
        {
            /// <summary>
            /// The ID of the Default Series, if it has one.
            /// </summary>
            public int DefaultSeries { get; set; }
            
            /// <summary>
            /// Parent Group, if it has one
            /// </summary>
            public int ParentGroup { get; set; }
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
        }
    }
}
