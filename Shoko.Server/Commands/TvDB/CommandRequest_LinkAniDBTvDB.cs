using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.TvDB;

[Serializable]
[Command(CommandRequestType.LinkAniDBTvDB)]
public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation
{
    private readonly TvDBApiHelper _helper;
    public int AnimeID;
    public int TvDBID;
    public bool AdditiveLink;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating Changed TvDB association: {0}",
        queueState = QueueStateEnum.LinkAniDBTvDB,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_LinkAniDBTvDB: {0}", AnimeID);

        try
        {
            _helper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
            SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error processing CommandRequest_LinkAniDBTvDB: {0} - {1}", AnimeID,
                ex);
        }
    }

    public override void GenerateCommandID()
    {
        CommandID =
            $"CommandRequest_LinkAniDBTvDB_{AnimeID}_{TvDBID}";
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
            AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "animeID"));
            TvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvDBID"));
            AdditiveLink = bool.Parse(
                TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "additiveLink"));
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

    public CommandRequest_LinkAniDBTvDB(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_LinkAniDBTvDB()
    {
    }
}
