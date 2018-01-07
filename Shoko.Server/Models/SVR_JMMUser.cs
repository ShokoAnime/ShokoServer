using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Nancy.Json;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_JMMUser : JMMUser
    {
        public SVR_JMMUser()
        {
        }

        /// <summary>
        /// Returns whether a user is allowed to view this series
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedSeries(SVR_AnimeSeries ser)
        {
            if (this.GetHideCategories().Count == 0) return true;
            if (ser?.Contract?.AniDBAnime == null) return false;
            return !this.GetHideCategories().FindInEnumerable(ser.Contract.AniDBAnime.AniDBAnime.GetAllTags());
        }

        /// <summary>
        /// Returns whether a user is allowed to view this anime
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedAnime(SVR_AniDB_Anime anime)
        {
            if (this.GetHideCategories().Count == 0) return true;
            if (anime?.Contract?.AnimeTitles == null) return false;
            return !this.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags());
        }

        public bool AllowedGroup(SVR_AnimeGroup grp)
        {
            if (this.GetHideCategories().Count == 0) return true;
            if (grp.Contract == null) return false;
            return !this.GetHideCategories().FindInEnumerable(grp.Contract.Stat_AllTags);
        }

        public static bool CompareUser(JMMUser olduser, JMMUser newuser)
        {
            if (olduser == null || olduser.HideCategories == newuser.HideCategories)
                return true;
            return false;
        }

        public void UpdateGroupFilters()
        {
            List<SVR_GroupFilter> gfs = Repo.GroupFilter.GetAll();
            List<SVR_AnimeGroup> allGrps = Repo.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
            List<SVR_AnimeSeries> allSeries = Repo.AnimeSeries.GetAll();
            foreach (SVR_GroupFilter gf in gfs)
            {
                using (var upd = Repo.GroupFilter.BeginUpdate(gf))
                {
                    bool change = false;
                    foreach (SVR_AnimeGroup grp in allGrps)
                    {
                        CL_AnimeGroup_User cgrp = grp.GetUserContract(this.JMMUserID);
                        change |= gf.CalculateGroupFilterGroups(cgrp, this, JMMUserID);
                    }
                    foreach (SVR_AnimeSeries ser in allSeries)
                    {
                        CL_AnimeSeries_User cser = ser.GetUserContract(this.JMMUserID);
                        change |= gf.CalculateGroupFilterSeries(cser, this, JMMUserID);
                    }
                    if (change)
                        upd.Commit();
                }
            }
        }

        // IUserIdentity implementation
        [NotMapped]
        public string UserName => Username;

        [NotMapped]
        [ScriptIgnore]
        public IEnumerable<string> Claims { get; set; }


       
    }
}