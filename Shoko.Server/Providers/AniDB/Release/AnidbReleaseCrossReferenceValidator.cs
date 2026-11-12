using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Providers.AniDB.Release;

/// <summary>
///   The core's validator for the AniDB episodes and anime a release links:
///   each cross-reference names one AniDB episode, known to AniDB, and at most
///   one anime, which is filled in from the episode when missing.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="udpConnection">The AniDB UDP connection, asked whether it is banned.</param>
/// <param name="requestFactory">Makes the UDP request for an episode not held locally.</param>
/// <param name="anidbEpisodes">The AniDB episodes held locally.</param>
public class AnidbReleaseCrossReferenceValidator(
    ILogger<AnidbReleaseCrossReferenceValidator> logger,
    IUDPConnectionHandler udpConnection,
    IRequestFactory requestFactory,
    AniDB_EpisodeRepository anidbEpisodes
) : IReleaseCrossReferenceValidator
{
    #region Fields

    /// <summary>
    ///   The episode IDs AniDB said it does not have, so they are refused
    ///   without asking again.
    /// </summary>
    private readonly ConcurrentDictionary<int, bool> _unknownEpisodeIDs = new();

    #endregion

    #region Properties

    /// <inheritdoc/>
    public string Name => "AniDB";

    /// <inheritdoc/>
    public MetadataEntityScope Scope { get; } = MetadataEntityScope.ForSource(MetadataSource.AniDB, MetadataEntityType.Episode, MetadataEntityType.Series);

    #endregion

    #region Methods

    /// <inheritdoc/>
    public async Task<ReleaseCrossReferenceValidationResult?> Validate(ReleaseCrossReferenceValidationContext context, CancellationToken cancellationToken)
    {
        var crossReference = context.CrossReference;
        var episodes = crossReference.IDs.Where(id => id.Source == MetadataSource.AniDB && id.EntityType == MetadataEntityType.Episode).ToList();
        var anime = crossReference.IDs.Where(id => id.Source == MetadataSource.AniDB && id.EntityType == MetadataEntityType.Series).ToList();
        if (episodes.Count is 0)
            return ReleaseCrossReferenceValidationResult.Invalid("It names an AniDB anime without an AniDB episode.");
        if (episodes.Count > 1)
            return ReleaseCrossReferenceValidationResult.Invalid("It names more than one AniDB episode.");
        if (anime.Count > 1)
            return ReleaseCrossReferenceValidationResult.Invalid("It names more than one AniDB anime.");
        if (!episodes[0].TryGetNumericID<int>(out var episodeID) || episodeID <= 0)
            return ReleaseCrossReferenceValidationResult.Invalid($"\"{episodes[0].ID}\" is not an AniDB episode ID.");
        if (anime.Count is 1 && (!anime[0].TryGetNumericID<int>(out var givenAnimeID) || givenAnimeID <= 0))
            return ReleaseCrossReferenceValidationResult.Invalid($"\"{anime[0].ID}\" is not an AniDB anime ID.");
        if (_unknownEpisodeIDs.ContainsKey(episodeID))
            return ReleaseCrossReferenceValidationResult.Invalid($"AniDB has no episode {episodeID}.");
        if (anime.Count is 1)
            return ReleaseCrossReferenceValidationResult.Valid();

        // Another cross-reference of the release may name the anime for the same episode.
        var animeID = context.CrossReferences
            .Where(other => other.AnidbEpisodeID == episodeID)
            .Select(other => other.AnidbAnimeID ?? 0)
            .Where(id => id > 0)
            .GroupBy(id => id)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault()?
            .Key;
        animeID ??= anidbEpisodes.GetByEpisodeID(episodeID)?.AnimeID;
        if (animeID is null)
        {
            if (udpConnection.IsBanned)
            {
                logger.LogInformation("Could not get the anime ID for AniDB episode {EpisodeID}, but we're UDP banned, so deferring the fetch to later.", episodeID);
                return ReleaseCrossReferenceValidationResult.Valid();
            }

            logger.LogInformation("Could not get the anime ID for AniDB episode {EpisodeID}, downloading more info…", episodeID);
            try
            {
                var response = await requestFactory
                    .Create<RequestGetEpisode>(request => request.EpisodeID = episodeID)
                    .SendAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (response.Code is UDPReturnCode.NO_SUCH_EPISODE)
                {
                    logger.LogError("Unknown AniDB episode with id {EpisodeID}!", episodeID);
                    _unknownEpisodeIDs.TryAdd(episodeID, true);
                    return ReleaseCrossReferenceValidationResult.Invalid($"AniDB has no episode {episodeID}.");
                }

                animeID = response.Response?.AnimeID;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not get the episode info for AniDB episode {EpisodeID}!", episodeID);
            }
        }

        if (animeID is not > 0)
            return ReleaseCrossReferenceValidationResult.Valid();

        return ReleaseCrossReferenceValidationResult.Corrected([.. crossReference.IDs, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, animeID.Value.ToString())]);
    }

    #endregion
}
