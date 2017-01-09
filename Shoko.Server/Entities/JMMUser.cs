using System.Collections.Generic;
using NHibernate;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Entities
{
    public class JMMUser: Nancy.Security.IUserIdentity
    {
        public int JMMUserID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int IsAdmin { get; set; }
        public int IsAniDBUser { get; set; }
        public int IsTraktUser { get; set; }
        public string HideCategories { get; set; }
        public int? CanEditServerSettings { get; set; }
        public string PlexUsers { get; set; }


        private Contract_JMMUser _contract = null;

        public virtual Contract_JMMUser Contract
        {
            get
            {
                if (_contract == null)
                    JMMUserRepository.GenerateContract(this);
                return _contract;
            }
            set { _contract = value; }
        }

        /// <summary>
        /// Returns whether a user is allowed to view this series
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedSeries(ISession session, SVR_AnimeSeries ser)
        {
            if (Contract.HideCategories.Count == 0) return true;
            if (ser?.Contract?.AniDBAnime == null) return false;
            return !Contract.HideCategories.FindInEnumerable(ser.Contract.AniDBAnime.AniDBAnime.GetAllTags());
        }

        public bool AllowedSeries(SVR_AnimeSeries ser)
        {
            if (Contract.HideCategories.Count == 0) return true;
            if (ser?.Contract?.AniDBAnime == null) return false;
            return !Contract.HideCategories.FindInEnumerable(ser.Contract.AniDBAnime.AniDBAnime.GetAllTags());
        }

        /// <summary>
        /// Returns whether a user is allowed to view this anime
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedAnime(SVR_AniDB_Anime anime)
        {
            if (Contract.HideCategories.Count == 0) return true;
            if (anime?.Contract?.AnimeTitles == null) return false;
            return !Contract.HideCategories.FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags());
        }

        public bool AllowedGroup(SVR_AnimeGroup grp)
        {
            if (Contract.HideCategories.Count == 0) return true;
            if (grp.Contract == null) return false;
            return !Contract.HideCategories.FindInEnumerable(grp.Contract.Stat_AllTags);
        }

        public static bool CompareUser(Contract_JMMUser olduser, Contract_JMMUser newuser)
        {
            if (olduser == null || !olduser.HideCategories.SetEquals(newuser.HideCategories))
                return true;
            return false;
        }

        public void UpdateGroupFilters()
        {
            IReadOnlyList<GroupFilter> gfs = RepoFactory.GroupFilter.GetAll();
            List<SVR_AnimeGroup> allGrps = RepoFactory.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
            foreach (GroupFilter gf in gfs)
            {
                bool change = false;
                foreach (SVR_AnimeGroup grp in allGrps)
                {
                    CL_AnimeGroup_User cgrp = grp.GetUserContract(this.JMMUserID);
                    change |= gf.CalculateGroupFilterGroups(cgrp, Contract, JMMUserID);
                }
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    CL_AnimeSeries_User cser = ser.GetUserContract(this.JMMUserID);
                    change |= gf.CalculateGroupFilterSeries(cser, Contract, JMMUserID);
                }
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }

        // IUserIdentity implementation
        public string UserName
        {
            get
            {
                return Username;
            }
        }

        public IEnumerable<string> Claims { get; set; }

        public JMMUser()
        {

        }

        public JMMUser(string username)
        {
            foreach (JMMUser us in RepoFactory.JMMUser.GetAll())
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
                    _contract = us._contract;
                    Claims = us.Claims;
                    break;
                }
            }
        }
    }
}