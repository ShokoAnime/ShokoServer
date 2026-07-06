using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBImagesJob(AniDBTitleHelper titleHelper, AnidbService anidbService, AniDB_AnimeRepository anidbAnimes) : BaseJob
{
    private AniDB_Anime _anime;
    private string _title;

    public int AnimeID { get; set; }
    public bool ForceDownload { get; set; }
    public bool OnlyPosters { get; set; }

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
    }
}
