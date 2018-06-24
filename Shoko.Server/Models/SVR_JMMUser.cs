using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using Nancy.Json;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_JMMUser : JMMUser, IIdentity
    {

        // IUserIdentity implementation
        [NotMapped]
        public string UserName => Username;

        [NotMapped]
        [ScriptIgnore]
        public IEnumerable<string> Claims { get; set; }

        /// <summary>
        ///     Returns whether a user is allowed to view this series
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
        ///     Returns whether a user is allowed to view this anime
        /// </summary>
        /// <param name="anime"></param>
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
            IReadOnlyList<SVR_GroupFilter> gfs = RepoFactory.GroupFilter.GetAll();
            List<SVR_AnimeGroup> allGrps = RepoFactory.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
            foreach (SVR_GroupFilter gf in gfs)
            {
                bool change = false;
                foreach (SVR_AnimeGroup grp in allGrps)
                {
                    CL_AnimeGroup_User cgrp = grp.GetUserContract(JMMUserID);
                    change |= gf.UpdateGroupFilterFromGroup(cgrp, this);
                }
                upd.Commit();
            }

        }

        [NotMapped]
        public string AuthenticationType { get; } = "api";
        [NotMapped]
        public bool IsAuthenticated { get; } = true;
        [NotMapped]
        public string Name => Username;
    }
}