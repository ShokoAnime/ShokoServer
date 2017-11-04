using System.Collections.Generic;
using System.Web.Script.Serialization;
using NHibernate;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Models
{
    public class SVR_JMMUser : JMMUser, Nancy.Security.IUserIdentity
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
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }

        // IUserIdentity implementation
        [ScriptIgnore]
        public string UserName
        {
            get { return Username; }
        }

        [ScriptIgnore]
        public IEnumerable<string> Claims { get; set; }


        public SVR_JMMUser(string username)
        {
            foreach (SVR_JMMUser us in RepoFactory.JMMUser.GetAll())
            {
                if (us.Username.ToLower() == username.ToLower())
                {
                    JMMUserID = us.JMMUserID;
                    Username = us.Username;
                    Password = us.Password;
                    IsAdmin = us.IsAdmin;
                    IsAniDBUser = us.IsAniDBUser;
                    IsTraktUser = us.IsTraktUser;
                    HideCategories = us.HideCategories;
                    CanEditServerSettings = us.CanEditServerSettings;
                    PlexUsers = us.PlexUsers;
                    Claims = us.Claims;
                    break;
                }
            }
        }
    }
}