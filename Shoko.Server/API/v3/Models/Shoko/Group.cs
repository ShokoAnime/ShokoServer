
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
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
        /// The Shoko Group ID. Groups are
        /// </summary>
        public int ID { get; set; }
        
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

            ID = grp.AnimeGroupID;
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
    }
}
