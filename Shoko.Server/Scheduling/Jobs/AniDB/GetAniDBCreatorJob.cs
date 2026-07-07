using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBCreatorJob(IRequestFactory requestFactory, AnidbService anidbService, AniDB_AnimeRepository anidbAnimes, AniDB_Anime_CharacterRepository anidbAnimeCharacters, AniDB_Anime_Character_CreatorRepository anidbAnimeCharacterCreators, AniDB_Anime_StaffRepository anidbAnimeStaff, AniDB_CreatorRepository anidbCreators) : BaseJob
{
    private string? _creatorName;

    public int CreatorID { get; set; }

    public override string TypeName => "Fetch AniDB Creator Details";

    public override string Title => "Fetching AniDB Creator Details";

    public override void PostInit()
    {
        // We have the title helper. May as well use it to provide better info for the user
        _creatorName = anidbCreators.GetByCreatorID(CreatorID)?.OriginalName;
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

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBCreatorJob));

        var request = requestFactory.Create<RequestGetCreator>(r => r.CreatorID = CreatorID);
        var response = request.Send().Response;
        if (response is null)
        {
            _logger.LogError("Unable to find an AniDB Creator with the given ID: {CreatorID}", CreatorID);
            var anidbAnimeStaffRoles = anidbAnimeStaff.GetByAnimeID(CreatorID);
            var anidbCharacterCreators = anidbAnimeCharacterCreators.GetByCreatorID(CreatorID);
            var animeCharacters = anidbCharacterCreators
                .SelectMany(c => anidbAnimeCharacters.GetByCharacterID(c.CharacterID))
                .ToList();
            var anidbAnime = anidbAnimeStaffRoles.Select(a => a.AnimeID)
                .Concat(animeCharacters.Select(a => a.AnimeID))
                .Distinct()
                .Select(anidbAnimes.GetByAnimeID)
                .WhereNotNull()
                .ToList();

            anidbCreators.Delete(CreatorID);
            anidbAnimeCharacterCreators.Delete(anidbCharacterCreators);
            anidbAnimeStaff.Delete(anidbAnimeStaffRoles);

            if (anidbAnime.Count > 0)
            {
                _logger.LogInformation("Scheduling {Count} AniDB Anime for a refresh due to removal of creator: {CreatorID}", anidbAnime.Count, CreatorID);
                foreach (var anime in anidbAnime)
                    await anidbService.ScheduleRefreshOfAnime(anime, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful).ConfigureAwait(false);
            }

            return;
        }

        _logger.LogInformation("Found AniDB Creator: {Creator} (ID={CreatorID}, Type={Type})", response.Name, response.ID, response.Type.ToString());
        var creator = anidbCreators.GetByCreatorID(CreatorID) ?? new();
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
        anidbCreators.Save(creator);

        await anidbService.ProcessImagesForCreatorByID(creator.CreatorID).ConfigureAwait(false);

        var rolesToUpdate = new List<AniDB_Anime_Staff>();
        var roles = anidbAnimeStaff.GetByCreatorID(creator.CreatorID);
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

        anidbAnimeStaff.Save(rolesToUpdate);
    }
}
