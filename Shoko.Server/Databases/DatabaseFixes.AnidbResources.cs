using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region AniDB Resources | Types

    /// <summary>
    ///   What filling the AniDB resource rows from the cached anime XML did.
    /// </summary>
    /// <param name="Filled">The anime whose cached XML was read.</param>
    /// <param name="AnimeRows">The anime-level rows saved.</param>
    /// <param name="EpisodeRows">The episode-level rows saved.</param>
    /// <param name="Queued">The anime queued for a refresh, for want of a readable cached XML.</param>
    internal sealed record AnidbResourceFillResult(int Filled, int AnimeRows, int EpisodeRows, int Queued);

    #endregion

    #region AniDB Resources | Steps

    /// <summary>
    ///   Fills <c>AniDB_Resource</c> for every AniDB anime from its cached
    ///   XML, and queues a refresh of each anime without a readable one.
    /// </summary>
    public static void PopulateAnidbResources()
    {
        var services = ISystemService.StaticServices;
        var systemService = services.GetRequiredService<SystemService>();
        var xmlUtils = services.GetRequiredService<HttpXmlUtils>();
        var parser = services.GetRequiredService<HttpAnimeParser>();
        var scheduler = services.GetRequiredService<IQueueScheduler>();
        var animeIDs = RepoFactory.AniDB_Anime.GetAll().Select(anime => anime.AnimeID).ToList();
        var str = systemService.StartupMessage ?? string.Empty;
        var progress = new StartupProgress(ReportTo(systemService, str), "Filling AniDB resources from the cached anime XML", animeIDs.Count);
        _logger.Info($"Filling AniDB resources for {animeIDs.Count} anidb anime entries from the cached anime XML...");

        var result = FillAnidbResources(
            animeIDs,
            animeID => xmlUtils.LoadAnimeHTTPFromFile(animeID).GetAwaiter().GetResult(),
            parser,
            (response, animeID) => AnimeCreator.CreateResources(response, animeID),
            animeID => QueueAnidbAnimeRefresh(scheduler, animeID),
            () => progress.Advance()
        );

        _logger.Info(
            $"Filled AniDB resources for {result.Filled} anidb anime entries ({result.AnimeRows} anime rows, {result.EpisodeRows} episode rows); " +
            $"queued {result.Queued} anime without a readable cached xml dump for a refresh."
        );
    }

    /// <summary>
    ///   Fills the AniDB resource rows of each anime from its cached XML, and
    ///   hands the anime without a readable one to <paramref name="queueRefresh"/>.
    /// </summary>
    /// <param name="animeIDs">The AniDB anime to fill.</param>
    /// <param name="loadXml">Reads an anime's cached XML, or gives <c>null</c> when there is none.</param>
    /// <param name="parser">The anime XML parser.</param>
    /// <param name="store">Stores the parsed resources of an anime.</param>
    /// <param name="queueRefresh">Queues a refresh of an anime.</param>
    /// <param name="advance">Called once per anime done, or <c>null</c>.</param>
    /// <returns>What was filled and queued.</returns>
    internal static AnidbResourceFillResult FillAnidbResources(
        IReadOnlyList<int> animeIDs,
        Func<int, string?> loadXml,
        HttpAnimeParser parser,
        Action<ResponseGetAnime, int> store,
        Action<int> queueRefresh,
        Action? advance = null
    )
    {
        var filled = 0;
        var animeRows = 0;
        var episodeRows = 0;
        var queued = 0;
        foreach (var animeID in animeIDs)
        {
            try
            {
                ResponseGetAnime? response = null;
                var xml = loadXml(animeID);
                if (!string.IsNullOrEmpty(xml))
                {
                    try
                    {
                        response = parser.Parse(animeID, xml);
                    }
                    catch (Exception e)
                    {
                        _logger.Warn(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {animeID}");
                    }
                }

                if (response is null)
                {
                    queueRefresh(animeID);
                    queued++;
                    continue;
                }

                store(response, animeID);
                filled++;
                animeRows += response.Resources.Count;
                episodeRows += response.Episodes.Sum(episode => episode.Resources.Count);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to fill the AniDB resources for anime: {animeID}");
            }
            finally
            {
                advance?.Invoke();
            }
        }

        return new(filled, animeRows, episodeRows, queued);
    }

    /// <summary>
    ///   Queues a plain refresh of an anime, which reads its cached XML when
    ///   there is one by then and otherwise asks AniDB, within the HTTP rate
    ///   limit and bans, without updating other sources after.
    /// </summary>
    /// <param name="scheduler">The queue.</param>
    /// <param name="animeID">The AniDB anime ID.</param>
    internal static void QueueAnidbAnimeRefresh(IQueueScheduler scheduler, int animeID)
        => scheduler.Enqueue<GetAniDBAnimeJob>(job => (job.AnimeID, job.SkipSupplementaryUpdate) = (animeID, true)).GetAwaiter().GetResult();

    #endregion
}
