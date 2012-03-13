using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using JMMServer.AniDB_API.Raws;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_GetMyListStats : AniDBUDPCommand, IAniDBUDPCommand
	{
		
		public string GetKey()
		{
			return "AniDBCommand_GetMyListStats";
		}

		private Raw_AniDB_MyListStats myListStats = null;
		public Raw_AniDB_MyListStats MyListStats
		{
			get { return myListStats; }
			set { myListStats = value; }
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingMyListStats;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{
			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand;
			if (ResponseCode == 555) return enHelperActivityType.Banned;

			if (errorOccurred) return enHelperActivityType.NoSuchMyListFile;


			// Process Response
			string sMsgType = socketResponse.Substring(0, 3);


			switch (sMsgType)
			{
				case "222":
					{
						myListStats = new Raw_AniDB_MyListStats(socketResponse);
						return enHelperActivityType.GotMyListStats;

					}
				case "501":
					{
						return enHelperActivityType.LoginRequired;
					}
			}

			return enHelperActivityType.NoSuchMyListFile;

		}

		public AniDBCommand_GetMyListStats()
		{
			commandType = enAniDBCommandType.GetMyListStats;
		}

		public void Init()
		{
			commandText = "MYLISTSTATS ";
			commandID = "AniDBCommand_GetMyListStats";
		}
	}
}
