using System;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetAnimeHTTP_Force)]
public class CommandRequest_GetAnimeHTTP_Force : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;

    public virtual int AnimeID { get; set; }
    public virtual bool DownloadRelations { get; set; }
    public virtual int RelDepth { get; set; }
    public virtual bool CreateSeriesEntry { get; set; }

    [XmlIgnore][JsonIgnore]
    public virtual SVR_AniDB_Anime Result { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Forcefully getting anime info from HTTP API: {0}",
        queueState = QueueStateEnum.AnimeInfo,
        extraParams = new[] { AnimeID.ToString() }
    };

    public override void PostInit()
    {
        if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null)
        {
            Priority = (int)CommandRequestPriority.Priority1;
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_GetAnimeHTTP_Force: {AnimeID}", AnimeID);
        var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
            c =>
            {
                c.AnimeID = AnimeID;
                c.DownloadRelations = DownloadRelations;
                c.RelDepth = RelDepth;
                c.CacheOnly = false;
                c.ForceRefresh = true;
                c.CreateSeriesEntry = CreateSeriesEntry;
                c.BubbleExceptions = true;
            }
        );
        command.ProcessCommand();
        Result = command.Result;
    }

    public override void GenerateCommandID()
    {
        CommandID = GetCommandID(AnimeID);
    }

    private static string GetCommandID(int animeID)
    {
        return $"CommandRequest_GetAnimeHTTP_Force_{animeID}";
    }

    public override bool LoadFromCommandDetails()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP_Force), nameof(AnimeID)));
        if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null) Priority = (int)CommandRequestPriority.Priority1;

        if (bool.TryParse(
                TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP_Force), nameof(DownloadRelations)),
                out var dlRelations))
        {
            DownloadRelations = dlRelations;
        }

        if (int.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP_Force), nameof(RelDepth)),
                out var depth))
        {
            RelDepth = depth;
        }

        if (bool.TryParse(
                TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP_Force), nameof(CreateSeriesEntry)),
                out var create))
        {
            CreateSeriesEntry = create;
        }

        return true;
    }

    public CommandRequest_GetAnimeHTTP_Force(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory) : base(loggerFactory)
    {
        _commandFactory = commandFactory;
    }

    protected CommandRequest_GetAnimeHTTP_Force()
    {
    }
}
