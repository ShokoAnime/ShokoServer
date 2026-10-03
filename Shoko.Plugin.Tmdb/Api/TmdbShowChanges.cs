using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMDbLib.Objects.Changes;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   The seasons and episodes of a show TMDb recorded changes to.
/// </summary>
/// <param name="SeasonNumbers">The numbers of the seasons that changed.</param>
/// <param name="Episodes">The season and episode numbers of the episodes that changed.</param>
public sealed record TmdbShowChanges(IReadOnlySet<int> SeasonNumbers, IReadOnlySet<(int Season, int Episode)> Episodes)
{
    /// <summary>
    ///   Reads TMDb's changes of a show into the seasons and episodes that
    ///   changed.
    /// </summary>
    /// <remarks>
    ///   Only the <c>season</c> and <c>episode</c> keys are read, and an
    ///   episode's change marks its season too, so the season is fetched with
    ///   it.
    /// </remarks>
    /// <param name="changes">The changes TMDb reported.</param>
    /// <returns>What changed.</returns>
    public static TmdbShowChanges Parse(IEnumerable<Change> changes)
    {
        var seasons = new HashSet<int>();
        var episodes = new HashSet<(int, int)>();
        foreach (var change in changes)
        {
            if (change.Key is not ("episode" or "season"))
                continue;

            foreach (var item in change.Items ?? [])
            {
                var value = item switch
                {
                    ChangeItemAdded added => added.Value as JObject,
                    ChangeItemUpdated updated => updated.Value as JObject,
                    ChangeItemDestroyed destroyed => destroyed.Value as JObject,
                    // A deleted item carries the value it had.
                    ChangeItemDeleted deleted => deleted.OriginalValue as JObject,
                    _ => null,
                };
                if (value?["season_number"]?.Value<int?>() is not { } seasonNumber)
                    continue;

                seasons.Add(seasonNumber);
                if (change.Key is "episode" && value["episode_number"]?.Value<int?>() is { } episodeNumber)
                    episodes.Add((seasonNumber, episodeNumber));
            }
        }

        return new(seasons, episodes);
    }
}
