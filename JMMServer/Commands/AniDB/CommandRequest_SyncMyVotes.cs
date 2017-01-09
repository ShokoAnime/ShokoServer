using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.AniDB_API.Commands;
using JMMServer.AniDB_API.Raws;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using Shoko.Models.Server;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_SyncMyVotes : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority6; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.Actions_SyncVotes, extraParams = new string[0] };
            }
        }


        public CommandRequest_SyncMyVotes()
        {
            this.CommandType = (int) CommandRequestType.AniDB_SyncVotes;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_SyncMyVotes");

            try
            {

                AniDBHTTPCommand_GetVotes cmd = new AniDBHTTPCommand_GetVotes();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                enHelperActivityType ev = cmd.Process();
                if (ev == enHelperActivityType.GotVotesHTTP)
                {
                    foreach (Raw_AniDB_Vote_HTTP myVote in cmd.MyVotes)
                    {
                        List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(myVote.EntityID);
                        AniDB_Vote thisVote = null;
                        foreach (AniDB_Vote dbVote in dbVotes)
                        {
                            // we can only have anime permanent or anime temp but not both
                            if (myVote.VoteType == enAniDBVoteType.Anime || myVote.VoteType == enAniDBVoteType.AnimeTemp)
                            {
                                if (dbVote.VoteType == (int) enAniDBVoteType.Anime ||
                                    dbVote.VoteType == (int) enAniDBVoteType.AnimeTemp)
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
                        thisVote.VoteType = (int) myVote.VoteType;
                        thisVote.VoteValue = myVote.VoteValue;

                        RepoFactory.AniDB_Vote.Save(thisVote);

                        if (myVote.VoteType == enAniDBVoteType.Anime || myVote.VoteType == enAniDBVoteType.AnimeTemp)
                        {
                            // download the anime info if the user doesn't already have it
                            CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(thisVote.EntityID,
                                false, false);
                            cmdAnime.Save();
                        }
                    }

                    logger.Info("Processed Votes: {0} Items", cmd.MyVotes.Count);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_SyncMyVotes: {0} ", ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_SyncMyVotes");
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}