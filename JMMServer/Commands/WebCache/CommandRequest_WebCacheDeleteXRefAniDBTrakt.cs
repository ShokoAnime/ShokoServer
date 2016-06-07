using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheDeleteXRefAniDBTrakt : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheDeleteXRefAniDBTrakt()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTrakt(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber, string traktID,
            int traktSeasonNumber, int traktStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TraktID = traktID;
            TraktSeasonNumber = traktSeasonNumber;
            TraktStartEpisodeNumber = traktStartEpisodeNumber;
            CommandType = (int)CommandRequestType.WebCache_DeleteXRefAniDBTrakt;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get { return string.Format("Deleting cross ref for Anidb to Trakt from web cache: {0}", AnimeID); }
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBTrakt(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TraktID,
                    TraktSeasonNumber, TraktStartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBTrakt: {0}" + ex, ex);
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

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "AnimeID"));
                AniDBStartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeType"));
                AniDBStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeNumber"));
                TraktID = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "TraktID");
                TraktSeasonNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktSeasonNumber"));
                TraktStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktStartEpisodeNumber"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBTrakt{0}", AnimeID);
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