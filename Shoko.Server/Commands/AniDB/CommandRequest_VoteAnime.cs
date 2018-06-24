using System;
using System.Globalization;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_VoteAnime)]
    public class CommandRequest_VoteAnime : CommandRequestImplementation
    {
        public virtual int AnimeID { get; set; }
        public virtual int VoteType { get; set; }
        public virtual decimal VoteValue { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.VoteAnime,
            extraParams = new[] {AnimeID.ToString(), VoteValue.ToString()}
        };

        public CommandRequest_VoteAnime()
        {
        }

        public CommandRequest_VoteAnime(int animeID, int voteType, decimal voteValue)
        {
            AnimeID = animeID;
            VoteType = voteType;
            VoteValue = voteValue;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_Vote: {0}", CommandID);


            try
            {
                ShokoService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (AniDBVoteType) VoteType);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Vote: {0} - {1}", CommandID, ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Vote_{AnimeID}_{VoteType}_{VoteValue}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "AnimeID"));
                VoteType = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteType"));
                VoteValue = decimal.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteValue"),
                    style, culture);
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
