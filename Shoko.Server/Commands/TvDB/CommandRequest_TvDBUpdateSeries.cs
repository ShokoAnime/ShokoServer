using System;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TvDB_UpdateSeries)]
public class CommandRequest_TvDBUpdateSeries : CommandRequestImplementation
{
    private readonly TvDBApiHelper _helper;
    public virtual int TvDBSeriesID { get; set; }
    public virtual bool ForceRefresh { get; set; }
    public virtual string SeriesTitle { get; set; }

    [XmlIgnore][JsonIgnore] public virtual TvDB_Series Result { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating TvDB Series: {0}",
        queueState = QueueStateEnum.GettingTvDBSeries,
        extraParams = new[] { $"{SeriesTitle} ({TvDBSeriesID})" }
    };

    public override void PostInit()
    {
        SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ??
                      string.Intern("Name not Available");
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TvDBUpdateSeries: {0}", TvDBSeriesID);
        Result = _helper.UpdateSeriesInfoAndImages(TvDBSeriesID, ForceRefresh, true);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TvDBUpdateSeries{TvDBSeriesID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TvDBSeriesID =
            int.Parse(docCreator.TryGetProperty("CommandRequest_TvDBUpdateSeries", "TvDBSeriesID"));
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_TvDBUpdateSeries",
                "ForceRefresh"));
        SeriesTitle = docCreator.TryGetProperty("CommandRequest_TvDBUpdateSeries",
                "SeriesTitle");

        return true;
    }

    public CommandRequest_TvDBUpdateSeries(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TvDBUpdateSeries()
    {
    }
}
