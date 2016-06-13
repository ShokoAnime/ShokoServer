using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using JMMServer;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_UpdateFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public IHash FileData = null;
        public bool IsWatched = false;

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
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "210":
                    return enHelperActivityType.FileAdded;
                case "310":
                    return enHelperActivityType.FileAlreadyExists;
                case "311":
                    return enHelperActivityType.UpdatingFile;
                case "320":
                    return enHelperActivityType.NoSuchFile;
                case "411":
                    return enHelperActivityType.NoSuchMyListFile;

                case "502":
                    return enHelperActivityType.LoginFailed;
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public AniDBCommand_UpdateFile()
        {
            commandType = enAniDBCommandType.UpdateFile;
        }

        /*public void Init(IHash fileData, bool watched)
		{
			FileData = fileData;
			IsWatched = watched;

			commandID = fileData.Info;

			commandText = "MYLISTADD size=" + fileData.FileSize.ToString();
			commandText += "&ed2k=" + fileData.ED2KHash;
			commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
			commandText += "&edit=1";
		}*/

        public void Init(IHash fileData, bool watched, DateTime? watchedDate, bool isEdit, AniDBFileStatus? fileState)
        {
            FileData = fileData;
            IsWatched = watched;

            commandID = fileData.Info;

            commandText = "MYLISTADD size=" + fileData.FileSize.ToString();
            commandText += "&ed2k=" + fileData.ED2KHash;
            commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
            if (fileState.HasValue)
                commandText += "&state=" + (int) fileState;
            if (watchedDate.HasValue)
                commandText += "&viewdate=" + Utils.GetAniDBDateAsSeconds(watchedDate.Value).ToString();
            if (isEdit)
                commandText += "&edit=1";
        }

        public void Init(int animeID, int episodeNumber, bool watched, bool isEdit)
        {
            IsWatched = watched;

            commandText = "MYLISTADD aid=" + animeID.ToString();
            commandText += "&generic=1";
            commandText += "&epno=" + episodeNumber.ToString();
            commandText += "&state=" + (int) ServerSettings.AniDB_MyList_StorageState;
            commandText += "&viewed=" + (IsWatched ? "1" : "0"); //viewed
            if (isEdit)
                commandText += "&edit=1";
        }
    }
}