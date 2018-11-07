using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.Raws
{
    public class Raw_AniDB_NotifyList
    {
        private List<NotifyListHeader> headers = new List<NotifyListHeader>();

        public List<NotifyListHeader> Headers
        {
            get { return headers; }
            set { headers = value; }
        }

        public Raw_AniDB_NotifyList(string sRecMessage)
        {
            /*
			// 291 NOTIFYLIST
			M|1251116
			M|1251199
			M|1251090
			M|1251036
			M|1250854
			*/

            // remove the header info
            string[] sDetails = sRecMessage.Substring(0).Split('\n');

            if (sDetails.Length <= 2) return;

            for (int i = 1; i < sDetails.Length - 1; i++)
                // first item will be the status command, and last will be empty
            {
                NotifyListHeader head = new NotifyListHeader();

                // {str type}|{int4 id}
                string[] flds = sDetails[i].Substring(0).Split('|');
                head.NotifyType = flds[0].Trim().ToUpper();
                head.NotifyID = long.Parse(flds[1]);
                headers.Add(head);

                //BaseConfig.MyAnimeLog.Write("grp: {0}", grp);
            }
        }
    }
}