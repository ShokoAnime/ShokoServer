using System;
using Shoko.Server;

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
                //ShokoService.AnidbProcessor.IsBanned = true;
                //ShokoService.AnidbProcessor.BanOrigin = "HTTP";
                ShokoService.AnidbProcessor.IsHttpBanned = true;
                return true;
            }

            return false;
        }
    }
}