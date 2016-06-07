using System;
using System.Text;
using JMMServer;

namespace AniDBAPI
{
    public class Raw_AniDB_NotifyMessage
    {
        protected string body;

        protected long fromUserID;

        protected string fromUserName;

        protected long iD;

        protected int messageDate;

        protected long messageType;

        protected string title;

        public Raw_AniDB_NotifyMessage(string sRecMessage)
        {
            // remove the header info
            var sDetails = sRecMessage.Substring(14).Split('|');

            //{int4 id}|{int4 from_user_id}|{str from_user_name}|{int4 date}|{int4 type}|{str title}|{str body}
            //292 NOTIFYGET
            //0. 1010180
            //1. 0
            //2. -unknown-
            //3. 1243764587
            //4. 2
            //5. Group Drop Notification - [LIME]/Amaenaide yo!! Katsu!!
            //6. This is an automated message notifying you that a group has dropped an anime<br />you were collecting.<br /><br />Group: LIME Anime [LIME] (<a href="http://anidb.net/g2182">2182</a>)<br />Anime: Amaenaide yo!! Katsu!! (<a href="http://anidb.net/a4145">4145</a>)<br /><br />marked dropped by: keitarou (1294)<br /><br />comment/reason:<br />GRANTED CREQ<br />


            iD = long.Parse(sDetails[0]);
            fromUserID = long.Parse(sDetails[1]);
            fromUserName = AniDBAPILib.ProcessAniDBString(sDetails[2]);
            messageDate = AniDBAPILib.ProcessAniDBInt(sDetails[3]);
            messageType = long.Parse(sDetails[4]);
            title = AniDBAPILib.ProcessAniDBString(sDetails[5]);
            body = AniDBAPILib.ProcessAniDBString(sDetails[6]);
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

        public int MessageDate
        {
            get { return messageDate; }
            set { messageDate = value; }
        }

        public long MessageType
        {
            get { return messageType; }
            set { messageType = value; }
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public string Body
        {
            get { return body; }
            set { body = value; }
        }

        public DateTime? MessageDateAsDate
        {
            get { return Utils.GetAniDBDateAsDate(MessageDate); }
        }


        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("AniDB_NotifyMessage:: ID: " + ID);
            sb.Append(" | messageDate: " + MessageDateAsDate);
            sb.Append(" | fromUserName: " + fromUserName);
            sb.Append(" | title: " + title);
            sb.Append(" | body: " + body);


            return sb.ToString();
        }
    }
}