using System.Collections.Generic;
using AniDBAPI;
using AniDBAPI.Commands;
using JMMServer.AniDB_API.Raws;

namespace JMMServer.AniDB_API.Commands
{
    public class AniDBHTTPCommand_GetVotes : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private string xmlResult = "";

        public AniDBHTTPCommand_GetVotes()
        {
            commandType = enAniDBCommandType.GetVotesHTTP;
        }

        public List<Raw_AniDB_Vote_HTTP> MyVotes { get; set; } = new List<Raw_AniDB_Vote_HTTP>();

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

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
            var docAnime = AniDBHTTPHelper.GetVotesXMLFromAPI(Username, Password, ref xmlResult);

            if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

            //APIUtils.WriteToLog("AniDBHTTPCommand_GetFullAnime: " + xmlResult);
            if (docAnime != null)
            {
                MyVotes = AniDBHTTPHelper.ProcessVotes(docAnime);
                return enHelperActivityType.GotVotesHTTP;
            }
            return enHelperActivityType.NoSuchAnime;
        }

        public void Init(string uname, string pword)
        {
            Username = uname;
            Password = pword;
            commandID = "VOTES";
        }
    }
}