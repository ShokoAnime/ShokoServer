using System;
using System.Collections.Generic;
using System.Text;
using JMMServer;

namespace AniDBAPI
{
    public class Raw_AniDB_NotifyAlert
    {
        protected int alertCount;

        protected int alertDate;

        protected long alertType;

        protected string body;

        protected long fromUserID;

        protected string fromUserName;
        protected long iD;

        protected string relName;

        public Raw_AniDB_NotifyAlert(string sRecMessage)
        {
            // remove the header info
            var sDetails = sRecMessage.Substring(14).Split('|');

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

            var fids = sDetails[5].Split(',');

            foreach (var fid in fids)
            {
                long lfid = 0;
                long.TryParse(fid, out lfid);
                if (lfid > 0) FileIDs.Add(lfid);
            }
        }

        public long ID
        {
            get { return iD; }
            set { iD = value; }
        }

        public long FromUserID
        {
            get { return fromUserID; }
            set { fromUserID = value; }
        }

        public string FromUserName
        {
            get { return fromUserName; }
            set { fromUserName = value; }
        }

        public int AlertDate
        {
            get { return alertDate; }
            set { alertDate = value; }
        }

        public long AlertType
        {
            get { return alertType; }
            set { alertType = value; }
        }

        public int AlertCount
        {
            get { return alertCount; }
            set { alertCount = value; }
        }

        public string RelName
        {
            get { return relName; }
            set { relName = value; }
        }

        public string Body
        {
            get { return body; }
            set { body = value; }
        }

        public List<long> FileIDs { get; set; } = new List<long>();

        public DateTime? AlertDateAsDate
        {
            get { return Utils.GetAniDBDateAsDate(alertDate); }
        }


        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Raw_AniDB_NotifyAlert:: ID: " + ID);
            sb.Append(" | alertDate: " + AlertDateAsDate);
            sb.Append(" | alertType: " + alertType);
            sb.Append(" | alertCount: " + alertCount);
            sb.Append(" | relName: " + relName);
            sb.Append(" | file ids: ");
            foreach (var fid in FileIDs)
            {
                sb.Append(fid + ",");
            }


            return sb.ToString();
        }
    }
}