using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
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
				if (!string.IsNullOrEmpty(cat) && animeCats.Contains(cat))
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
				if (!string.IsNullOrEmpty(cat) && animeCats.Contains(cat))
				{
					return false;
				}
			}

			return true;
		}

		public bool AllowedGroup(AnimeGroup grp, AnimeGroup_User userRec)
		{
			if (grp.AnimeGroupID == 266)
				Console.Write("");

			if (string.IsNullOrEmpty(HideCategories)) return true;

			string[] cats = HideCategories.ToLower().Split(',');
			string[] animeCats = grp.ToContract(userRec).Stat_AllTags.ToLower().Split('|');
			foreach (string cat in cats)
			{
				if (!string.IsNullOrEmpty(cat) && animeCats.Contains(cat))
				{
					return false;
				}
			}

			return true;
		}
	}
}
