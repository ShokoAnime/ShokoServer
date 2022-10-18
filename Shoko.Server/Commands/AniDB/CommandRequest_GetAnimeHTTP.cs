using System;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Commands.Import;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetAnimeHTTP)]
public class CommandRequest_GetAnimeHTTP : CommandRequestImplementation
{
    private readonly IHttpConnectionHandler _handler;
    private readonly HttpAnimeParser _parser;
    private readonly AnimeCreator _animeCreator;
    private readonly HttpXmlUtils _xmlUtils;
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;

    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }
    public bool CacheOnly { get; set; }
    public bool DownloadRelations { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting anime info from HTTP API: {0}",
        queueState = QueueStateEnum.AnimeInfo,
        extraParams = new[] { AnimeID.ToString() }
    };

    public int RelDepth { get; set; }

    public bool CreateSeriesEntry { get; set; }

    [XmlIgnore] public SVR_AniDB_Anime Result { get; set; }

    public override void PostInit()
    {
        if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null)
        {
            Priority = (int)CommandRequestPriority.Priority1;
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_GetAnimeHTTP: {AnimeID}", AnimeID);

        try
        {
            if (_handler.IsBanned)
            {
                throw new AniDBBannedException
                {
                    BanType = UpdateType.HTTPBan,
                    BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
                };
            }

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
            var skip = true;
            var animeRecentlyUpdated = false;
            if (anime != null && update != null)
            {
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < ServerSettings.Instance.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    animeRecentlyUpdated = true;
                }
            }

            if (!animeRecentlyUpdated && !CacheOnly)
            {
                if (ForceRefresh)
                {
                    skip = false;
                }
                else if (anime == null)
                {
                    skip = false;
                }
            }

            ResponseGetAnime response = null;
            if (skip)
            {
                var xml = _xmlUtils.LoadAnimeHTTPFromFile(AnimeID);
                if (xml != null)
                {
                    response = _parser.Parse(AnimeID, xml);
                }
            }
            else
            {
                var request = _requestFactory.Create<RequestGetAnime>(r => r.AnimeID = AnimeID);
                var httpResponse = request.Execute();
                response = httpResponse.Response;
            }

            if (response == null)
            {
                Logger.LogError("No such anime with ID: {AnimeID}", AnimeID);
                return;
            }

            anime ??= new SVR_AniDB_Anime();
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var sessionWrapper = session.Wrap();
            _animeCreator.CreateAnime(session, response, anime, 0);

            var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
            // conditionally create AnimeSeries if it doesn't exist
            if (series == null && CreateSeriesEntry)
            {
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

            ProcessRelations(session, response, _requestFactory, _handler, _animeCreator);

            // Request an image download
            _commandFactory.Create<CommandRequest_DownloadAniDBImages>(c => c.AnimeID = anime.AnimeID).Save();
        }
        catch (AniDBBannedException ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_GetAnimeHTTP: {AnimeID} - {Ex}", AnimeID, ex);
        }
    }

    private void ProcessRelations(ISession session, ResponseGetAnime response, IRequestFactory requestFactory,
        IHttpConnectionHandler handler, AnimeCreator animeCreator)
    {
        if (!DownloadRelations)
        {
            return;
        }

        if (ServerSettings.Instance.AniDb.MaxRelationDepth <= 0)
        {
            return;
        }

        if (!ServerSettings.Instance.AutoGroupSeries && !ServerSettings.Instance.AniDb.DownloadRelatedAnime)
        {
            return;
        }

        // this command is RelDepth, so any further relations are +1
        ProcessRelationsRecursive(session, response, requestFactory, handler, animeCreator, RelDepth + 1);
    }

    private void ProcessRelationsRecursive(ISession session, ResponseGetAnime response, IRequestFactory requestFactory,
        IHttpConnectionHandler handler, AnimeCreator animeCreator, int depth)
    {
        if (depth > ServerSettings.Instance.AniDb.MaxRelationDepth)
        {
            return;
        }

        foreach (var relation in response.Relations)
        {
            var relatedAnime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(relation.RelatedAnimeID);

            var animeRecentlyUpdated = false;
            if (relatedAnime != null && update != null)
            {
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < ServerSettings.Instance.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    animeRecentlyUpdated = true;
                }
            }

            var download = !animeRecentlyUpdated && !CacheOnly;

            // we only want to pull right now if we are grouping, and not if it was recently or banned
            if (download && ServerSettings.Instance.AutoGroupSeries && !handler.IsBanned)
            {
                try
                {
                    var relationRequest =
                        requestFactory.Create<RequestGetAnime, ResponseGetAnime>(r =>
                            r.AnimeID = relation.RelatedAnimeID);
                    var relationResponse = relationRequest.Execute();
                    relatedAnime ??= new SVR_AniDB_Anime();
                    animeCreator.CreateAnime(session, relationResponse.Response, relatedAnime, depth);
                    // we just downloaded depth, so the next recursion is depth + 1
                    if (depth + 1 > ServerSettings.Instance.AniDb.MaxRelationDepth)
                    {
                        return;
                    }

                    ProcessRelationsRecursive(session, relationResponse.Response, requestFactory, handler, animeCreator,
                        depth + 1);
                    continue;
                }
                catch (AniDBBannedException)
                {
                    // pass to allow making command requests
                }
            }

            // here, we either didn't do the above, or it was stopped by a ban. Either way, we haven't downloaded depth, so queue that
            if (RepoFactory.CommandRequest.GetByCommandID(session, GetCommandID(relation.RelatedAnimeID)) != null)
            {
                continue;
            }

            var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                c =>
                {
                    c.AnimeID = relation.RelatedAnimeID;
                    c.DownloadRelations = true;
                    c.RelDepth = depth;
                }
            );
            command.Save();
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = GetCommandID(AnimeID);
    }

    private static string GetCommandID(int animeID)
    {
        return $"CommandRequest_GetAnimeHTTP_{animeID}";
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
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            AnimeID = int.Parse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(AnimeID)));
            if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null)
            {
                Priority = (int)CommandRequestPriority.Priority1;
            }

            if (bool.TryParse(
                    TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(DownloadRelations)),
                    out var dlRelations))
            {
                DownloadRelations = dlRelations;
            }

            if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(ForceRefresh)),
                    out var force))
            {
                ForceRefresh = force;
            }

            if (int.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(RelDepth)),
                    out var depth))
            {
                RelDepth = depth;
            }

            if (bool.TryParse(
                    TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(CreateSeriesEntry)),
                    out var create))
            {
                CreateSeriesEntry = create;
            }

            if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(CacheOnly)),
                    out var cache))
            {
                CacheOnly = cache;
            }
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

    public CommandRequest_GetAnimeHTTP(ILoggerFactory loggerFactory, IHttpConnectionHandler handler,
        HttpAnimeParser parser, AnimeCreator animeCreator, HttpXmlUtils xmlUtils, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory) : base(loggerFactory)
    {
        _handler = handler;
        _parser = parser;
        _animeCreator = animeCreator;
        _xmlUtils = xmlUtils;
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
    }

    protected CommandRequest_GetAnimeHTTP()
    {
    }
}
