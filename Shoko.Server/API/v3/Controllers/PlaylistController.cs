using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3, Authorize]
public class PlaylistController : BaseController
{
    private readonly GeneratedPlaylistService _playlistService;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly VideoLocalRepository _videoRepository;

    public PlaylistController(ISettingsProvider settingsProvider, GeneratedPlaylistService playlistService, AnimeSeriesRepository animeSeriesRepository, AnimeEpisodeRepository animeEpisodeRepository, VideoLocalRepository videoRepository) : base(settingsProvider)
    {
        _playlistService = playlistService;
        _seriesRepository = animeSeriesRepository;
        _episodeRepository = animeEpisodeRepository;
        _videoRepository = videoRepository;
    }

    /// <summary>
    /// Generate an on-demand playlist for the specified list of items.
    /// </summary>
    /// <remarks>
    /// **Playlist item DSL** (`playlist` query parameter):
    ///
    /// The `playlist` parameter accepts one or more comma-separated entries. Each entry
    /// is a list of sub-items joined by `+` or a space.
    ///
    /// **Prefix reference:**
    ///
    /// | Prefix | Type and ID source |
    /// |--------|-------------------|
    /// | `g` | **Group** — Shoko AnimeGroup ID. Follows prequel/sequel chain within the group (or all series with `includeAllSeries`). |
    /// | `a` | **Series** — AniDB Anime ID. Resolves to next-up episodes. |
    /// | `s` | **Series** — Shoko AnimeSeries ID. Resolves to next-up episodes. |
    /// | `e` | **Episode** — AniDB Episode ID. Resolves to the best file(s). |
    /// | `E` | **Episode** — Shoko AnimeEpisode ID. Resolves to the best file(s). |
    /// | `f` | **File** — ED2K hash (32 hex chars), optionally `hash-fileSize`. The `f` prefix disambiguates from a bare number. Without it, a bare hash or number is still treated as a file. |
    /// | (bare hash) | **File** — ED2K hash (32+ hex chars); optionally `hash-fileSize`. |
    /// | (bare number) | **File** — Shoko `VideoLocal` internal ID. Not a series ID! |
    /// | `r` | **Release group modifier** — AniDB Release Group ID. Filters file selection by release group. Not a standalone entry. |
    ///
    /// **Release group (`r`) interaction:**
    ///
    /// | Usage | Behavior |
    /// |-------|----------|
    /// | `a id r gid` (or `s id r gid`, `g id r gid`) | Filters the series' episode cross-references to the given release group. |
    /// | `r gid e id` (or `E id`) | Selects the episode, then picks only files from release group `gid`. |
    /// | `r gid f hash` | **Error:** a release group cannot be specified for a direct file reference. |
    /// | `r gid` alone | **No-op:** silently skipped — no episodes or files to resolve. |
    /// | `e id f hash` (or `E id f hash`) with `r` | `r` is silently ignored — the user has already provided explicit episode-file pairs, so no file selection occurs. |
    /// | No `r` | The most-used release group for the series/episode is auto-selected. |
    ///
    /// **Series extras** (appended to `a` or `s` after a `+`, dash-separated):
    ///
    /// `a id+onlyUnwatched-includeSpecials-includeOthers-includeRewatching`
    ///
    /// | Extra | Effect |
    /// |-------|--------|
    /// | `onlyUnwatched` | Exclude episodes currently being watched. |
    /// | `includeSpecials` | Include special episode types. |
    /// | `includeOthers` | Include other types (credits, trailers, etc.). |
    /// | `includeRewatching` | Include episodes currently being rewatched. |
    ///
    /// **Group extras** (appended to `g` after a `+`, dash-separated):
    ///
    /// Same extras as series plus:
    ///
    /// | Extra | Effect |
    /// |-------|--------|
    /// | `recursive` | Include series from child groups. |
    /// | `includeAllSeries` | Skip prequel/sequel chain, include all series by air date (implies recursive). |
    /// | `includePrequels` | Walk prequel relations backward too. No-op if `includeAllSeries`. |
    ///
    /// **Examples:**
    ///
    /// ```
    /// a123                          Next-up episodes for anime 123 (AniDB ID)
    /// s456                          Next-up episodes for Shoko series 456
    /// a123 r789                     Same, files from release group 789 only
    /// a123+onlyUnwatched            Only unwatched episodes
    /// s456+includeSpecials-includeOthers  Include specials and other types
    /// g123                          Chain for Shoko group 123 (direct children, sequels only)
    /// g123+recursive                Chain including child groups
    /// g123+includePrequels          Chain walking prequels backward too
    /// g123+includeAllSeries         All series in group, air date order
    /// g123+includeAllSeries-onlyUnwatched  All series, unwatched only
    /// e98765                        Episode 98765 (AniDB Episode ID), best file
    /// E54321                        Episode 54321 (Shoko AnimeEpisode ID)
    /// r789 e98765                   Episode 98765, files from group 789
    /// a123,r789 e98765,fabc123de... Three entries: anime 123 + episode 98765 (group 789) + file
    /// E54321 fabc123de...           Episode 54321 paired with a specific file
    /// 42                            File by Shoko VideoLocal ID
    /// abc123de...                   File by bare ED2K hash
    /// abc123de...-123456            File by ED2K hash + file size
    /// ```
    /// </remarks>
    /// <param name="items">Comma-separated playlist items. See remarks for the full DSL format.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns></returns>
    [HttpGet("Generate")]
    [ProducesResponseType(400)]
    public ActionResult<IReadOnlyList<PlaylistItem>> GetGeneratedPlaylistJson(
        [FromQuery(Name = "playlist"), ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null
    )
    {
        include ??= [];
        if (!_playlistService.TryParsePlaylist(items ?? [], out var playlist, ModelState))
            return ValidationProblem(ModelState);

        return playlist
            .Select(tuple => new PlaylistItem(
                tuple.episodes
                    .Select(episode =>
                    {
                        var animeEpisode = (episode as AnimeEpisode)!;
                        return new PlaylistEpisode(
                            animeEpisode,
                            animeEpisode.AnimeSeries!,
                            animeEpisode.AniDB_Episode!,
                            animeEpisode.AniDB_Anime!
                        );
                    })
                    .ToList(),
                tuple.videos
                    .Select(video => new File(
                        HttpContext,
                        (video as VideoLocal)!,
                        include.Contains(FileNonDefaultIncludeType.XRefs),
                        include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
                        include.Contains(FileNonDefaultIncludeType.MediaInfo),
                        include.Contains(FileNonDefaultIncludeType.AbsolutePaths),
                        include.Contains(FileNonDefaultIncludeType.LocationUIDs)
                    ))
                    .ToList()
            ))
            .ToList();
    }

    /// <summary>
    /// Generate an on-demand playlist for the specified list of items, as a .m3u8 file.
    /// </summary>
    /// <remarks>
    /// **Playlist item DSL** (`playlist` query parameter):
    ///
    /// The `playlist` parameter accepts one or more comma-separated entries. Each entry
    /// is a list of sub-items joined by `+` or a space.
    ///
    /// **Prefix reference:**
    ///
    /// | Prefix | Type and ID source |
    /// |--------|-------------------|
    /// | `g` | **Group** — Shoko AnimeGroup ID. Follows prequel/sequel chain within the group (or all series with `includeAllSeries`). |
    /// | `a` | **Series** — AniDB Anime ID. Resolves to next-up episodes. |
    /// | `s` | **Series** — Shoko AnimeSeries ID. Resolves to next-up episodes. |
    /// | `e` | **Episode** — AniDB Episode ID. Resolves to the best file(s). |
    /// | `E` | **Episode** — Shoko AnimeEpisode ID. Resolves to the best file(s). |
    /// | `f` | **File** — ED2K hash (32 hex chars), optionally `hash-fileSize`. The `f` prefix disambiguates from a bare number. Without it, a bare hash or number is still treated as a file. |
    /// | (bare hash) | **File** — ED2K hash (32+ hex chars); optionally `hash-fileSize`. |
    /// | (bare number) | **File** — Shoko `VideoLocal` internal ID. Not a series ID! |
    /// | `r` | **Release group modifier** — AniDB Release Group ID. Filters file selection by release group. Not a standalone entry. |
    ///
    /// **Release group (`r`) interaction:**
    ///
    /// | Usage | Behavior |
    /// |-------|----------|
    /// | `a id r gid` (or `s id r gid`, `g id r gid`) | Filters the series' episode cross-references to the given release group. |
    /// | `r gid e id` (or `E id`) | Selects the episode, then picks only files from release group `gid`. |
    /// | `r gid f hash` | **Error:** a release group cannot be specified for a direct file reference. |
    /// | `r gid` alone | **No-op:** silently skipped — no episodes or files to resolve. |
    /// | `e id f hash` (or `E id f hash`) with `r` | `r` is silently ignored — the user has already provided explicit episode-file pairs, so no file selection occurs. |
    /// | No `r` | The most-used release group for the series/episode is auto-selected. |
    ///
    /// **Series extras** (appended to `a` or `s` after a `+`, dash-separated):
    ///
    /// `a id+onlyUnwatched-includeSpecials-includeOthers-includeRewatching`
    ///
    /// | Extra | Effect |
    /// |-------|--------|
    /// | `onlyUnwatched` | Exclude episodes currently being watched. |
    /// | `includeSpecials` | Include special episode types. |
    /// | `includeOthers` | Include other types (credits, trailers, etc.). |
    /// | `includeRewatching` | Include episodes currently being rewatched. |
    ///
    /// **Group extras** (appended to `g` after a `+`, dash-separated):
    ///
    /// Same extras as series plus:
    ///
    /// | Extra | Effect |
    /// |-------|--------|
    /// | `recursive` | Include series from child groups. |
    /// | `includeAllSeries` | Skip prequel/sequel chain, include all series by air date (implies recursive). |
    /// | `includePrequels` | Walk prequel relations backward too. No-op if `includeAllSeries`. |
    ///
    /// **Examples:**
    ///
    /// ```
    /// a123                          Next-up episodes for anime 123 (AniDB ID)
    /// s456                          Next-up episodes for Shoko series 456
    /// a123 r789                     Same, files from release group 789 only
    /// a123+onlyUnwatched            Only unwatched episodes
    /// s456+includeSpecials-includeOthers  Include specials and other types
    /// g123                          Chain for Shoko group 123 (direct children, sequels only)
    /// g123+recursive                Chain including child groups
    /// g123+includePrequels          Chain walking prequels backward too
    /// g123+includeAllSeries         All series in group, air date order
    /// g123+includeAllSeries-onlyUnwatched  All series, unwatched only
    /// e98765                        Episode 98765 (AniDB Episode ID), best file
    /// E54321                        Episode 54321 (Shoko AnimeEpisode ID)
    /// r789 e98765                   Episode 98765, files from group 789
    /// a123,r789 e98765,fabc123de... Three entries: anime 123 + episode 98765 (group 789) + file
    /// E54321 fabc123de...           Episode 54321 paired with a specific file
    /// 42                            File by Shoko VideoLocal ID
    /// abc123de...                   File by bare ED2K hash
    /// abc123de...-123456            File by ED2K hash + file size
    /// ```
    /// </remarks>
    /// <param name="items">Comma-separated playlist items. See remarks for the full DSL format.</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [Produces("application/x-mpegURL")]
    [HttpGet("Generate.m3u8")]
    [HttpHead("Generate.m3u8")]
    public ActionResult GetGeneratedPlaylistM3U8(
        [FromQuery(Name = "playlist"), ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null
    )
    {
        if (!_playlistService.TryParsePlaylist(items ?? [], out var playlist, ModelState))
            return ValidationProblem(ModelState);

        return _playlistService.GeneratePlaylist(playlist, "Mixed");
    }
}
