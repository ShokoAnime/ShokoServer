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
    private readonly IServerSettings _settings;

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
            if (ForceRefresh && _handler.IsBanned)
            {
                Logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}.", AnimeID);
                throw new AniDBBannedException
                {
                    BanType = UpdateType.HTTPBan,
                    BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
                };
            }

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
            var animeRecentlyUpdated = false;
            if (anime != null && update != null)
            {
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    animeRecentlyUpdated = true;
                }
            }

            // If we're not only using the cache, the anime was not recently
            // updated, we're not http banned, and the user requested a forced
            // online refresh _or_ if there is no local anime record, then try
            // to fetch a new updated record online but fallback to loading from
            // the cache unless we request a forced online refresh.
            ResponseGetAnime response = null;
            if (!CacheOnly && !animeRecentlyUpdated && !_handler.IsBanned && (ForceRefresh || anime == null))
            {
                try
                {
                    var request = _requestFactory.Create<RequestGetAnime>(r => r.AnimeID = AnimeID);
                    var httpResponse = request.Execute();
                    response = httpResponse.Response;
                    if (response == null)
                    {
                        Logger.LogError("No such anime with ID: {AnimeID}", AnimeID);
                        return;
                    }
                }
                catch (AniDBBannedException)
                {
                    // Don't even try to load from the cache if we requested a
                    // forced online refresh.
                    if (anime != null)
                    {
                        Logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}.", AnimeID);
                        throw;
                    }

                    // If the anime record doesn't exist yet then try to load it
                    // from the cache. A stall record is better than no record
                    // in most cases.
                    var xml = _xmlUtils.LoadAnimeHTTPFromFile(AnimeID);
                    if (xml == null)
                    {
                        Logger.LogDebug("We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file.", AnimeID);
                        // Queue the command to get the data when we're no longer banned if there is no anime record.
                        var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                            c =>
                            {
                                c.AnimeID = AnimeID;
                                c.DownloadRelations = DownloadRelations;
                                c.RelDepth = RelDepth;
                                c.CacheOnly = false;
                                c.ForceRefresh = true;
                                c.CreateSeriesEntry = CreateSeriesEntry;
                            }
                        );
                        command.Save();
                        throw;
                    }

                    try
                    {
                        response = _parser.Parse(AnimeID, xml);
                    }
                    catch
                    {
                        Logger.LogDebug("Failed to parse the cached AnimeDoc_{AnimeID}.xml file.", AnimeID);
                        // Queue the command to get the data when we're no longer banned if there is no anime record.
                        var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                            c =>
                            {
                                c.AnimeID = AnimeID;
                                c.DownloadRelations = DownloadRelations;
                                c.RelDepth = RelDepth;
                                c.CacheOnly = false;
                                c.ForceRefresh = true;
                                c.CreateSeriesEntry = CreateSeriesEntry;
                            }
                        );
                        command.Save();
                        throw;
                    }

                    Logger.LogDebug("We're HTTP banned but were able to load the cached AnimeDoc_{AnimeID}.xml file from the cache.", AnimeID);
                }
            }
            // Else, try to load a cached xml file.
            else
            {
                var xml = _xmlUtils.LoadAnimeHTTPFromFile(AnimeID);
                if (xml == null)
                {
                    var sayWeAreBanned = !CacheOnly && _handler.IsBanned;
                    Logger.LogDebug(
                        sayWeAreBanned ?
                            "We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file." :
                            "Unable to find a cached AnimeDoc_{AnimeID}.xml file.",
                        AnimeID
                    );
                    if (!CacheOnly)
                    {
                        // Queue the command to get the data when we're no longer banned if there is no anime record.
                        var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                            c =>
                            {
                                c.AnimeID = AnimeID;
                                c.DownloadRelations = DownloadRelations;
                                c.RelDepth = RelDepth;
                                c.CacheOnly = false;
                                c.ForceRefresh = true;
                                c.CreateSeriesEntry = CreateSeriesEntry;
                            }
                        );
                        command.Save();
                    }
                    return;
                }

                try
                {
                    response = _parser.Parse(AnimeID, xml);
                }
                catch
                {
                    Logger.LogDebug("Failed to parse the cached AnimeDoc_{AnimeID}.xml file.", AnimeID);
                    if (!CacheOnly)
                    {
                        // Queue the command to get the data when we're no longer banned if there is no anime record.
                        var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                            c =>
                            {
                                c.AnimeID = AnimeID;
                                c.DownloadRelations = DownloadRelations;
                                c.RelDepth = RelDepth;
                                c.CacheOnly = false;
                                c.ForceRefresh = true;
                                c.CreateSeriesEntry = CreateSeriesEntry;
                            }
                        );
                        command.Save();
                    }
                    throw;
                }
            }

            // Create or update the anime record,
            anime ??= new SVR_AniDB_Anime();
            _animeCreator.CreateAnime(response, anime, 0);

            // then conditionally create the series record if it doesn't exist,
            var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
            if (series == null && CreateSeriesEntry)
            {
                series = anime.CreateAnimeSeriesAndGroup();
            }

            // and then create or update the episode records if we have an
            // existing series record.
            if (series != null)
            {
                series.CreateAnimeEpisodes(anime);
                RepoFactory.AnimeSeries.Save(series, true, false);
            }

            SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);

            Result = anime;

            ProcessRelations(response);

            // Request an image download
            _commandFactory.Create<CommandRequest_DownloadAniDBImages>(c => c.AnimeID = anime.AnimeID).Save();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_GetAnimeHTTP: {AnimeID}", AnimeID);
        }
    }

    private void ProcessRelations(ResponseGetAnime response)
    {
        if (!DownloadRelations)
        {
            return;
        }

        if (_settings.AniDb.MaxRelationDepth <= 0)
        {
            return;
        }

        if (RelDepth > _settings.AniDb.MaxRelationDepth)
        {
            return;
        }

        if (!_settings.AutoGroupSeries && !_settings.AniDb.DownloadRelatedAnime)
        {
            return;
        }

        // Queue or process the related series.
        foreach (var relation in response.Relations)
        {
            // Skip queuing/processing the command if it is already queued.
            if (RepoFactory.CommandRequest.GetByCommandID(GetCommandID(relation.RelatedAnimeID)) != null)
                continue;

            // Skip queuing/processing the command if the anime record were
            // recently updated.
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
            if (anime != null)
            {
                // Check when the anime was last updated online if we are
                // forcing a refresh and we're not banned, otherwise check when
                // the local anime record was last updated (be it from a fresh
                // online xml file or from a cached xml file).
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(relation.RelatedAnimeID);
                var updatedAt = ForceRefresh && !_handler.IsBanned && update != null ? update.UpdatedAt : anime.DateTimeUpdated;
                var ts = DateTime.Now - updatedAt;
                if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    continue;
                }
            }

            try
            {
                // Append the command to the queue.
                var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                    c =>
                    {
                        c.AnimeID = relation.RelatedAnimeID;
                        c.DownloadRelations = true;
                        c.RelDepth = RelDepth + 1;
                        c.CacheOnly = CacheOnly;
                        c.ForceRefresh = ForceRefresh;
                        c.CreateSeriesEntry = CreateSeriesEntry && _settings.AniDb.AutomaticallyImportSeries;
                    }
                );
                command.Save();
            }
            catch (AniDBBannedException)
            {
                // Catch banned exceptions if we run the command in-place.
                continue;
            }
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
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(AnimeID)));
        if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null) Priority = (int)CommandRequestPriority.Priority1;

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
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _handler = handler;
        _parser = parser;
        _animeCreator = animeCreator;
        _xmlUtils = xmlUtils;
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected CommandRequest_GetAnimeHTTP()
    {
    }
}
