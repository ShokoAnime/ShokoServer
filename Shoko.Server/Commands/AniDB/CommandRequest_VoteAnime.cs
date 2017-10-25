using System;
using System.Globalization;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.MAL;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_VoteAnime : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public int VoteType { get; set; }
        public decimal VoteValue { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
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
            CommandType = (int) CommandRequestType.AniDB_VoteAnime;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_Vote: {0}", CommandID);


            try
            {
                ShokoService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (AniDBVoteType) VoteType);

                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(AnimeID);
                    cmdMAL.Save();
                }
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

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
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