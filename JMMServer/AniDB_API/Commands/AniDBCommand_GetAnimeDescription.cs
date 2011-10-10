using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_GetAnimeDescription : AniDBUDPCommand, IAniDBUDPCommand
	{
		private int animeID;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		public string GetKey()
		{
			return "AniDBCommand_GetAnimeDescription" + AnimeID.ToString();
		}

		private Raw_AniDB_AnimeDesc animeDesc;
		public Raw_AniDB_AnimeDesc AnimeDesc
		{
			get { return animeDesc; }
			set { animeDesc = value; }
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingAnimeDesc;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{
			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand;
			if (ResponseCode == 555) return enHelperActivityType.Banned;

			if (errorOccurred) return enHelperActivityType.NoSuchAnime;

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Response: {0}", socketResponse);

			// Process Response
			string sMsgType = socketResponse.Substring(0, 3);


			switch (sMsgType)
			{
				case "233":
					{
						// 233 ANIMEDESC
						// the first 11 characters should be "240 EPISODE"
						// the rest of the information should be the data list

						animeDesc = new Raw_AniDB_AnimeDesc(socketResponse);
						return enHelperActivityType.GotAnimeDesc;

					}
				case "330":
					{
						return enHelperActivityType.NoSuchAnime;
					}
				case "333": // no such description
					{
						return enHelperActivityType.NoSuchAnime;
					}
				case "501":
					{
						return enHelperActivityType.LoginRequired;
					}
			}

			return enHelperActivityType.FileDoesNotExist;

		}

		public AniDBCommand_GetAnimeDescription()
		{
			commandType = enAniDBCommandType.GetAnimeDescription;
		}

		public void Init(int animeID)
		{
			this.animeID = animeID;
			commandText = "ANIMEDESC aid=" + animeID.ToString();
			commandText += "&part=0";

			//BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Request: {0}", commandText);

			commandID = animeID.ToString();
		}
	}
}
