using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
//using System.Web.Script.Serialization;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_JMMUser : JMMUser, System.Security.Principal.IIdentity
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


            List<SVR_AnimeGroup> allGrps = Repo.Instance.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
            IReadOnlyList<SVR_AnimeSeries> allSeries = Repo.Instance.AnimeSeries.GetAll();

            using (var upd = Repo.Instance.GroupFilter.BeginBatchUpdate(() => Repo.Instance.GroupFilter.GetAll()))
            {

                foreach (SVR_GroupFilter gf in upd)
                {
                    bool change = false;
                    foreach (SVR_AnimeGroup grp in allGrps)
                    {
                        CL_AnimeGroup_User cgrp = grp.GetUserContract(this.JMMUserID);
                        change |= gf.CalculateGroupFilterGroups(cgrp, this);
                    }
                    foreach (SVR_AnimeSeries ser in allSeries)
                    {
                        CL_AnimeSeries_User cser = ser.GetUserContract(this.JMMUserID);
                        change |= gf.CalculateGroupFilterSeries(cser, this);
                    }

                    if (change)
                        upd.Update(gf);
                }
                upd.Commit();
            }
        }

        //[JsonIgnore]
        [NotMapped] public IEnumerable<string> Claims { get; set; }

        [NotMapped] string IIdentity.AuthenticationType => "API";

        [NotMapped] bool IIdentity.IsAuthenticated => true;

        [NotMapped] string IIdentity.Name => Username;

        public SVR_JMMUser(string username)
        {
            foreach (SVR_JMMUser us in Repo.Instance.JMMUser.GetAll())
            {
                if (string.Equals(us.Username, username, System.StringComparison.CurrentCultureIgnoreCase))
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
                    break;
                }
            }
        }
    }
}