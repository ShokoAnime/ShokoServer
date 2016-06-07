using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.AniDB_API.Commands;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_SyncMyVotes : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_SyncMyVotes()
        {
            CommandType = (int)CommandRequestType.AniDB_SyncVotes;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority6; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Actions_SyncVotes);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_SyncMyVotes");

            try
            {
                var repVotes = new AniDB_VoteRepository();

                var cmd = new AniDBHTTPCommand_GetVotes();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                var ev = cmd.Process();
                if (ev == enHelperActivityType.GotVotesHTTP)
                {
                    foreach (var myVote in cmd.MyVotes)
                    {
                        var dbVotes = repVotes.GetByEntity(myVote.EntityID);
                        AniDB_Vote thisVote = null;
                        foreach (var dbVote in dbVotes)
                        {
                            // we can only have anime permanent or anime temp but not both
                            if (myVote.VoteType == enAniDBVoteType.Anime || myVote.VoteType == enAniDBVoteType.AnimeTemp)
                            {
                                if (dbVote.VoteType == (int)enAniDBVoteType.Anime ||
                                    dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
                                {
                                    thisVote = dbVote;
                                }
                            }
                            else
                            {
                                thisVote = dbVote;
                            }
                        }

                        if (thisVote == null)
                        {
                            thisVote = new AniDB_Vote();
                            thisVote.EntityID = myVote.EntityID;
                        }
                        thisVote.VoteType = (int)myVote.VoteType;
                        thisVote.VoteValue = myVote.VoteValue;

                        repVotes.Save(thisVote);

                        if (myVote.VoteType == enAniDBVoteType.Anime || myVote.VoteType == enAniDBVoteType.AnimeTemp)
                        {
                            // download the anime info if the user doesn't already have it
                            var cmdAnime = new CommandRequest_GetAnimeHTTP(thisVote.EntityID, false, false);
                            cmdAnime.Save();
                        }
                    }

                    logger.Info("Processed Votes: {0} Items", cmd.MyVotes.Count);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_SyncMyVotes: {0} ", ex.ToString());
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_SyncMyVotes";
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}