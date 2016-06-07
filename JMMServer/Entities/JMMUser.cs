using System.Linq;
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
        public string PlexUsers { get; set; }

        public Contract_JMMUser ToContract()
        {
            var contract = new Contract_JMMUser();

            contract.JMMUserID = JMMUserID;
            contract.Username = Username;
            contract.Password = Password;
            contract.IsAdmin = IsAdmin;
            contract.IsAniDBUser = IsAniDBUser;
            contract.IsTraktUser = IsTraktUser;
            contract.HideCategories = HideCategories;
            contract.CanEditServerSettings = CanEditServerSettings;
            contract.PlexUsers = PlexUsers;
            return contract;
        }

        /// <summary>
        ///     Returns whether a user is allowed to view this series
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedSeries(ISession session, AnimeSeries ser)
        {
            if (string.IsNullOrEmpty(HideCategories)) return true;

            var cats = HideCategories.ToLower().Split(',');
            var animeCats = ser.GetAnime(session).AllCategories.ToLower().Split('|');
            foreach (var cat in cats)
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
        ///     Returns whether a user is allowed to view this anime
        /// </summary>
        /// <param name="ser"></param>
        /// <returns></returns>
        public bool AllowedAnime(AniDB_Anime anime)
        {
            if (string.IsNullOrEmpty(HideCategories)) return true;

            var cats = HideCategories.ToLower().Split(',');
            var animeCats = anime.AllCategories.ToLower().Split('|');
            foreach (var cat in cats)
            {
                if (!string.IsNullOrEmpty(cat.Trim()) && animeCats.Contains(cat.Trim()))
                {
                    return false;
                }
            }

            return true;
        }

        public bool AllowedGroup(AnimeGroup grp, AnimeGroup_User userRec)
        {
            if (string.IsNullOrEmpty(HideCategories)) return true;

            var cats = HideCategories.ToLower().Split(',');
            var animeCats = grp.ToContract(userRec).Stat_AllTags.ToLower().Split('|');
            foreach (var cat in cats)
            {
                if (!string.IsNullOrEmpty(cat.Trim()) && animeCats.Contains(cat.Trim()))
                {
                    return false;
                }
            }

            return true;
        }
    }
}