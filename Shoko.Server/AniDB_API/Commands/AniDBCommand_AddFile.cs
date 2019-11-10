using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_AddFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public IHash FileData;
        public bool ReturnIsWatched;
        public DateTime? WatchedDate;
        public AniDBFile_State? State;
        public int MyListID;

        public string GetKey()
        {
            return "AniDBCommand_AddFile" + FileData.ED2KHash;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.AddingFile;
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
                case "210":
                {
                    /* Response Format
                     * {int4 mylist id of new entry}
                     */
                    // parse the MyList ID
                    string[] arrResult = socketResponse.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        int.TryParse(arrResult[1], out MyListID);
                        if (FileData == null) MyListID = 0;
                    }
                    return AniDBUDPResponseCode.FileAdded;
                }
                case "310":
                {
                    /* Response Format
                     * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                     */
                    //file already exists: read 'watched' status
                    string[] arrResult = socketResponse.Split('\n');
                    if (arrResult.Length >= 2)
                    {
                        string[] arrStatus = arrResult[1].Split('|');
                        int.TryParse(arrStatus[0], out MyListID);

                        int state = int.Parse(arrStatus[6]);
                        State = (AniDBFile_State) state;

                        int viewdate = int.Parse(arrStatus[7]);
                        ReturnIsWatched = viewdate > 0;

                        if (ReturnIsWatched)
                        {
                            DateTime utcDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            utcDate = utcDate.AddSeconds(viewdate);

                            WatchedDate = utcDate.ToLocalTime();
                        }
                        else
                        {
                            WatchedDate = null;
                        }
                    }
                    return AniDBUDPResponseCode.FileAlreadyExists;
                }
                case "311": return AniDBUDPResponseCode.UpdatingFile;
                case "320": return AniDBUDPResponseCode.NoSuchFile;
                case "411": return AniDBUDPResponseCode.NoSuchFile;
                case "502": return AniDBUDPResponseCode.LoginFailed;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_AddFile()
        {
            commandType = enAniDBCommandType.AddFile;
        }

        public void Init(IHash fileData, AniDBFile_State fileState, DateTime? watchedDate = null)
        {
            FileData = fileData;

            commandID = fileData.Info;

            commandText = "MYLISTADD size=" + fileData.FileSize;
            commandText += "&ed2k=" + fileData.ED2KHash;
            if (watchedDate == null)
                commandText += "&viewed=0";
            else
            {
                commandText += "&viewed=1";
                commandText += "&viewdate=" + AniDB.GetAniDBDateAsSeconds(watchedDate.Value);
            }
            commandText += "&state=" + (int) fileState;
        }

        public void Init(int animeID, int episodeNumber, AniDBFile_State fileState, DateTime? watchedDate = null)
        {
            // MYLISTADD aid={int4 aid}&generic=1&epno={int4 episode number}

            commandText = "MYLISTADD aid=" + animeID;
            commandText += "&generic=1";
            commandText += "&epno=" + episodeNumber;
            if (watchedDate == null)
                commandText += "&viewed=0";
            else
            {
                commandText += "&viewed=1";
                commandText += "&viewdate=" + AniDB.GetAniDBDateAsSeconds(watchedDate.Value);
            }
            commandText += "&state=" + (int) fileState;
        }
    }
}
