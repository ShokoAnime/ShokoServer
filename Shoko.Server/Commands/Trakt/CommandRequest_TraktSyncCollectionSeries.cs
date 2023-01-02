using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_SyncCollectionSeries)]
public class CommandRequest_TraktSyncCollectionSeries : CommandRequestImplementation
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public int AnimeSeriesID { get; set; }
    public string SeriesName { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Syncing Trakt collection for series: {0}",
        queueState = QueueStateEnum.SyncTraktSeries,
        extraParams = new[] { SeriesName }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktSyncCollectionSeries");

        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled ||
                string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return;
            }

            var series = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
            if (series == null)
            {
                Logger.LogError("Could not find anime series: {AnimeSeriesID}", AnimeSeriesID);
                return;
            }

            _helper.SyncCollectionToTrakt_Series(series);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_TraktSyncCollectionSeries");
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TraktSyncCollectionSeries_{AnimeSeriesID}";
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
        AnimeSeriesID =
            int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "AnimeSeriesID"));
        SeriesName = TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "SeriesName");

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

    public CommandRequest_TraktSyncCollectionSeries(ILoggerFactory loggerFactory, TraktTVHelper helper, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TraktSyncCollectionSeries()
    {
    }
}
