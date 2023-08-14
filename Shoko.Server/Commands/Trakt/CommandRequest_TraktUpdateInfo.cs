using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_UpdateInfo)]
public class CommandRequest_TraktUpdateInfo : CommandRequestImplementation
{
    private readonly TraktTVHelper _helper;
    public virtual string TraktID { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating info/images on Trakt.TV: {0}",
        queueState = QueueStateEnum.UpdateTraktData,
        extraParams = new[] { TraktID }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktUpdateInfo: {0}", TraktID);
        _helper.UpdateAllInfo(TraktID);
    }


    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TraktUpdateInfo{TraktID}";
    }

    public override bool LoadFromCommandDetails()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TraktID = TryGetProperty(docCreator, "CommandRequest_TraktUpdateInfo", "TraktID");

        return true;
    }

    public CommandRequest_TraktUpdateInfo(ILoggerFactory loggerFactory, TraktTVHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TraktUpdateInfo()
    {
    }
}
