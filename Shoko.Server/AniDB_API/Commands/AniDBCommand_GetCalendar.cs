using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetCalendar : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string GetKey()
        {
            return "AniDBCommand_GetCalendar";
        }

        private CalendarCollection calendars = null;

        public CalendarCollection Calendars
        {
            get { return calendars; }
            set { calendars = value; }
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
            switch (ResponseCode)
            {
                case 598: return enHelperActivityType.UnknownCommand_598;
                case 555: return enHelperActivityType.Banned_555;
            }


            if (errorOccurred) return enHelperActivityType.CalendarEmpty;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "297":
                {
                    calendars = new CalendarCollection(socketResponse);
                    return enHelperActivityType.GotCalendar;
                }
                case "397": return enHelperActivityType.CalendarEmpty;
                case "501": return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.CalendarEmpty;
        }

        public AniDBCommand_GetCalendar()
        {
            commandType = enAniDBCommandType.GetCalendar;
        }

        public void Init()
        {
            commandText = "CALENDAR ";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Request: {0}", commandText);

            commandID = "CALENDAR ";
        }
    }
}