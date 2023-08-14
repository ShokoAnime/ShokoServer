using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.Refresh_AnimeStats)]
public class CommandRequest_RefreshAnime : CommandRequestImplementation
{
    public virtual int AnimeID { get; set; }


    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Refreshing anime stats: {0}",
        queueState = QueueStateEnum.Refresh,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_RefreshAnime_{AnimeID}";
    }

    public override bool LoadFromCommandDetails()
    {
        AnimeID = int.Parse(CommandDetails);
        return true;
    }

    protected override string GetCommandDetails()
    {
        return AnimeID.ToString();
    }

    public CommandRequest_RefreshAnime(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_RefreshAnime()
    {
    }
}
