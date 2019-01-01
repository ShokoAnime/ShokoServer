using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_UpdateFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public IHash FileData;
        public bool IsWatched;

        public string GetKey()
        {
            return "AniDBCommand_UpdateFile" + FileData.ED2KHash;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.UpdatingFile;
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

            if (errorOccurred) return enHelperActivityType.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "210": return enHelperActivityType.FileAdded;
                case "310": return enHelperActivityType.FileAlreadyExists;
                case "311": return enHelperActivityType.UpdatingFile;
                case "320": return enHelperActivityType.NoSuchFile;
                case "411": return enHelperActivityType.NoSuchMyListFile;

                case "502": return enHelperActivityType.LoginFailed;
                case "501": return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public AniDBCommand_UpdateFile()
        {
            commandType = enAniDBCommandType.UpdateFile;
        }

        public void Init(IHash fileData, bool watched, DateTime? watchedDate)
        {
            FileData = fileData;
            IsWatched = watched;

            commandID = fileData.Info;

            commandText = "MYLISTADD lid=" + fileData.MyListID;
            commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
            commandText += "&state=" + (int) ServerSettings.Instance.AniDb.MyList_StorageState;
            if (watchedDate.HasValue)
                commandText += "&viewdate=" + AniDB.GetAniDBDateAsSeconds(watchedDate.Value);
            commandText += "&edit=1";
        }

        public void Init(IHash fileData, int animeID, string epData, bool watched, DateTime? watchedDate)
        {
            FileData = fileData;
            IsWatched = watched;

            commandID = fileData.Info;

            commandText = "MYLISTADD aid=" + animeID;
            commandText += "&generic=1";
            commandText += "&epno=" + epData;
            commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
            commandText += "&state=" + (int) ServerSettings.Instance.AniDb.MyList_StorageState;
            if (watchedDate.HasValue)
                commandText += "&viewdate=" + AniDB.GetAniDBDateAsSeconds(watchedDate.Value);
            commandText += "&edit=1";
        }
    }
}
