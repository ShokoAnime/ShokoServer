using System;
using JMMServer;

namespace AniDBAPI.Commands
{
    public abstract class AniDBHTTPCommand
    {
        public string commandID = string.Empty;
        public enAniDBCommandType commandType;

        public bool CheckForBan(string xmlresult)
        {
            if (string.IsNullOrEmpty(xmlresult)) return false;

            int index = xmlresult.IndexOf(@">banned<", 0, StringComparison.InvariantCultureIgnoreCase);
            if (index > -1)
            {
                JMMService.AnidbProcessor.IsBanned = true;
                JMMService.AnidbProcessor.BanOrigin = "HTTP";
                return true;
            }

            return false;
        }
    }
}