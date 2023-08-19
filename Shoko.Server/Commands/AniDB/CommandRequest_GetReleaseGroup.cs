using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetReleaseGroup)]
public class CommandRequest_GetReleaseGroup : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    public virtual int GroupID { get; set; }
    public virtual bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting release group info from UDP API: {0}",
        queueState = QueueStateEnum.GetReleaseInfo,
        extraParams = new[] { GroupID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_GetReleaseGroup: {GroupID}", GroupID);


        var relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);

        if (!ForceRefresh && relGroup != null) return;

        // redownload anime details from http ap so we can get an update character list
        var request = _requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = GroupID);
        var response = request.Execute();

        if (response?.Response == null)
        {
            return;
        }

        relGroup ??= new AniDB_ReleaseGroup();
        relGroup.GroupID = response.Response.ID;
        relGroup.Rating = (int)(response.Response.Rating * 100);
        relGroup.Votes = response.Response.Votes;
        relGroup.AnimeCount = response.Response.AnimeCount;
        relGroup.FileCount = response.Response.FileCount;
        relGroup.GroupName = response.Response.Name;
        relGroup.GroupNameShort = response.Response.ShortName;
        relGroup.IRCChannel = response.Response.IrcChannel;
        relGroup.IRCServer = response.Response.IrcServer;
        relGroup.URL = response.Response.URL;
        relGroup.Picname = response.Response.Picture;
        RepoFactory.AniDB_ReleaseGroup.Save(relGroup);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_GetReleaseGroup_{GroupID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        GroupID = int.Parse(docCreator.TryGetProperty("CommandRequest_GetReleaseGroup", "GroupID"));
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_GetReleaseGroup", "ForceRefresh"));

        return true;
    }

    public CommandRequest_GetReleaseGroup(ILoggerFactory loggerFactory, IRequestFactory requestFactory) :
        base(loggerFactory)
    {
        _requestFactory = requestFactory;
    }

    protected CommandRequest_GetReleaseGroup()
    {
    }
}
