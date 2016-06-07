using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Repositories;

namespace JMMServer.Commands.MAL
{
    [Serializable]
    public class CommandRequest_MALSearchAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_MALSearchAnime()
        {
        }

        public CommandRequest_MALSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.MAL_SearchAnime;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SearchMal, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALSearchAnime: {0}", AnimeID);

            try
            {
                // first check if the user wants to use the web cache
                if (ServerSettings.WebCache_MAL_Get)
                {
                    try
                    {
                        var crossRef = AzureWebAPI.Get_CrossRefAniDBMAL(AnimeID);
                        if (crossRef != null)
                        {
                            logger.Trace("Found MAL match on web cache for {0} - id = {1} ({2}/{3})", AnimeID,
                                crossRef.MALID, crossRef.StartEpisodeType, crossRef.StartEpisodeNumber);
                            MALHelper.LinkAniDBMAL(AnimeID, crossRef.MALID, crossRef.MALTitle, crossRef.StartEpisodeType,
                                crossRef.StartEpisodeNumber, true);

                            return;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                var searchCriteria = "";
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                searchCriteria = anime.MainTitle;

                // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                var malResults = MALHelper.SearchAnimesByTitle(searchCriteria);

                if (malResults.entry.Length == 1)
                {
                    logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria,
                        malResults.entry[0].id, malResults.entry[0].title);
                    MALHelper.LinkAniDBMAL(AnimeID, malResults.entry[0].id, malResults.entry[0].title,
                        (int)enEpisodeType.Episode, 1, false);
                }
                else if (malResults.entry.Length == 0)
                    logger.Trace("ZERO MAL search result results for: {0}", searchCriteria);
                else
                {
                    // if the title's match exactly and they have the same amount of episodes, we will use it
                    foreach (var res in malResults.entry)
                    {
                        if (res.title.Equals(anime.MainTitle, StringComparison.InvariantCultureIgnoreCase) &&
                            res.episodes == anime.EpisodeCountNormal)
                        {
                            logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria, res.id,
                                res.title);
                            MALHelper.LinkAniDBMAL(AnimeID, res.id, res.title, (int)enEpisodeType.Episode, 1, false);
                        }
                    }
                    logger.Trace("Too many MAL search result results for, skipping: {0}", searchCriteria);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALSearchAnime: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MALSearchAnime", "AnimeID"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_MALSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_MALSearchAnime{0}", AnimeID);
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