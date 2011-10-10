using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI.Commands;
using AniDBAPI;
using JMMServer.AniDB_API.Raws;
using System.Xml;

namespace JMMServer.AniDB_API.Commands
{
	public class AniDBHTTPCommand_GetVotes : AniDBHTTPCommand, IAniDBHTTPCommand
	{
		private List<Raw_AniDB_Vote_HTTP> myVotes = new List<Raw_AniDB_Vote_HTTP>();
		public List<Raw_AniDB_Vote_HTTP> MyVotes
		{
			get { return myVotes; }
			set { myVotes = value; }
		}

		private string username = "";
		public string Username
		{
			get { return username; }
			set { username = value; }
		}

		private string password = "";
		public string Password
		{
			get { return password; }
			set { password = value; }
		}

		private string xmlResult = "";
		public string XmlResult
		{
			get { return xmlResult; }
			set { xmlResult = value; }
		}

		public string GetKey()
		{
			return "AniDBHTTPCommand_GetVotes";
		}

		public virtual enHelperActivityType GetStartEventType()
		{
			return enHelperActivityType.GettingVotesHTTP;
		}

		public virtual enHelperActivityType Process()
		{

			XmlDocument docAnime = AniDBHTTPHelper.GetVotesXMLFromAPI(username, password, ref xmlResult);

			if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

			//APIUtils.WriteToLog("AniDBHTTPCommand_GetFullAnime: " + xmlResult);
			if (docAnime != null)
			{

				myVotes = AniDBHTTPHelper.ProcessVotes(docAnime);
				return enHelperActivityType.GotVotesHTTP;
			}
			else
			{
				return enHelperActivityType.NoSuchAnime;
			}
		}

		public AniDBHTTPCommand_GetVotes()
		{
			commandType = enAniDBCommandType.GetVotesHTTP;
		}

		public void Init(string uname, string pword)
		{
			this.username = uname;
			this.password = pword;
			commandID = "VOTES";
		}
	}
}
