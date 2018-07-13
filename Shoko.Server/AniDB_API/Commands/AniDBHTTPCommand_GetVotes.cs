using System.Collections.Generic;
using System.Xml;
using AniDBAPI;
using AniDBAPI.Commands;
using Shoko.Server.AniDB_API.Raws;

namespace Shoko.Server.AniDB_API.Commands
{
    public class AniDBHTTPCommand_GetVotes : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private List<Raw_AniDB_Vote_HTTP> myVotes = new List<Raw_AniDB_Vote_HTTP>();

        public List<Raw_AniDB_Vote_HTTP> MyVotes
        {
            get { return myVotes; }
            set { myVotes = value; }
        }

        private string username = string.Empty;

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        private string password = string.Empty;

        public string Password
        {
            get { return password; }
            set { password = value; }
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
            XmlDocument docAnime = AniDBHTTPHelper.GetVotesXMLFromAPI(username, password);

            //APIUtils.WriteToLog("AniDBHTTPCommand_GetFullAnime: " + xmlResult);
            if (docAnime != null)
            {
                myVotes = AniDBHTTPHelper.ProcessVotes(docAnime);
                return enHelperActivityType.GotVotesHTTP;
            }
            
            return enHelperActivityType.NoSuchAnime;
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