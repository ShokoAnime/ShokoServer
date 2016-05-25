using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
	public class JMMUser
	{
		public int JMMUserID { get; private set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public int IsAdmin { get; set; }
		public int IsAniDBUser { get; set; }
		public int IsTraktUser { get; set; }
		public string HideCategories { get; set; }
		public int? CanEditServerSettings { get; set; }
        public string PlexUsers { get; set; }

        public Contract_JMMUser ToContract()
		{
			Contract_JMMUser contract = new Contract_JMMUser();

			contract.JMMUserID = this.JMMUserID;
			contract.Username = this.Username;
			contract.Password = this.Password;
			contract.IsAdmin = this.IsAdmin;
			contract.IsAniDBUser = this.IsAniDBUser;
			contract.IsTraktUser = this.IsTraktUser;
			contract.HideCategories = this.HideCategories;
			contract.CanEditServerSettings = this.CanEditServerSettings;
            contract.PlexUsers = this.PlexUsers;
			return contract;
		}

		/// <summary>
		/// Returns whether a user is allowed to view this series
		/// </summary>
		/// <param name="ser"></param>
		/// <returns></returns>
		public bool AllowedSeries(ISession session, AnimeSeries ser)
		{
			if (string.IsNullOrEmpty(HideCategories)) return true;

			string[] cats = HideCategories.ToLower().Split(',');
			string[] animeCats = ser.GetAnime(session).AllCategories.ToLower().Split('|');
			foreach (string cat in cats)
			{
				if (!string.IsNullOrEmpty(cat.Trim()) && animeCats.Contains(cat.Trim()))
				{
					return false;
				}
			}

			return true;
		}

		public bool AllowedSeries(AnimeSeries ser)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return AllowedSeries(session, ser);
			}
		}

		/// <summary>
		/// Returns whether a user is allowed to view this anime
		/// </summary>
		/// <param name="ser"></param>
		/// <returns></returns>
		public bool AllowedAnime(AniDB_Anime anime)
		{
			if (string.IsNullOrEmpty(HideCategories)) return true;

			string[] cats = HideCategories.ToLower().Split(',');
			string[] animeCats = anime.AllCategories.ToLower().Split('|');
			foreach (string cat in cats)
			{
				if (!string.IsNullOrEmpty(cat.Trim()) && animeCats.Contains(cat.Trim()))
				{
					return false;
				}
			}

			return true;
		}

		public bool AllowedGroup(AnimeGroup grp, JMMUser user)
		{
		    try
		    {
                if (string.IsNullOrEmpty(HideCategories)) return true;

                string[] cats = HideCategories.ToLower().Split(',');
                string[] animeCats = ((grp.GetUserContract(user.JMMUserID).Stat_AllTags) ?? "").ToLower().Split('|');
                foreach (string cat in cats)
                {
                    if (!string.IsNullOrEmpty(cat.Trim()) && animeCats.Contains(cat.Trim()))
                    {
                        return false;
                    }
                }

                return true;

            }
		    catch (Exception e)
    	    {
	            return false;
		    }
        }



        public void UpdateGroupFilters()
        {
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
            AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();
            GroupFilterRepository repGrpFilter = new GroupFilterRepository();
            List<GroupFilter> gfs = repGrpFilter.GetAll();
            List<AnimeGroup> allGrps = repGroups.GetAllTopLevelGroups(); // No Need of subgroups
            foreach (GroupFilter gf in gfs)
            {

                bool change = false;
                foreach (AnimeGroup grp in allGrps)
                {
                    AnimeGroup_User userRec = repUserGroups.GetByUserAndGroupID(JMMUserID, grp.AnimeGroupID);
                    if (gf.EvaluateGroupFilter(grp, this, userRec))
                    {
                        if (!gf.GroupsIds.ContainsKey(JMMUserID))
                        {
                            gf.GroupsIds[JMMUserID] = new HashSet<int>();
                        }
                        if (!gf.GroupsIds[JMMUserID].Contains(grp.AnimeGroupID))
                        {
                            gf.GroupsIds[JMMUserID].Add(grp.AnimeGroupID);
                            change = true;
                        }
                    }
                    else
                    {
                        if (gf.GroupsIds.ContainsKey(JMMUserID))
                        {
                            if (gf.GroupsIds[JMMUserID].Contains(grp.AnimeGroupID))
                            {
                                gf.GroupsIds[JMMUserID].Remove(grp.AnimeGroupID);
                                change = true;
                            }
                        }
                    }
                    if (change)
                        repGrpFilter.Save(gf, false, this);
                }
            }
        }
    }
}
