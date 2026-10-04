using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
[JobPriority(Default = 10, Prioritized = 60)]
public class GetAniDBImagesJob(
    AniDBTitleHelper titleHelper,
    AnidbService anidbService,
    AniDB_AnimeRepository anidbAnimes,
    MetadataImageContributorScheduler contributorScheduler
) : BaseJob
{
    private AniDB_Anime? _anime;
    private string? _title;

    public int AnimeID { get; set; }
    public bool ForceDownload { get; set; }
    public bool OnlyPosters { get; set; }

    /// <summary>
    ///   Whether the anime or its series was just created, which the
    ///   contributors' jobs are ranked by. Left out of the job's key.
    /// </summary>
    [JobKeyIgnore]
    public bool IsNew { get; set; }

    public override string TypeName => "Get AniDB Images Data";

    public override string Title => "Getting AniDB Image Data";
    public override Dictionary<string, object> Details => _title == null
        ? new()
        {
            {
                "AnimeID", AnimeID
            }
        }
        : new()
        {
            {
                "Anime", _title
            }
        };

    public override void PostInit()
    {
        _anime = anidbAnimes.GetByAnimeID(AnimeID);
        _title = _anime?.Title ?? titleHelper.SearchAnimeID(AnimeID)?.Title;
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(GetAniDBImagesJob), _anime?.Title ?? AnimeID.ToString());
        if (_anime == null)
        {
            _logger.LogWarning("{Anime} was null for {AnimeID}", nameof(_anime), AnimeID);
            return;
        }

        await anidbService.ProcessImagesForAnimeByID(AnimeID, OnlyPosters, ForceDownload).ConfigureAwait(false);

        // The contributors add theirs once AniDB's are linked.
        await contributorScheduler.ScheduleForEntry(
            new(MetadataSource.AniDB, MetadataEntityType.Series, AnimeID.ToString()),
            ForceDownload,
            isNew: IsNew
        ).ConfigureAwait(false);
    }
}
