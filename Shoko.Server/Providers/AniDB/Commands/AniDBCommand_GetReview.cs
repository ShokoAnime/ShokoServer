using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Server.Providers.AniDB.Raws;

namespace Shoko.Server.Providers.AniDB.Commands
{
    public class AniDBCommand_GetReview : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int reviewID;

        public int ReviewID
        {
            get { return reviewID; }
            set { reviewID = value; }
        }

        private string reviewText = string.Empty;

        public string ReviewText
        {
            get { return reviewText; }
            set { reviewText = value; }
        }

        public Raw_AniDB_Review ReviewInfo = null;

        public string GetKey()
        {
            return "AniDBCommand_GetReview" + ReviewID.ToString();
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingReview;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            switch (ResponseCode)
            {
                case 598: return enHelperActivityType.UnknownCommand_598;
                case 555: return enHelperActivityType.Banned_555;
            }

            if (errorOccurred) return enHelperActivityType.NoSuchReview;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "234":
                {
                    // 234 REVIEW 
                    // the first 10 characters should be "240 REVIEW"
                    // the rest of the information should be the data list

                    ReviewInfo = new Raw_AniDB_Review(socketResponse);
                    return enHelperActivityType.GotReview;
                }
                case "334": return enHelperActivityType.NoSuchReview;
                case "501": return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.NoSuchReview;
        }

        public AniDBCommand_GetReview()
        {
            commandType = enAniDBCommandType.GetReview;
        }

        public void Init(int revID)
        {
            this.reviewID = revID;
            commandText = "REVIEW rid=" + revID.ToString();
            commandText += "&part=0";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetReview.Process: Request: {0}", commandText);

            commandID = revID.ToString();
        }
    }
}