using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_GetMyListFileInfo : AniDBUDPCommand, IAniDBUDPCommand
	{
		private int fileID = 0;
		public int FileID
		{
			get { return fileID; }
			set { fileID = value; }
		}

		public string GetKey()
		{
			return "AniDBCommand_GetMyListFileInfo" + FileID.ToString();
		}

		private Raw_AniDB_MyListFile myListFile = null;
		public Raw_AniDB_MyListFile MyListFile
		{
			get { return myListFile; }
			set { myListFile = value; }
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingMyListFileInfo;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{
			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
			if (ResponseCode == 555) return enHelperActivityType.Banned_555;

			if (errorOccurred) return enHelperActivityType.NoSuchMyListFile;

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Response: {0}", socketResponse);

			// Process Response
			string sMsgType = socketResponse.Substring(0, 3);


			switch (sMsgType)
			{
				case "221":
					{
						myListFile = new Raw_AniDB_MyListFile(socketResponse);
						//BaseConfig.MyAnimeLog.Write(myListFile.ToString());
						return enHelperActivityType.GotMyListFileInfo;

					}
				case "321":
					{
						return enHelperActivityType.NoSuchMyListFile;
					}
				case "501":
					{
						return enHelperActivityType.LoginRequired;
					}
			}

			return enHelperActivityType.NoSuchMyListFile;

		}

		public AniDBCommand_GetMyListFileInfo()
		{
			commandType = enAniDBCommandType.GetMyListFileInfo;
		}

		public void Init(int fileId)
		{
			this.fileID = fileId;
			commandText = "MYLIST fid=" + fileID.ToString();

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Request: {0}", commandText);

			commandID = fileID.ToString();
		}
	}
}
