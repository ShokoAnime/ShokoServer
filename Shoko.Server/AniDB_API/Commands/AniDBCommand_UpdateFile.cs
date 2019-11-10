using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server;
using Shoko.Server.Settings;

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

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.UpdatingFile;
        }

        public virtual AniDBUDPResponseCode Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            switch (ResponseCode)
            {
                case 598: return AniDBUDPResponseCode.UnknownCommand_598;
                case 555: return AniDBUDPResponseCode.Banned_555;
            }

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "210": return AniDBUDPResponseCode.FileAdded;
                case "310": return AniDBUDPResponseCode.FileAlreadyExists;
                case "311": return AniDBUDPResponseCode.UpdatingFile;
                case "320": return AniDBUDPResponseCode.NoSuchFile;
                case "411": return AniDBUDPResponseCode.NoSuchMyListFile;

                case "502": return AniDBUDPResponseCode.LoginFailed;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
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
        
        public void Init(IHash fileData, int animeID, int epnumber, bool watched, DateTime? watchedDate)
        {
            FileData = fileData;
            IsWatched = watched;

            commandID = fileData.Info;

            commandText = "MYLISTADD aid=" + animeID;
            commandText += "&generic=1";
            commandText += "&epno=" + epnumber;
            commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
            commandText += "&state=" + (int) ServerSettings.Instance.AniDb.MyList_StorageState;
            if (watchedDate.HasValue)
                commandText += "&viewdate=" + AniDB.GetAniDBDateAsSeconds(watchedDate.Value);
            commandText += "&edit=1";
        }
    }
}