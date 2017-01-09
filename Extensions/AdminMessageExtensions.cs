using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Utils;
using Shoko.Models.Azure;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AdminMessageExtensions
    {
        public static DateTime GetMessageDateAsDate(this Azure_AdminMessage message)
        {
            return TimeZone.CurrentTimeZone.ToLocalTime(AniDB.GetAniDBDateAsDate((int) message.MessageDate).Value);
        }

        public static bool GetHasMessageURL(this Azure_AdminMessage message)
        {
            return !String.IsNullOrEmpty(message.MessageURL);
        }

        public static string ToStringEx(this Azure_AdminMessage message)
        {
            return $"{message.AdminMessageId} - {message.GetMessageDateAsDate()} - {message.Message}";
        }
    }
}
