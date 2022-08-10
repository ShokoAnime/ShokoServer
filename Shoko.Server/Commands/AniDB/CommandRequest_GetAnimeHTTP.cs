using System;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.AniDB_API;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetAnimeHTTP)]
    public class CommandRequest_GetAnimeHTTP : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }
        public bool CacheOnly { get; set; }
        public bool DownloadRelations { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AnimeInfo,
            extraParams = new[] {AnimeID.ToString()}
        };

        public int RelDepth { get; set; }

        public bool CreateSeriesEntry { get; set; }
        
        [XmlIgnore]
        public SVR_AniDB_Anime Result { get; set; }

        public CommandRequest_GetAnimeHTTP()
        {
        }

        public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations, bool createSeriesEntry, int relDepth = 0)
        {
            AnimeID = animeid;
            DownloadRelations = downloadRelations;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;
            if (RepoFactory.AniDB_Anime.GetByAnimeID(animeid) == null) Priority = (int) CommandRequestPriority.Priority1;
            RelDepth = relDepth;
            CreateSeriesEntry = createSeriesEntry;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_GetAnimeHTTP: {AnimeID}", AnimeID);

            try
            {
                var handler = serviceProvider.GetRequiredService<IHttpConnectionHandler>();
                var parser = serviceProvider.GetRequiredService<HttpAnimeParser>();
                var animeCreator = serviceProvider.GetRequiredService<AnimeCreator>();
                var xmlUtils = serviceProvider.GetRequiredService<HttpXmlUtils>();

                if (handler.IsBanned) throw new AniDBBannedException { BanType = UpdateType.HTTPBan, BanExpires = handler.BanTime?.AddHours(handler.BanTimerResetLength) };

                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var sessionWrapper = session.Wrap();

                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
                var skip = true;
                var animeRecentlyUpdated = false;
                if (anime != null && update != null)
                {
                    var ts = DateTime.Now - update.UpdatedAt;
                    if (ts.TotalHours < ServerSettings.Instance.AniDb.MinimumHoursToRedownloadAnimeInfo) animeRecentlyUpdated = true;
                }
                if (!animeRecentlyUpdated && !CacheOnly)
                {
                    if (ForceRefresh)
                        skip = false;
                    else if (anime == null) skip = false;
                }

                ResponseGetAnime response = null;
                if (skip)
                {
                    var xml = xmlUtils.LoadAnimeHTTPFromFile(AnimeID);
                    if (xml != null) response = parser.Parse(AnimeID, xml);
                }
                else
                {
                    var request = new RequestGetAnime { AnimeID = AnimeID };
                    var httpResponse = request.Execute(handler);
                    response = httpResponse.Response;
                }

                if (response == null)
                {
                    Logger.LogError("No such anime with ID: {AnimeID}", AnimeID);
                    return;
                }

                anime ??= new SVR_AniDB_Anime();
                animeCreator.CreateAnime(session, response, anime, 0);

                var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
                // conditionally create AnimeSeries if it doesn't exist
                if (series == null && CreateSeriesEntry) {
                    series = anime.CreateAnimeSeriesAndGroup(sessionWrapper);
                }

                // create AnimeEpisode records for all episodes in this anime only if we have a series
                if (series != null)
                {
                    series.CreateAnimeEpisodes(session, anime);
                    RepoFactory.AnimeSeries.Save(series, true, false);
                }

                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);

                Result = anime;

                ProcessRelations(session, response, handler, animeCreator);

                // Request an image download
            }
            catch (AniDBBannedException ex)
            {
                Logger.LogError(ex, "Error processing CommandRequest_GetAnimeHTTP: {AnimeID} - {Ex}", AnimeID, ex);
            }
        }

        private void ProcessRelations(ISession session, ResponseGetAnime response, IHttpConnectionHandler handler, AnimeCreator animeCreator)
        {
            if (!DownloadRelations) return;
            if (ServerSettings.Instance.AniDb.MaxRelationDepth <= 0) return;
            if (!ServerSettings.Instance.AutoGroupSeries && !ServerSettings.Instance.AniDb.DownloadRelatedAnime) return;
            // this command is RelDepth, so any further relations are +1
            ProcessRelationsRecursive(session, response, handler, animeCreator, RelDepth + 1);
        }

        private void ProcessRelationsRecursive(ISession session, ResponseGetAnime response, IHttpConnectionHandler handler, AnimeCreator animeCreator, int depth)
        {
            if (depth > ServerSettings.Instance.AniDb.MaxRelationDepth) return;
            foreach (var relation in response.Relations)
            {
                var relatedAnime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(relation.RelatedAnimeID);
                
                var animeRecentlyUpdated = false;
                if (relatedAnime != null && update != null)
                {
                    var ts = DateTime.Now - update.UpdatedAt;
                    if (ts.TotalHours < ServerSettings.Instance.AniDb.MinimumHoursToRedownloadAnimeInfo) animeRecentlyUpdated = true;
                }

                var download = !animeRecentlyUpdated && !CacheOnly;

                // we only want to pull right now if we are grouping, and not if it was recently or banned
                if (download && ServerSettings.Instance.AutoGroupSeries && !handler.IsBanned)
                {
                    try
                    {
                        var relationRequest = new RequestGetAnime { AnimeID = relation.RelatedAnimeID };
                        var relationResponse = relationRequest.Execute(handler);
                        relatedAnime ??= new SVR_AniDB_Anime();
                        animeCreator.CreateAnime(session, relationResponse.Response, relatedAnime, depth);
                        // we just downloaded depth, so the next recursion is depth + 1
                        if (depth + 1 > ServerSettings.Instance.AniDb.MaxRelationDepth) return;
                        ProcessRelationsRecursive(session, relationResponse.Response, handler, animeCreator, depth + 1);
                        continue;
                    }
                    catch (AniDBBannedException)
                    {
                        // pass to allow making command requests
                    }
                }

                // here, we either didn't do the above, or it was stopped by a ban. Either way, we haven't downloaded depth, so queue that
                if (RepoFactory.CommandRequest.GetByCommandID(session, GetCommandID(relation.RelatedAnimeID)) != null) continue;
                var command = new CommandRequest_GetAnimeHTTP { AnimeID = relation.RelatedAnimeID, DownloadRelations = true, RelDepth = depth };
                command.Save();
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = GetCommandID(AnimeID);
        }
        
        private static string GetCommandID(int animeID) => $"CommandRequest_GetAnimeHTTP_{animeID}";

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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(AnimeID)));
                if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null) Priority = (int) CommandRequestPriority.Priority1;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(DownloadRelations)), out var dlRelations))
                    DownloadRelations = dlRelations;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(ForceRefresh)), out var force))
                    ForceRefresh = force;
                if (int.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(RelDepth)), out var depth))
                    RelDepth = depth;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(CreateSeriesEntry)), out var create))
                    CreateSeriesEntry = create;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(CacheOnly)), out var cache))
                    CacheOnly = cache;
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
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