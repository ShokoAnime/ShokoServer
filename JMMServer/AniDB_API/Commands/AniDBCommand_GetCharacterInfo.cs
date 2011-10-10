using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_GetCharacterInfo : AniDBUDPCommand, IAniDBUDPCommand
	{
		private int charID = 0;
		public int CharID
		{
			get { return charID; }
			set { charID = value; }
		}

		private Raw_AniDB_Character charInfo = null;
		public Raw_AniDB_Character CharInfo
		{
			get { return charInfo; }
			set { charInfo = value; }
		}

		private bool forceRefresh = false;
		public bool ForceRefresh
		{
			get { return forceRefresh; }
			set { forceRefresh = value; }
		}

		public string GetKey()
		{
			return "AniDBCommand_GetCharacterInfo" + CharID.ToString();
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingCharInfo;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{
			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand;
			if (ResponseCode == 555) return enHelperActivityType.Banned;

			if (errorOccurred) return enHelperActivityType.NoSuchChar;

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCharacterInfo.Process: Response: {0}", socketResponse);

			// Process Response
			string sMsgType = socketResponse.Substring(0, 3);


			switch (sMsgType)
			{
				case "235":
					{
						// 235 CHARACTER INFO
						// the first 11 characters should be "235 CHARACTER"
						// the rest of the information should be the data list

						charInfo = new Raw_AniDB_Character(socketResponse);
						return enHelperActivityType.GotCharInfo;


						// Response: 235 CHARACTER 99297|6267|25|539|5|01|The Girl Returns|Shoujo Kikan|????|1238976000
					}
				case "335":
					{
						return enHelperActivityType.NoSuchChar;
					}
				case "501":
					{
						return enHelperActivityType.LoginRequired;
					}
			}

			return enHelperActivityType.NoSuchChar;

		}



		public AniDBCommand_GetCharacterInfo()
		{
			commandType = enAniDBCommandType.GetCharInfo;
		}

		public void Init(int charID, bool force)
		{

			this.charID = charID;
			this.forceRefresh = force;
			commandText = "CHARACTER charid=" + charID.ToString();

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCharacterInfo.Process: Request: {0}", commandText);

			commandID = charID.ToString();
		}
	}
}
