using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetReview : AniDBUDPCommand, IAniDBUDPCommand
    {
        public Raw_AniDB_Review ReviewInfo;

        public AniDBCommand_GetReview()
        {
            commandType = enAniDBCommandType.GetReview;
        }

        public int ReviewID { get; set; }

        public string ReviewText { get; set; } = "";

        public string GetKey()
        {
            return "AniDBCommand_GetReview" + ReviewID;
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
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchReview;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


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
                case "334":
                    {
                        return enHelperActivityType.NoSuchReview;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchReview;
        }

        public void Init(int revID)
        {
            ReviewID = revID;
            commandText = "REVIEW rid=" + revID;
            commandText += "&part=0";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetReview.Process: Request: {0}", commandText);

            commandID = revID.ToString();
        }
    }
}