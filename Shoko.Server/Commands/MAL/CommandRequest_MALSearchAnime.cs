using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.MAL
{
    [Serializable]
    [Command(CommandRequestType.MAL_SearchAnime)]
    public class CommandRequest_MALSearchAnime : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SearchMal,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_MALSearchAnime()
        {
        }

        public CommandRequest_MALSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
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
                        Azure_CrossRef_AniDB_MAL crossRef = AzureWebAPI.Get_CrossRefAniDBMAL(AnimeID);
                        if (crossRef != null)
                        {
                            logger.Trace("Found MAL match on web cache for {0} - id = {1} ({2}/{3})", AnimeID,
                                crossRef.MALID,
                                crossRef.StartEpisodeType, crossRef.StartEpisodeNumber);
                            MALHelper.LinkAniDBMAL(AnimeID, crossRef.MALID, crossRef.MALTitle,
                                crossRef.StartEpisodeType,
                                crossRef.StartEpisodeNumber, true);

                            return;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (!ServerSettings.MAL_AutoLink) return;

                string searchCriteria = string.Empty;
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                searchCriteria = anime.MainTitle;

                // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                anime malResults = MALHelper.SearchAnimesByTitle(searchCriteria);

                if (malResults.entry.Length == 1)
                {
                    logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria,
                        malResults.entry[0].id,
                        malResults.entry[0].title);
                    MALHelper.LinkAniDBMAL(AnimeID, malResults.entry[0].id, malResults.entry[0].title,
                        (int) EpisodeType.Episode, 1,
                        false);
                }
                else if (malResults.entry.Length == 0)
                    logger.Trace("ZERO MAL search result results for: {0}", searchCriteria);
                else
                {
                    // if the title's match exactly and they have the same amount of episodes, we will use it
                    foreach (animeEntry res in malResults.entry)
                    {
                        if (res.title.Equals(anime.MainTitle, StringComparison.InvariantCultureIgnoreCase) &&
                            res.episodes == anime.EpisodeCountNormal)
                        {
                            logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria,
                                res.id,
                                res.title);
                            MALHelper.LinkAniDBMAL(AnimeID, res.id, res.title, (int) EpisodeType.Episode, 1, false);
                        }
                    }
                    logger.Trace("Too many MAL search result results for, skipping: {0}", searchCriteria);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALSearchAnime: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_MALSearchAnime{AnimeID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MALSearchAnime", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_MALSearchAnime", "ForceRefresh"));
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