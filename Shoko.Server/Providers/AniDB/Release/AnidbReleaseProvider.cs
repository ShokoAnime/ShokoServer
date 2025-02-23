
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
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Info;

#nullable enable
namespace Shoko.Server.Providers.AniDB.Release;

public class AnidbReleaseProvider(ILogger<AnidbReleaseProvider> logger, IRequestFactory requestFactory, IUDPConnectionHandler connectionHandler) : IReleaseInfoProvider
{
    public IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(25),
        SizeLimit = 10,
    });

    public const string ReleasePrefix = "https://anidb.net/file/";

    public string Name => "AniDB";

    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    public async Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(releaseId, out ReleaseInfoWithProvider? releaseInfo))
            return releaseInfo;

        if (string.IsNullOrEmpty(releaseId))
            return null;

        var (hash, fileSize) = releaseId.Split('+');
        if (string.IsNullOrEmpty(hash) || hash.Length != 32 || !long.TryParse(fileSize, out var size))
            return null;

        if (connectionHandler.IsBanned)
        {
            logger.LogInformation("Unable to lookup release for {Hash} & {Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }

        ResponseGetFile? anidbFile = null;
        try
        {
            var response = await Task.Run(() => requestFactory.Create<RequestGetFile>(request => { request.Hash = hash; request.Size = size; }).Send());
            anidbFile = response?.Response;
        }
        catch (NotLoggedInException ex)
        {
            logger.LogError(ex, "Unable to lookup release for {Hash} & {Size} due to being AniDB UDP banned.", hash, size);
        }
        catch (AniDBBannedException ex)
        {
            logger.LogError(ex, "Unable to lookup release for {Hash} & {Size} due to being AniDB UDP banned.", hash, size);
        }
        if (anidbFile is null)
            return null;

        releaseInfo = new ReleaseInfoWithProvider(Name)
        {
            ID = releaseId,
            ProviderID = Name,
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
            CrossReferences = [],
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

        _memoryCache.Set(releaseId, releaseInfo, TimeSpan.FromHours(1));
        return releaseInfo;
    }

    public Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
        => GetReleaseInfoById($"{video.Hashes.ED2K}+{video.Size}", cancellationToken);
}
