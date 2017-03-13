using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Models.Interfaces;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetFileInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        public Raw_AniDB_File fileInfo = null;

        //public Raw_AniDB_Episode episodeInfo = null;
        public IHash fileData = null;

        private bool forceRefresh = false;

        public bool ForceRefresh
        {
            get { return forceRefresh; }
            set { forceRefresh = value; }
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingFileInfo;
        }

        public string GetKey()
        {
            return "AniDBCommand_GetFileInfo" + fileData.ED2KHash;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.FileDoesNotExist;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetFileInfo.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "220":
                {
                    // 220 FILE INFO
                    // the first 9 characters should be "220 FILE "
                    // the rest of the information should be the data list

                    fileInfo = new Raw_AniDB_File(socketResponse);
                    //episodeInfo = new Raw_AniDB_Episode(socketResponse, enEpisodeSourceType.File);
                    return enHelperActivityType.GotFileInfo;
                }
                case "320":
                {
                    return enHelperActivityType.FileDoesNotExist;
                }
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public AniDBCommand_GetFileInfo()
        {
            commandType = enAniDBCommandType.GetFileInfo;
        }

        public void Init(IHash fileData, bool force)
        {
            int fByte1 = 127; // fmask - byte1 (old 120 Added other episodes)
            int fByte2 = 248; // old 72 fmask - byte2 (Added FileSize, SHA1, MD5)
            int fByte3 = 255; // fmask - byte3
            int fByte4 = 249; // fmask - byte4
            int fByte5 = 254; // fmask - byte5

            int aByte1 = 0; // amask - byte1
            int aByte2 = 0; // amask - byte2
            int aByte3 = 252; // amask - byte3 old 236 Added Kanji name
            int aByte4 = 192; // amask - byte4

            this.fileData = fileData;
            this.forceRefresh = force;

            commandID = fileData.Info;
            // 220 FILE572794|6107|99294|2723|c646d82a184a33f4e4f98af39f29a044|8452c4bf|high|HDTV|Vorbis (Ogg Vorbis)|148|H264/AVC|1773|1280x720|mkv|1470||1239494400|2|The Day It Began|Hajimari no Hi|712|14|Eclipse Productions|Eclipse


            commandText = "FILE size=" + fileData.FileSize.ToString();
            commandText += "&ed2k=" + fileData.ED2KHash;
            commandText += string.Format("&fmask={0}{1}{2}{3}{4}", fByte1.ToString("X").PadLeft(2, '0'),
                fByte2.ToString("X").PadLeft(2, '0'), fByte3.ToString("X").PadLeft(2, '0'),
                fByte4.ToString("X").PadLeft(2, '0'),
                fByte5.ToString("X").PadLeft(2, '0'));
            commandText += string.Format("&amask={0}{1}{2}{3}", aByte1.ToString("X").PadLeft(2, '0'),
                aByte2.ToString("X").PadLeft(2, '0'), aByte3.ToString("X").PadLeft(2, '0'),
                aByte4.ToString("X").PadLeft(2, '0'));
        }

        /*public void Init(IHash fileData, bool force)
		{
			int fByte1 = 124; // fmask - byte1 (old 120 Added other episodes)
			int fByte2 = 248; // old 72 fmask - byte2 (Added FileSize, SHA1, MD5)
			int fByte3 = 255; // fmask - byte3
			int fByte4 = 249; // fmask - byte4

			int aByte1 = 0; // amask - byte1
			int aByte2 = 0; // amask - byte2
			int aByte3 = 252; // amask - byte3 old 236 Added Kanji name
			int aByte4 = 192; // amask - byte4

			this.fileData = fileData;
			this.forceRefresh = force;

			commandID = fileData.Info;
			// 220 FILE572794|6107|99294|2723|c646d82a184a33f4e4f98af39f29a044|8452c4bf|high|HDTV|Vorbis (Ogg Vorbis)|148|H264/AVC|1773|1280x720|mkv|1470||1239494400|2|The Day It Began|Hajimari no Hi|712|14|Eclipse Productions|Eclipse



			commandText = "FILE size=" + fileData.FileSize.ToString();
			commandText += "&ed2k=" + fileData.ED2KHash;
			commandText += string.Format("&fmask={0}{1}{2}{3}", fByte1.ToString("X").PadLeft(2, '0'),
							fByte2.ToString("X").PadLeft(2, '0'), fByte3.ToString("X").PadLeft(2, '0'), fByte4.ToString("X").PadLeft(2, '0'));
			commandText += string.Format("&amask={0}{1}{2}{3}", aByte1.ToString("X").PadLeft(2, '0'),
				aByte2.ToString("X").PadLeft(2, '0'), aByte3.ToString("X").PadLeft(2, '0'), aByte4.ToString("X").PadLeft(2, '0'));

		}*/
    }
}