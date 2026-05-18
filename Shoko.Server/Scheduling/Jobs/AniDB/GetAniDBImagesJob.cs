using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBImagesJob : BaseJob
{
    private AniDB_Anime _anime;
    private string _title;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly AnidbService _anidbService;

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
        _anime = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID);
        _title = _anime?.Title ?? _titleHelper.SearchAnimeID(AnimeID)?.Title;
    }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(GetAniDBImagesJob), _anime?.Title ?? AnimeID.ToString());
        if (_anime == null)
        {
            _logger.LogWarning("{Anime} was null for {AnimeID}", nameof(_anime), AnimeID);
            return;
        }

        await _anidbService.ProcessImagesForAnimeByID(AnimeID, OnlyPosters, ForceDownload).ConfigureAwait(false);
    }

    public GetAniDBImagesJob(AniDBTitleHelper aniDBTitleHelper, IAnidbService anidbService)
    {
        _titleHelper = aniDBTitleHelper;
        _anidbService = (AnidbService)anidbService;
    }

    protected GetAniDBImagesJob() { }
}
