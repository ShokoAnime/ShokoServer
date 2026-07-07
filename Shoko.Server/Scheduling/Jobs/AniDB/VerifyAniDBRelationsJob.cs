using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class VerifyAniDBRelationsJob(IRequestFactory requestFactory, AniDB_Anime_RelationRepository anidbAnimeRelations) : BaseJob
{
    public int AnimeID { get; set; }

    public override string TypeName => "Verify AniDB Relations";
    public override string Title => "Verifying AniDB Relations";

    public override Dictionary<string, object> Details => new()
    {
        { "AnimeID", AnimeID }
    };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {AnimeID}", nameof(VerifyAniDBRelationsJob), AnimeID);

        if (AnimeID == 0) return;

        var existingRelations = anidbAnimeRelations.GetByAnimeID(AnimeID);
        var unverified = existingRelations.Where(r => !r.Verified).ToList();
        if (unverified.Count == 0)
        {
            _logger.LogInformation("All relations for {AnimeID} already verified, skipping UDP lookup.", AnimeID);
            return;
        }

        // Resolve any unverified from local verified reverse relations.
        foreach (var rel in unverified)
        {
            if (rel.AbstractRelationType is not (RelationType.AlternativeSetting or RelationType.AlternativeVersion))
            {
                rel.Verified = true;
                continue;
            }

            var reverseRelation = anidbAnimeRelations.GetByAnimeID(rel.RelatedAnimeID)
                .SingleOrDefault(r => r.RelatedAnimeID == AnimeID);
            if (reverseRelation is { Verified: true, AbstractRelationType: RelationType.AlternativeSetting or RelationType.AlternativeVersion })
            {
                rel.AbstractRelationType = reverseRelation.AbstractRelationType.Reverse();
                rel.Verified = true;
            }
        }

        var verifiedNow = unverified.Where(r => r.Verified).ToList();
        if (verifiedNow.Count > 0)
            anidbAnimeRelations.Save(verifiedNow);

        if (!existingRelations.Any(r => !r.Verified))
        {
            _logger.LogInformation("All relations for {AnimeID} resolved from reverse relations, skipping UDP lookup.", AnimeID);
            return;
        }

        // Use the UDP API to verify the remaining unverified relations.
        var request = requestFactory.Create<RequestGetAnime>(r => r.AnimeID = AnimeID);
        var response = request.Send();
        if (response.Response is null)
            return;

        foreach (var stored in existingRelations)
        {
            var udpRelation = response.Response.Relations
                .FirstOrDefault(r => r.RelatedAnimeID == stored.RelatedAnimeID);
            if (udpRelation is not null)
            {
                var udpRelationType = MapRawTypeToString(udpRelation.RawType);
                if (stored.AbstractRelationType != udpRelationType)
                    stored.AbstractRelationType = udpRelationType;
            }

            stored.Verified = true;
        }

        // Verify reverse direction for newly verified targeted types.
        var toSaveReverse = new List<AniDB_Anime_Relation>(existingRelations);
        foreach (var stored in existingRelations)
        {
            var reverseRelations = anidbAnimeRelations.GetByAnimeID(stored.RelatedAnimeID)
                .Where(r => r.RelatedAnimeID == AnimeID && !r.Verified)
                .ToList();
            foreach (var reverse in reverseRelations)
            {
                reverse.AbstractRelationType = stored.AbstractRelationType.Reverse();
                reverse.Verified = true;
                toSaveReverse.Add(reverse);
            }
        }

        anidbAnimeRelations.Save(toSaveReverse);
    }

    private static RelationType MapRawTypeToString(int rawType)
    {
        return rawType switch
        {
            1 => RelationType.Sequel,
            2 => RelationType.Prequel,
            11 => RelationType.SameSetting,
            12 => RelationType.AlternativeSetting,
            32 => RelationType.AlternativeVersion,
            41 => RelationType.Other,
            42 => RelationType.Other,
            51 => RelationType.SideStory,
            52 => RelationType.MainStory,
            61 => RelationType.Summary,
            62 => RelationType.FullStory,
            100 => RelationType.Other,
            _ => RelationType.Other,
        };
    }
}
