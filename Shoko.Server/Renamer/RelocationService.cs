using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Renamer;

public class RelocationService : IRelocationService
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger<RelocationService> _logger;

    public RelocationService(ISettingsProvider settingsProvider, ILogger<RelocationService> logger)
    {
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public IImportFolder? GetFirstDestinationWithSpace(RelocationEventArgs args)
    {
        if (_settingsProvider.GetSettings().Import.SkipDiskSpaceChecks)
            return args.AvailableFolders.FirstOrDefault(fldr => fldr.DropFolderType.HasFlag(DropFolderType.Destination));

        return args.AvailableFolders.Where(fldr => fldr.DropFolderType.HasFlag(DropFolderType.Destination) && Directory.Exists(fldr.Path))
            .FirstOrDefault(fldr => ImportFolderHasSpace(fldr, args.File));
    }

    public bool ImportFolderHasSpace(IImportFolder folder, IVideoFile file)
    {
        return folder.ID == file.ImportFolderID || folder.AvailableFreeSpace >= file.Size;
    }

    public (IImportFolder ImportFolder, string RelativePath)? GetExistingSeriesLocationWithSpace(RelocationEventArgs args)
    {
        var series = args.Series.Select(s => s.AnidbAnime).FirstOrDefault();
        if (series is null)
            return null;

        // sort the episodes by air date, so that we will move the file to the location of the latest episode
        var allEps = series.Episodes
            .OrderByDescending(a => a.AirDate ?? DateTime.MinValue)
            .ToList();

        var skipDiskSpaceChecks = _settingsProvider.GetSettings().Import.SkipDiskSpaceChecks;
        foreach (var ep in allEps)
        {
            var videoList = ep.VideoList;
            // check if this episode belongs to more than one anime
            // if it does, we will ignore it
            if (videoList.SelectMany(v => v.Series).DistinctBy(s => s.AnidbAnimeID).Count() > 1)
                continue;

            foreach (var vid in videoList)
            {
                if (vid.Hashes.ED2K == args.File.Video.Hashes.ED2K) continue;

                var place = vid.Locations.FirstOrDefault(b =>
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    b.ImportFolder is not null &&
                    !b.ImportFolder.DropFolderType.HasFlag(DropFolderType.Source) &&
                    !string.IsNullOrWhiteSpace(b.RelativePath));
                if (place is null) continue;

                var placeFld = place.ImportFolder;

                // check space
                if (!skipDiskSpaceChecks && !ImportFolderHasSpace(placeFld, args.File))
                    continue;

                var placeDir = Path.GetDirectoryName(place.Path);
                if (placeDir is null)
                    continue;
                // ensure we aren't moving to the current directory
                if (placeDir.Equals(Path.GetDirectoryName(args.File.Path), StringComparison.InvariantCultureIgnoreCase))
                    continue;

                return (placeFld, Path.GetRelativePath(placeFld.Path, placeDir));
            }
        }

        return null;
    }
}
