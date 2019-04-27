
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
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

        #region Constructors and Helper Methods

        public Group(HttpContext ctx, int id)
        {
            SVR_AnimeGroup group = RepoFactory.AnimeGroup.GetByID(id);
            GenerateFromAnimeGroup(ctx, group);
        }

        public void GenerateFromAnimeGroup(HttpContext ctx, SVR_AnimeGroup grp)
        {
            int uid = ctx.GetUser()?.JMMUserID ?? 0;
            List<SVR_AnimeEpisode> ael = grp.GetAllSeries().SelectMany(a => a.GetAnimeEpisodes()).ToList();

            ID = grp.AnimeGroupID;
            Name = grp.GroupName;
            Sizes = ModelHelper.GenerateSizes(ael, uid);
            Size = grp.GetSeries().Count;
        }

        #endregion
    }
}
