using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

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
    private readonly IVideoReleaseService _videoReleaseService;

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
        var databaseReleaseGroups = RepoFactory.StoredReleaseInfo.GetByGroupAndProviderIDs(GroupID.ToString(), "AniDB");
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
            RepoFactory.StoredReleaseInfo.Save(incorrectReleaseGroups);

            return;
        }

        var request = _requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = GroupID);
        var response = request.Send();
        if (response.Response is null)
        {
            var xrefsToDelete = new List<SVR_CrossRef_File_Episode>();
            var scheduler = await _schedulerFactory.GetScheduler();
            var videosToUpdate = new List<VideoLocal>();
            foreach (var databaseReleaseGroup in databaseReleaseGroups)
            {
                var xrefs = RepoFactory.CrossRef_File_Episode.GetByEd2k(databaseReleaseGroup.ED2K);
                xrefsToDelete.AddRange(xrefs);

                var video = RepoFactory.VideoLocal.GetByEd2kAndSize(databaseReleaseGroup.ED2K, databaseReleaseGroup.FileSize);
                if (video is not null)
                    videosToUpdate.Add(video);
            }

            RepoFactory.CrossRef_File_Episode.Delete(xrefsToDelete);
            RepoFactory.StoredReleaseInfo.Delete(databaseReleaseGroups);

            // If auto-match is not available then just ignore the removal of
            // the group, since it seems like we don't care about changes like
            // that.
            if (!_videoReleaseService.AutoMatchEnabled)
                return;

            foreach (var video in videosToUpdate)
                await _videoReleaseService.ScheduleFindReleaseForVideo(video, force: true);
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
                databaseReleaseGroup.GroupSource = null;
                databaseReleaseGroup.GroupName = null;
                databaseReleaseGroup.GroupShortName = null;
            }
        }
        RepoFactory.StoredReleaseInfo.Save(databaseReleaseGroups);

        // TODO: Maybe schedule all files with a release from the release group to be ran through the rename/move process again.

        return;
    }

    public GetAniDBReleaseGroupJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, IVideoReleaseService videoReleaseService)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _videoReleaseService = videoReleaseService;
    }

    protected GetAniDBReleaseGroupJob()
    {
    }
}
