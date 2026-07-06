using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBReleaseGroupJob(IRequestFactory requestFactory, IVideoReleaseService videoReleaseService, CrossRef_File_EpisodeRepository crossRefFileEpisodes, StoredReleaseInfoRepository storedReleaseInfos, VideoLocalRepository videoLocals) : BaseJob
{
    internal static HashSet<string?> InvalidReleaseGroupNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "raw",
        "unk",
        "unknown",
        "raw/unknown",
        "raw/unk",
    };

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

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {GroupID}", nameof(GetAniDBReleaseGroupJob), GroupID);

        // We've got nothing to download.
        var databaseReleaseGroups = storedReleaseInfos.GetByGroupAndProviderIDs(GroupID.ToString(), "AniDB");
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
            storedReleaseInfos.Save(incorrectReleaseGroups);

            return;
        }

        var request = requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = GroupID);
        var response = request.Send();
        if (response.Response is null)
        {
            var xrefsToDelete = new List<CrossRef_File_Episode>();
            var videosToUpdate = new List<VideoLocal>();
            foreach (var databaseReleaseGroup in databaseReleaseGroups)
            {
                var xrefs = crossRefFileEpisodes.GetByEd2k(databaseReleaseGroup.ED2K);
                xrefsToDelete.AddRange(xrefs);

                var video = videoLocals.GetByEd2kAndSize(databaseReleaseGroup.ED2K, databaseReleaseGroup.FileSize);
                if (video is not null)
                    videosToUpdate.Add(video);
            }

            crossRefFileEpisodes.Delete(xrefsToDelete);
            storedReleaseInfos.Delete(databaseReleaseGroups);

            // If auto-match is not available then just ignore the removal of
            // the group, since it seems like we don't care about changes like
            // that.
            if (!videoReleaseService.AutoMatchEnabled)
                return;

            foreach (var video in videosToUpdate)
                await videoReleaseService.ScheduleFindReleaseForVideo(video, force: true);
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
        storedReleaseInfos.Save(databaseReleaseGroups);

        // TODO: Maybe schedule all files with a release from the release group to be ran through the rename/move process again.

        return;
    }

}
