
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Providers.AniDB.Release;

/// <summary>
///    The built-in AniDB release provider, using the AniDB UDP API through the
///    internal UDP connection handler.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="requestFactory">The request factory.</param>
/// <param name="connectionHandler">The connection handler.</param>
/// <param name="videoRepository">The video repository.</param>
public class AnidbReleaseProvider(ILogger<AnidbReleaseProvider> logger, IRequestFactory requestFactory, IUDPConnectionHandler connectionHandler, VideoLocalRepository videoRepository) : IReleaseInfoProvider
{
    /// <summary>
    /// Simple memory cache to prevent looking up the same file multiple times within half an hour.
    /// </summary>
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(25),
    });

    /// <summary>
    ///    Prefix for AniDB file URLs.
    /// </summary>
    public const string ReleasePrefix = "https://anidb.net/file/";

    /// <inheritdoc/>
    public string Name => "AniDB";

    /// <inheritdoc/>
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
        => GetReleaseInfoById($"{video.Hashes.ED2K}+{video.Size}", video);

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => GetReleaseInfoById(releaseId, null);

    /// <summary>
    ///    Gets the release info by ID. The ID should be a hash+size combination.
    /// </summary>
    /// <param name="releaseId">Release ID. Hash+Size.</param>
    /// <param name="video">Optional. A loaded video instance to use for some extra metadata to include in the release.</param>
    /// <returns>The release info, or null if not found.</returns>
    private async Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, IVideo? video = null)
    {
        if (_memoryCache.TryGetValue(releaseId, out ReleaseInfo? releaseInfo))
            return releaseInfo;

        if (string.IsNullOrEmpty(releaseId))
            return null;

        var (hash, fileSize) = releaseId.Split('+');
        if (string.IsNullOrEmpty(hash) || hash.Length != 32 || !long.TryParse(fileSize, out var size))
            return null;

        if (connectionHandler.IsBanned)
        {
            logger.LogInformation("Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }

        ResponseGetFile anidbFile;
        try
        {
            var response = await Task.Run(() => requestFactory.Create<RequestGetFile>(request => { request.Hash = hash; request.Size = size; }).Send());
            if (response?.Response is null)
            {
                logger.LogInformation("Unable to find a release for Hash={Hash} & Size={Size} at AniDB.", hash, size);
                return null;
            }

            anidbFile = response.Response;
        }
        catch (NotLoggedInException ex)
        {
            logger.LogError(ex, "Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }
        catch (AniDBBannedException ex)
        {
            logger.LogError(ex, "Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }

        video ??= videoRepository.GetByEd2kAndSize(hash, size);
        releaseInfo = new ReleaseInfo()
        {
            ID = releaseId,
            ReleaseURI = $"{ReleasePrefix}{anidbFile.FileID}",
            Revision = anidbFile.Version,
            Comment = anidbFile.Description,
            OriginalFilename = anidbFile.Filename,
            IsCensored = anidbFile.Censored,
            IsCorrupted = anidbFile.Deprecated,
            Source = anidbFile.Source switch
            {
                GetFile_Source.TV => ReleaseSource.TV,
                GetFile_Source.DTV => ReleaseSource.TV,
                GetFile_Source.HDTV => ReleaseSource.TV,
                GetFile_Source.DVD => ReleaseSource.DVD,
                GetFile_Source.HKDVD => ReleaseSource.DVD,
                GetFile_Source.HDDVD => ReleaseSource.DVD,
                GetFile_Source.VHS => ReleaseSource.VHS,
                GetFile_Source.Camcorder => ReleaseSource.Camera,
                GetFile_Source.VCD => ReleaseSource.VCD,
                GetFile_Source.SVCD => ReleaseSource.VCD,
                GetFile_Source.LaserDisc => ReleaseSource.LaserDisc,
                GetFile_Source.BluRay => ReleaseSource.BluRay,
                GetFile_Source.Web => ReleaseSource.Web,
                _ => ReleaseSource.Unknown,
            },
            Group = new()
            {
                ID = anidbFile.GroupID?.ToString() ?? string.Empty,
                Source = "AniDB",
                Name = string.IsNullOrEmpty(anidbFile.GroupName) ? string.Empty : anidbFile.GroupName,
                ShortName = string.IsNullOrEmpty(anidbFile.GroupShortName) ? string.Empty : anidbFile.GroupShortName,
            },
            MediaInfo = new()
            {
                AudioLanguages = anidbFile.AudioLanguages
                    .Select(a => a.GetTitleLanguage())
                    .ToList(),
                SubtitleLanguages = anidbFile.SubtitleLanguages
                    .Select(a => a.GetTitleLanguage())
                    .ToList(),
            },
            FileSize = size,
            Hashes = [
                    new() { Type = "ED2K", Value = hash },
                    ..video?.Hashes.Hashes.Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata }) ?? [],
                ],
            ReleasedAt = anidbFile.ReleasedAt,
            CreatedAt = DateTime.Now,
        };

        // These percentages will probably be wrong, but we can tolerate that for now
        // until a better solution to get more accurate readings for the start/end ranges
        // is found.
        var offset = 0;
        foreach (var xref in anidbFile.EpisodeIDs)
        {
            releaseInfo.CrossReferences.Add(new()
            {
                AnidbAnimeID = anidbFile.AnimeID,
                AnidbEpisodeID = xref.EpisodeID,
                PercentageStart = xref.Percentage < 100 ? offset : 0,
                PercentageEnd = xref.Percentage < 100 ? offset + xref.Percentage : 100,
            });
            if (xref.Percentage < 100)
                offset += xref.Percentage;
        }
        foreach (var xref in anidbFile.OtherEpisodes)
        {
            releaseInfo.CrossReferences.Add(new()
            {
                AnidbEpisodeID = xref.EpisodeID,
                PercentageStart = xref.Percentage < 100 ? offset : 0,
                PercentageEnd = xref.Percentage < 100 ? offset + xref.Percentage : 100,
            });
            if (xref.Percentage < 100)
                offset += xref.Percentage;
        }

        logger.LogInformation("Found a release for Hash={Hash} & Size={Size} at AniDB!", hash, size);
        _memoryCache.Set(releaseId, releaseInfo, TimeSpan.FromMinutes(30));
        return releaseInfo;
    }
}
