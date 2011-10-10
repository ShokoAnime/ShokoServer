using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace AniDBAPI.Commands
{
	public class AniDBCommand_DeleteFile : AniDBUDPCommand, IAniDBUDPCommand
	{
		public string Hash = "";
		public long FileSize = 0;

		public string GetKey()
		{
			return "AniDBCommand_DeleteFile" + Hash;
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.DeletingFile;
		}

		public virtual enHelperActivityType Process(ref Socket soUDP,
			ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
		{
			ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

			// handle 555 BANNED and 598 - UNKNOWN COMMAND
			if (ResponseCode == 598) return enHelperActivityType.UnknownCommand;
			if (ResponseCode == 555) return enHelperActivityType.Banned;

			if (errorOccurred) return enHelperActivityType.NoSuchFile;

			string sMsgType = socketResponse.Substring(0, 3);
			switch (sMsgType)
			{
				case "211":
					return enHelperActivityType.FileDeleted;
				case "411":
					return enHelperActivityType.NoSuchFile;
				case "502":
					return enHelperActivityType.LoginFailed;
				case "501":
					return enHelperActivityType.LoginRequired;

			}

			return enHelperActivityType.NoSuchFile;
		}

		public AniDBCommand_DeleteFile()
		{
			commandType = enAniDBCommandType.DeleteFile;
		}

		public void Init(string hash, long fileSize)
		{
			Hash = hash;
			FileSize = fileSize;

			commandID = "Deleting File: " + Hash;

			commandText = "MYLISTDEL size=" + FileSize.ToString();
			commandText += "&ed2k=" + Hash;
		}
	}
}
