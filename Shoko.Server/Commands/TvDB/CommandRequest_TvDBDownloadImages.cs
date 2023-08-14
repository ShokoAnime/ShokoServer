using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
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
    public virtual int TvDBSeriesID { get; set; }
    public virtual bool ForceRefresh { get; set; }

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
        _helper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TvDBDownloadImages_{TvDBSeriesID}";
    }

    public override bool LoadFromCommandDetails()
    {
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

    public CommandRequest_TvDBDownloadImages(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TvDBDownloadImages()
    {
    }
}
