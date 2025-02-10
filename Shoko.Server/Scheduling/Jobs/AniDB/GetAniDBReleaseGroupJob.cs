using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Shoko;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBReleaseGroupJob : BaseJob
{
    internal static HashSet<string?> InvalidReleaseGroupNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "raw",
        "unk",
        "unknown",
        "raw/unknown",
        "raw/unk",
    };

    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
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

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {GroupID}", nameof(GetAniDBReleaseGroupJob), GroupID);

        // We've got nothing to download.
        var databaseReleaseGroups = RepoFactory.DatabaseReleaseInfo.GetByGroupAndProviderIDs(GroupID.ToString(), "AniDB");
        if (databaseReleaseGroups.Count == 0)
            return;

        // We already have the data, but it may not be populated everywhere, so just check that.
        var existingReleaseGroup = databaseReleaseGroups
            .Where(rI => !string.IsNullOrEmpty(rI.GroupName) && !string.IsNullOrEmpty(rI.GroupShortName))
            .OrderByDescending(rI => rI.LastUpdatedAt)
            .FirstOrDefault();
        if (!ForceRefresh && existingReleaseGroup is not null)
        {
            var incorrectReleaseGroups = databaseReleaseGroups
                .Where(rI => !string.Equals(rI.GroupName, existingReleaseGroup.GroupName) || !string.Equals(rI.GroupShortName, existingReleaseGroup.GroupShortName))
                .ToList();
            foreach (var incorrectReleaseGroup in incorrectReleaseGroups)
            {
                incorrectReleaseGroup.GroupName = existingReleaseGroup.GroupName;
                incorrectReleaseGroup.GroupShortName = existingReleaseGroup.GroupShortName;
            }
            RepoFactory.DatabaseReleaseInfo.Save(incorrectReleaseGroups);

            return;
        }

        var request = _requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = GroupID);
        var response = request.Send();
        if (response.Response is null)
        {
            var xrefsToDelete = new List<SVR_CrossRef_File_Episode>();
            var scheduler = await _schedulerFactory.GetScheduler();
            var videosToUpdate = new HashSet<int>();
            foreach (var databaseReleaseGroup in databaseReleaseGroups)
            {
                var xrefs = RepoFactory.CrossRef_File_Episode.GetByEd2k(databaseReleaseGroup.ED2K);
                xrefsToDelete.AddRange(xrefs);

                var video = RepoFactory.VideoLocal.GetByEd2kAndSize(databaseReleaseGroup.ED2K, databaseReleaseGroup.FileSize);
                if (video is not null)
                    videosToUpdate.Add(video.VideoLocalID);
            }

            RepoFactory.CrossRef_File_Episode.Delete(xrefsToDelete);
            RepoFactory.DatabaseReleaseInfo.Delete(databaseReleaseGroups);

            foreach (var videoID in videosToUpdate)
                await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = videoID;
                    c.ForceRecheck = true;
                });
            return;
        }

        var groupName = response.Response.Name;
        var groupShortName = response.Response.ShortName;
        var isValid = !string.IsNullOrEmpty(groupName) &&
            !string.IsNullOrEmpty(groupShortName) &&
            !InvalidReleaseGroupNames.Contains(groupName) &&
            !InvalidReleaseGroupNames.Contains(groupShortName);
        foreach (var databaseReleaseGroup in databaseReleaseGroups)
        {
            if (isValid)
            {
                databaseReleaseGroup.GroupName = groupName;
                databaseReleaseGroup.GroupShortName = groupShortName;
            }
            else
            {
                databaseReleaseGroup.GroupID = null;
                databaseReleaseGroup.GroupProviderID = null;
                databaseReleaseGroup.GroupName = null;
                databaseReleaseGroup.GroupShortName = null;
            }
        }
        RepoFactory.DatabaseReleaseInfo.Save(databaseReleaseGroups);

        // TODO: Maybe schedule all files with a release from the release group to be ran through the rename/move process again.

        return;
    }

    public GetAniDBReleaseGroupJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
    }

    protected GetAniDBReleaseGroupJob()
    {
    }
}
