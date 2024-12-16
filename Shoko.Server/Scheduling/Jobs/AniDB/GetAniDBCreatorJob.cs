using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBCreatorJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;

    private readonly ISchedulerFactory _schedulerFactory;

    private string? _creatorName;

    public int CreatorID { get; set; }

    public override string TypeName => "Fetch AniDB Creator Details";

    public override string Title => "Fetching AniDB Creator Details";

    public override void PostInit()
    {
        // We have the title helper. May as well use it to provide better info for the user
        _creatorName = RepoFactory.AniDB_Creator?.GetByCreatorID(CreatorID)?.OriginalName;
    }
    public override Dictionary<string, object> Details => string.IsNullOrEmpty(_creatorName)
        ? new()
        {
            { "CreatorID", CreatorID },
        }
        : new()
        {
            { "Creator", _creatorName },
            { "CreatorID", CreatorID },
        };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBCreatorJob));

        var request = _requestFactory.Create<RequestGetCreator>(r => r.CreatorID = CreatorID);
        var response = request.Send().Response;
        if (response is null)
        {
            _logger.LogError("Unable to find an AniDB Creator with the given ID: {CreatorID}", CreatorID);
            return;
        }

        _logger.LogInformation("Found AniDB Creator: {Creator} (ID={CreatorID},Type={Type})", response.Name, response.ID, response.Type.ToString());
        var creator = RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID) ?? new();
        creator.CreatorID = response.ID;
        if (!string.IsNullOrEmpty(response.Name))
            creator.Name = response.Name;
        if (!string.IsNullOrEmpty(response.OriginalName))
            creator.OriginalName = response.OriginalName;
        creator.Type = response.Type;
        creator.ImagePath = response.ImagePath;
        creator.EnglishHomepageUrl = response.EnglishHomepageUrl;
        creator.JapaneseHomepageUrl = response.JapaneseHomepageUrl;
        creator.EnglishWikiUrl = response.EnglishWikiUrl;
        creator.JapaneseWikiUrl = response.JapaneseWikiUrl;
        creator.LastUpdatedAt = response.LastUpdateAt;
        RepoFactory.AniDB_Creator.Save(creator);

        if (RepoFactory.AnimeStaff.GetByAniDBID(creator.CreatorID) is { } staff)
        {
            var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;
            staff.Name = creator.Name;
            staff.AlternateName = creator.OriginalName;
            staff.ImagePath = creator.GetFullImagePath()?.Replace(creatorBasePath, "");
            RepoFactory.AnimeStaff.Save(staff);
        }

        if (!(creator.GetImageMetadata()?.IsLocalAvailable ?? true))
        {
            _logger.LogInformation("Image not found locally, queuing image download for {Creator} (ID={CreatorID},Type={Type})", response.Name, response.ID, response.Type.ToString());
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<DownloadAniDBImageJob>(c =>
            {
                c.ImageType = ImageEntityType.Person;
                c.ImageID = creator.CreatorID;
            });
        }
    }

    public GetAniDBCreatorJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
    }

    protected GetAniDBCreatorJob()
    {
    }
}
