using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TvDB_DownloadImages)]
public class CommandRequest_TvDBDownloadImages : CommandRequestImplementation
{
    private readonly TvDBApiHelper _helper;
    public int TvDBSeriesID { get; set; }
    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting images from The TvDB: {0}",
        queueState = QueueStateEnum.DownloadTvDBImages,
        extraParams = new[] { TvDBSeriesID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TvDBDownloadImages: {0}", TvDBSeriesID);

        try
        {
            _helper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_TvDBDownloadImages: {SeriesID}", TvDBSeriesID);
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TvDBDownloadImages_{TvDBSeriesID}";
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
        TvDBSeriesID =
            int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "TvDBSeriesID"));
        ForceRefresh =
            bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "ForceRefresh"));

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

    public CommandRequest_TvDBDownloadImages(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TvDBDownloadImages()
    {
    }
}
