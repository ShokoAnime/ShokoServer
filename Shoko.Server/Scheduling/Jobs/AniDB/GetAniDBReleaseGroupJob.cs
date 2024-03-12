using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBReleaseGroupJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    public int GroupID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get AniDB Release Group Data";

    public override string Title => "Getting AniDB Release Group Data";
    public override Dictionary<string, object> Details => new()
    {
        {
            "GroupID", GroupID
        }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {GroupID}", nameof(GetAniDBReleaseGroupJob), GroupID);

        var relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);
        if (!ForceRefresh && relGroup != null) return Task.CompletedTask;

        var request = _requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = GroupID);
        var response = request.Send();

        if (response?.Response == null) return Task.CompletedTask;

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
        return Task.CompletedTask;
    }
    
    public GetAniDBReleaseGroupJob(IRequestFactory requestFactory)
    {
        _requestFactory = requestFactory;
    }

    protected GetAniDBReleaseGroupJob()
    {
    }
}
