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
}
