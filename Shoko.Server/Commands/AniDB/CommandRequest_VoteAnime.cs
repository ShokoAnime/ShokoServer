using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Models.Server;
using AniDBAPI;
using Shoko.Models.Queue;
using Shoko.Server.Commands.MAL;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_VoteAnime : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public int VoteType { get; set; }
        public decimal VoteValue { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.VoteAnime,
                    extraParams = new string[] {AnimeID.ToString(), VoteValue.ToString()}
                };
            }
        }

        public CommandRequest_VoteAnime()
        {
        }

        public CommandRequest_VoteAnime(int animeID, int voteType, decimal voteValue)
        {
            this.AnimeID = animeID;
            this.VoteType = voteType;
            this.VoteValue = voteValue;
            this.CommandType = (int) CommandRequestType.AniDB_VoteAnime;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_Vote: {0}", CommandID);


            try
            {
                ShokoService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (AniDBAPI.enAniDBVoteType) VoteType);

                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(AnimeID);
                    cmdMAL.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Vote: {0} - {1}", CommandID, ex.ToString());
                return;
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_Vote_{0}_{1}_{2}", AnimeID, (int) VoteType, VoteValue);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "AnimeID"));
                this.VoteType = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteType"));
                this.VoteValue = decimal.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteValue"),
                    style, culture);
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