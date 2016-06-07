using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetCalendar : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_GetCalendar()
        {
            commandType = enAniDBCommandType.GetCalendar;
        }

        public CalendarCollection Calendars { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetCalendar";
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingCalendar;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.CalendarEmpty;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "297":
                    {
                        Calendars = new CalendarCollection(socketResponse);
                        return enHelperActivityType.GotCalendar;
                    }
                case "397":
                    {
                        return enHelperActivityType.CalendarEmpty;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.CalendarEmpty;
        }

        public void Init()
        {
            commandText = "CALENDAR ";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Request: {0}", commandText);

            commandID = "CALENDAR ";
        }
    }
}