using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheDeleteXRefAniDBTvDB : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheDeleteXRefAniDBTvDB()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTvDB(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber, int tvDBID,
            int tvDBSeasonNumber, int tvDBStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TvDBID = tvDBID;
            TvDBSeasonNumber = tvDBSeasonNumber;
            TvDBStartEpisodeNumber = tvDBStartEpisodeNumber;
            CommandType = (int)CommandRequestType.WebCache_DeleteXRefAniDBTvDB;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get { return string.Format("Deleting cross ref for Anidb to TvDB from web cache: {0}", AnimeID); }
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBTvDB(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TvDBID,
                    TvDBSeasonNumber, TvDBStartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDB: {0}" + ex, ex);
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            try
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
                    AnimeID =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB", "AnimeID"));
                    AniDBStartEpisodeType =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "AniDBStartEpisodeType"));
                    AniDBStartEpisodeNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "AniDBStartEpisodeNumber"));
                    TvDBID =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB", "TvDBID"));
                    TvDBSeasonNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "TvDBSeasonNumber"));
                    TvDBStartEpisodeNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "TvDBStartEpisodeNumber"));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDB: {0}" + ex, ex);
                return true;
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBTvDB{0}", AnimeID);
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