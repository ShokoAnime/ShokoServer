using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Server;
using Shoko.Commons.Utils;

namespace AniDBAPI
{
    public class Raw_AniDB_NotifyAlert
    {
        protected long iD;

        public long ID
        {
            get { return iD; }
            set { iD = value; }
        }

        protected long fromUserID;

        public long FromUserID
        {
            get { return fromUserID; }
            set { fromUserID = value; }
        }

        protected string fromUserName;

        public string FromUserName
        {
            get { return fromUserName; }
            set { fromUserName = value; }
        }

        protected int alertDate;

        public int AlertDate
        {
            get { return alertDate; }
            set { alertDate = value; }
        }

        protected long alertType;

        public long AlertType
        {
            get { return alertType; }
            set { alertType = value; }
        }

        protected int alertCount;

        public int AlertCount
        {
            get { return alertCount; }
            set { alertCount = value; }
        }

        protected string relName;

        public string RelName
        {
            get { return relName; }
            set { relName = value; }
        }

        protected string body;

        public string Body
        {
            get { return body; }
            set { body = value; }
        }

        private List<long> fileIDs = new List<long>();

        public List<long> FileIDs
        {
            get { return fileIDs; }
            set { fileIDs = value; }
        }

        public DateTime? AlertDateAsDate
        {
            get { return AniDB.GetAniDBDateAsDate(alertDate); }
        }

        public Raw_AniDB_NotifyAlert(string sRecMessage)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(14).Split('|');

            //{int4 relid}|{int4 type}|{int2 count}|{int4 date}|{str relidname}|{str fids}
            //293 NOTIFYGET
            //0. 6080
            //1. 0
            //2. 2
            //3. 1262144732
            //4. Queen`s Blade: Rurou no Senshi
            //5. 685215,685216,685210,685211,685208,685213,685214,685212,685209


            iD = long.Parse(sDetails[0]);
            alertType = long.Parse(sDetails[1]);
            alertCount = int.Parse(sDetails[2]);
            alertDate = AniDBAPILib.ProcessAniDBInt(sDetails[3]);
            relName = AniDBAPILib.ProcessAniDBString(sDetails[4]);

            string[] fids = sDetails[5].Split(',');

            foreach (string fid in fids)
            {
                long.TryParse(fid, out long lfid);
                if (lfid > 0) fileIDs.Add(lfid);
            }
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Raw_AniDB_NotifyAlert:: ID: " + ID.ToString());
            sb.Append(" | alertDate: " + AlertDateAsDate.ToString());
            sb.Append(" | alertType: " + alertType.ToString());
            sb.Append(" | alertCount: " + alertCount.ToString());
            sb.Append(" | relName: " + relName);
            sb.Append(" | file ids: ");
            foreach (long fid in fileIDs)
            {
                sb.Append(fid.ToString() + ",");
            }


            return sb.ToString();
        }
    }
}