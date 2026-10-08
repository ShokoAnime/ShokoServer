using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Services.Airing;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Episode Airing Sequence Numbers | Steps

    /// <summary>
    /// Gives every stored airing written before airings were stored by
    /// sequence number the place on its schedule's line that its episode has.
    /// </summary>
    public static void AssignEpisodeAiringSequenceNumbers()
    {
        var services = ISystemService.StaticServices;
        AssignEpisodeAiringSequenceNumbers(
            (AiringScheduleService)services.GetRequiredService<IAiringScheduleService>(),
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseFixes))
        );
    }

    /// <summary>
    /// Gives the stored airings their sequence numbers, as
    /// <see cref="AssignEpisodeAiringSequenceNumbers()"/> does.
    /// </summary>
    /// <remarks>
    /// An airing of a regular episode takes the place its number gives it, and
    /// is no longer pinned when that place resolves to the same episode. One of
    /// any other episode stays pinned, off the line. One whose episode is gone
    /// takes the number its key ends in, when it does, and is deleted when it
    /// does not. A key derived from the episode becomes the one derived from
    /// the sequence number.
    /// </remarks>
    /// <param name="service">The airing schedule service, whose reads resolve the episodes.</param>
    /// <param name="logger">Told what was changed.</param>
    /// <returns>How many airings were given a sequence number, and how many were deleted.</returns>
    internal static (int Assigned, int Deleted) AssignEpisodeAiringSequenceNumbers(AiringScheduleService service, ILogger logger)
    {
        var context = new AiringReadContext(service, includeDisabled: true);
        var assigned = 0;
        var deleted = 0;
        foreach (var row in RepoFactory.AiringSchedule.GetAll().ToList())
        {
            var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
            var keys = airings.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
            var removed = new List<EpisodeAiring>();
            foreach (var entry in airings)
            {
                if (entry.SequenceNumber is not null || !entry.IsPinned)
                    continue;

                var derivedKey = AiringScheduleUtility.GetDerivedAiringKey(entry.EpisodeSource, entry.EpisodeID);
                var episode = context.GetEpisode(entry.EpisodeSource, entry.EpisodeID);
                var number = episode is null
                    ? (entry.Key == derivedKey ? null : GetTrailingNumber(entry.Key))
                    : episode.Type is EpisodeType.Episode ? episode.EpisodeNumber : null;
                if (number is null || number < row.FirstEpisodeNumber)
                {
                    // Another kind of episode stays pinned off the line, and
                    // one that is gone with nothing to place it by goes too.
                    if (episode is null)
                        removed.Add(entry);

                    continue;
                }

                var sequenceNumber = number.Value - row.FirstEpisodeNumber + 1;
                entry.SequenceNumber = sequenceNumber;
                if (episode is null || context.GetScheduleEpisodeByNumber(row, number.Value) is { } atPlace &&
                    AiringScheduleService.GetEntityKey(atPlace) == AiringScheduleService.GetEntityKey(episode))
                {
                    entry.EpisodeSource = null;
                    entry.EpisodeID = null;
                }

                var sequenceKey = AiringScheduleUtility.GetDerivedSequenceAiringKey(sequenceNumber);
                if (entry.Key == derivedKey && keys.Add(sequenceKey))
                {
                    keys.Remove(entry.Key);
                    entry.Key = sequenceKey;
                }

                RepoFactory.EpisodeAiring.Save(entry);
                assigned++;
            }

            if (removed.Count is 0)
                continue;

            RepoFactory.EpisodeAiring.Delete(removed);
            AiringScheduleService.NormalizeLinkSets(row.AiringScheduleID, []);
            deleted += removed.Count;
        }

        service.ForgetAiringIDs();
        logger.LogInformation(
            "Gave {Assigned} stored airings their sequence numbers, and deleted {Deleted} whose episode is gone with nothing to place them by.",
            assigned,
            deleted
        );
        return (assigned, deleted);
    }

    #endregion

    #region Episode Airing Sequence Numbers | Helpers

    /// <summary>
    /// The number a provider's key ends in: the whole key, or the part after
    /// its last colon, as in <c>1234</c> or <c>5678:12</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The number, or <c>null</c> when the key ends in none.</returns>
    private static int? GetTrailingNumber(string key)
        => int.TryParse(key[(key.LastIndexOf(':') + 1)..], out var number) && number > 0 ? number : null;

    #endregion
}
