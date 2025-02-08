
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;

#nullable enable
namespace Shoko.Server.Providers.AniDB.Release;

public class AnidbReleaseProvider(IRequestFactory requestFactory, IMetadataService metadataService) : IReleaseInfoProvider
{
    public string Name => "AniDB";

    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    public async Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(releaseId))
            return null;

        var (hash, fileSize) = releaseId.Split('-');
        if (string.IsNullOrEmpty(hash) || hash.Length != 32 || !long.TryParse(fileSize, out var size))
            return null;

        var response = await Task.Run(() => requestFactory.Create<RequestGetFile>(request =>
            {
                request.Hash = hash;
                request.Size = size;
            }
        ).Send());
        if (response?.Response is not { } anidbFile)
            return null;

        var releaseInfo = new ReleaseInfoWithProvider(Name)
        {
            ID = releaseId,
            ProviderID = Name,
            ReleaseURI = $"https://anidb.net/file/{anidbFile.FileID}",
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
                ProviderID = Name,
                Name = string.IsNullOrEmpty(anidbFile.GroupName) ? null : anidbFile.GroupName,
                ShortName = string.IsNullOrEmpty(anidbFile.GroupShortName) ? null : anidbFile.GroupShortName,
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

        // TODO: Do xrefs here

        return releaseInfo;
    }

    public Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
        => GetReleaseInfoById($"{video.Hashes.ED2K}-{video.Size}", cancellationToken);
}
