using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;

using ImageEntityType = Shoko.Plugin.Abstractions.Enums.ImageEntityType;

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
            var anidbAnimeStaffRoles = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(CreatorID);
            var anidbCharacterCreators = RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(CreatorID);
            var anidbAnimeCharacters = anidbCharacterCreators
                .SelectMany(c => RepoFactory.AniDB_Anime_Character.GetByCharacterID(c.CharacterID))
                .ToList();
            var anidbAnime = anidbAnimeStaffRoles.Select(a => a.AnimeID)
                .Concat(anidbAnimeCharacters.Select(a => a.AnimeID))
                .Distinct()
                .Select(RepoFactory.AniDB_Anime.GetByAnimeID)
                .WhereNotNull()
                .ToList();

            RepoFactory.AniDB_Creator.Delete(CreatorID);
            RepoFactory.AniDB_Anime_Character_Creator.Delete(anidbCharacterCreators);
            RepoFactory.AniDB_Anime_Staff.Delete(anidbAnimeStaffRoles);

            if (anidbAnime.Count > 0)
            {
                _logger.LogInformation("Scheduling {Count} AniDB Anime for a refresh due to removal of creator: {CreatorID}", anidbAnime.Count, CreatorID);
                var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
                foreach (var anime in anidbAnime)
                    await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                    {
                        c.AnimeID = anime.AnimeID;
                        c.UseCache = false;
                        c.CreateSeriesEntry = false;
                        c.DownloadRelations = false;
                    });
            }

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

        var rolesToUpdate = new List<AniDB_Anime_Staff>();
        var roles = RepoFactory.AniDB_Anime_Staff.GetByCreatorID(creator.CreatorID);
        foreach (var role in roles)
        {
            var roleType = role.Role switch
            {
                "Animation Work" when creator.Type is CreatorType.Company => CreatorRoleType.Studio,
                "Work" when creator.Type is CreatorType.Company => CreatorRoleType.Studio,
                "Original Work" => CreatorRoleType.SourceWork,
                "Music" => CreatorRoleType.Music,
                "Character Design" => CreatorRoleType.CharacterDesign,
                "Direction" => CreatorRoleType.Director,
                "Series Composition" => CreatorRoleType.SeriesComposer,
                "Chief Animation Direction" => CreatorRoleType.Producer,
                _ => CreatorRoleType.Staff
            };
            if (role.RoleType != roleType)
            {
                role.RoleType = roleType;
                rolesToUpdate.Add(role);
            }
        }

        RepoFactory.AniDB_Anime_Staff.Save(rolesToUpdate);
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
