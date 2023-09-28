using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Show_Purge)]
public class CommandRequest_TMDB_Show_Purge : CommandRequestImplementation
{
    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _helper;

    public virtual int TmdbShowID { get; set; }

    public virtual bool RemoveImageFiles { get; set; } = true;

    public virtual string ShowTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription =>
        new()
        {
            message = "Purge TMDB Show: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { string.IsNullOrEmpty(ShowTitle) ? TmdbShowID.ToString() : $"{ShowTitle} ({TmdbShowID})" }
        };

    public override void PostInit()
    {
        // TODO: Set the show title when we have finalised the show model and the repostory is usable.
        ShowTitle ??= null;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Show_Purge: {TmdbShowId}", TmdbShowID);
        Task.Run(() => _helper.PurgeShow(TmdbShowID, RemoveImageFiles))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Show_Purge_{TmdbShowID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbShowID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Purge), nameof(TmdbShowID)));
        RemoveImageFiles = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Purge), nameof(RemoveImageFiles)));
        ShowTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Purge), nameof(ShowTitle));

        return true;
    }

    public CommandRequest_TMDB_Show_Purge(ILoggerFactory loggerFactory, TMDBHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TMDB_Show_Purge()
    {
    }
}
