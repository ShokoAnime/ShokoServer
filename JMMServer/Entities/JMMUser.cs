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
		public bool AllowedSeries(ISession session, AnimeSeries ser)
		{
			if (Contract.HideCategories.Count==0) return true;
			if (ser?.Contract?.AniDBAnime == null) return false;
			return !Contract.HideCategories.FindInEnumerable(ser.Contract.AniDBAnime.AllTags);

		}

		public bool AllowedSeries(AnimeSeries ser)
		{
			if (Contract.HideCategories.Count == 0) return true;
			if (ser?.Contract?.AniDBAnime == null) return false;
			return !Contract.HideCategories.FindInEnumerable(ser.Contract.AniDBAnime.AllTags);
		}

		/// <summary>
		/// Returns whether a user is allowed to view this anime
		/// </summary>
		/// <param name="ser"></param>
		/// <returns></returns>
		public bool AllowedAnime(AniDB_Anime anime)
		{
			if (Contract.HideCategories.Count == 0) return true;
			if (anime?.Contract?.AnimeTitles == null) return false;
			return !Contract.HideCategories.FindInEnumerable(anime.Contract.AniDBAnime.AllTags);
		}

		public bool AllowedGroup(AnimeGroup grp)
		{
			if (Contract.HideCategories.Count == 0) return true;
			if (grp.Contract == null) return false;
			return !Contract.HideCategories.FindInEnumerable(grp.Contract.Stat_AllTags);
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
