using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Commands.MAL;
using JMMServer.Entities;
using JMMServer.Properties;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_VoteAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_VoteAnime()
        {
        }

        public CommandRequest_VoteAnime(int animeID, int voteType, decimal voteValue)
        {
            AnimeID = animeID;
            VoteType = voteType;
            VoteValue = voteValue;
            CommandType = (int)CommandRequestType.AniDB_VoteAnime;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public int VoteType { get; set; }
        public decimal VoteValue { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_VoteAnime, AnimeID, VoteValue);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_Vote: {0}", CommandID);


            try
            {
                JMMService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (enAniDBVoteType)VoteType);

                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    var cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(AnimeID);
                    cmdMAL.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Vote: {0} - {1}", CommandID, ex.ToString());
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

            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "AnimeID"));
                VoteType = int.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteType"));
                VoteValue = decimal.Parse(TryGetProperty(docCreator, "CommandRequest_VoteAnime", "VoteValue"), style,
                    culture);
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_Vote_{0}_{1}_{2}", AnimeID, VoteType, VoteValue);
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