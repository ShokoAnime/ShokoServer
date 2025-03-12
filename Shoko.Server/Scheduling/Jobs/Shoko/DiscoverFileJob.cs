using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class DiscoverFileJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IVideoHashingService _videoHashingService;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly VideoLocal_PlaceService _vlPlaceService;
    private readonly ShokoManagedFolderRepository _managedFolders;

    public string FilePath { get; set; }

    public override string TypeName => "Preprocess File";

    public override string Title => "Preprocessing File";
    public override Dictionary<string, object> Details => new()
    {
        {
            "File Path", Utils.GetDistinctPath(FilePath)
        }
    };

    protected DiscoverFileJob() { }

    public DiscoverFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, IVideoHashingService videoHashingService, IVideoReleaseService videoReleaseService, VideoLocal_PlaceService vlPlaceService, ShokoManagedFolderRepository managedFolders)
    {
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _videoHashingService = videoHashingService;
        _videoReleaseService = videoReleaseService;
        _vlPlaceService = vlPlaceService;
        _managedFolders = managedFolders;
    }

    public override async Task Process()
    {
        // The flow has changed.
        // Check for previous existence, merge info if needed.
        // If it's a new file or info is missing, queue a hash.
        // HashFileJob will create the records for a new file, so don't save an empty record.
        _logger.LogInformation("Checking File For Hashes: {Filename}", FilePath);

        var (vlocal, vlocalplace) = GetVideoLocal();
        if (vlocal == null || vlocalplace == null)
        {
            _logger.LogWarning("Could not get VideoLocal. exiting");
            return;
        }

        var filename = vlocalplace.FileName;
        var shouldSave = false;
        if (string.IsNullOrEmpty(vlocal.Hash) && vlocal.FileSize > 0)
        {
            _logger.LogTrace("Missing hashes in VideoLocal (or forced), checking XRefs");
            // try getting the hash from the CrossRef
            if (TrySetHashFromXrefs(filename, vlocal))
            {
                shouldSave = true;
                _logger.LogTrace("Found Hash in CrossRef_File_Episode: {Hash}", vlocal.Hash);
            }
            else if (TrySetHashFromFileNameHash(filename, vlocal))
            {
                shouldSave = true;
                _logger.LogTrace("Found Hash in FileNameHash: {Hash}", vlocal.Hash);
            }
        }

        var enabledHashes = _videoHashingService.AllEnabledHashTypes;
        var shouldHash = vlocal.Hashes is { } hashes && (hashes.Count == 0 || vlocal.Hashes.Any(a => !enabledHashes.Contains(a.Type)));
        var scheduler = await _schedulerFactory.GetScheduler();

        // if !shouldHash, then we definitely have a hash
        var hasXrefs = vlocal.EpisodeCrossReferences is { } xrefs && xrefs.Count > 0 && xrefs.All(a => a.AnimeEpisode is not null && a.AnimeSeries is not null);
        if (!shouldHash && hasXrefs && !vlocal.DateTimeImported.HasValue)
        {
            vlocal.DateTimeImported = DateTime.Now;
            shouldSave = true;
        }

        // this can't run on a new file, because shouldHash is true
        if (!shouldHash && !shouldSave)
        {
            if (hasXrefs)
            {
                _logger.LogTrace("Hashes were not necessary for file, so exiting: {File}, Hash: {Hash}", FilePath, vlocal.Hash);
                return;
            }

            // Don't schedule the auto-match attempt if auto-matching is disabled.
            if (!_videoReleaseService.AutoMatchEnabled)
            {
                _logger.LogTrace("Hashes wer found and xrefs are missing, but auto-match is disabled. Exiting: {File}, Hash: {Hash}", FilePath, vlocal.Hash);
                return;
            }

            _logger.LogTrace("Hashes were found, but xrefs are missing. Queuing a rescan for: {File}, Hash: {Hash}", FilePath, vlocal.Hash);
            await scheduler.StartJobNow<ProcessFileJob>(a => a.VideoLocalID = vlocal.VideoLocalID);
            return;
        }

        // process duplicate import
        var duplicateRemoved = await ProcessDuplicates(vlocal, vlocalplace);
        // it was removed. Don't try to hash or save
        if (duplicateRemoved) return;

        if (shouldSave)
        {
            _logger.LogTrace("Saving VideoLocal: VideoLocalID: {VideoLocalID},  Filename: {FileName}, Hash: {Hash}", vlocal.VideoLocalID, FilePath, vlocal.Hash);
            RepoFactory.VideoLocal.Save(vlocal, true);

            _logger.LogTrace("Saving VideoLocal_Place: VideoLocal_Place_ID: {PlaceID}, Path: {Path}", vlocalplace.ID, FilePath);
            vlocalplace.VideoID = vlocal.VideoLocalID;
            RepoFactory.VideoLocalPlace.Save(vlocalplace);
        }

        if (shouldHash)
        {
            await scheduler.StartJobNow<HashFileJob>(a =>
            {
                a.FilePath = FilePath;
            });
            return;
        }

        // Only schedule the auto-match attempt if auto-matching is enabled.
        if (!hasXrefs)
        {
            if (!_videoReleaseService.AutoMatchEnabled)
            {
                _logger.LogTrace("Hashes were found and xrefs are missing, but auto-match is disabled. Exiting: {File}, Hash: {Hash}", FilePath, vlocal.Hash);
                return;
            }

            _logger.LogTrace("Hashes were found, but xrefs are missing. Queuing a rescan for: {File}, Hash: {Hash}", FilePath, vlocal.Hash);
            await scheduler.StartJobNow<ProcessFileJob>(a => a.VideoLocalID = vlocal.VideoLocalID);
        }
    }

    private (VideoLocal?, VideoLocal_Place?) GetVideoLocal()
    {
        // hash and read media info for file
        var (folder, filePath) = _managedFolders.GetFromAbsolutePath(FilePath);
        if (folder == null)
        {
            _logger.LogError("Unable to locate Managed Folder for {FileName}", FilePath);
            return default;
        }

        if (!File.Exists(FilePath))
        {
            _logger.LogError("File does not exist: {Filename}", FilePath);
            return default;
        }

        // check if we have already processed this file
        var folderID = folder.ID;
        var vlocalplace = RepoFactory.VideoLocalPlace.GetByRelativePathAndManagedFolderID(filePath, folderID);
        VideoLocal? vlocal = null;

        if (vlocalplace != null)
        {
            vlocal = vlocalplace.VideoLocal;
            if (vlocal != null)
            {
                _logger.LogTrace("VideoLocal record found in database: {Filename}", FilePath);

                // This will only happen with DB corruption, so just clean up the mess.
                if (vlocalplace.Path == null)
                {
                    _logger.LogTrace("VideoLocal_Place path is non-existent, removing it");
                    if (vlocal.Places.Count == 1)
                    {
                        RepoFactory.VideoLocal.Delete(vlocal);
                        vlocal = null;
                    }

                    RepoFactory.VideoLocalPlace.Delete(vlocalplace);
                    vlocalplace = null;
                }
            }
        }

        if (vlocal == null)
        {
            _logger.LogTrace("No existing VideoLocal, creating temporary record");
            vlocal = new VideoLocal
            {
                DateTimeUpdated = DateTime.Now,
                DateTimeCreated = DateTime.Now,
                FileName = Path.GetFileName(filePath),
                Hash = string.Empty,
            };
        }

        if (vlocalplace == null)
        {
            _logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            vlocalplace = new VideoLocal_Place
            {
                RelativePath = filePath,
                ManagedFolderID = folderID,
            };
            if (vlocal.VideoLocalID != 0) vlocalplace.VideoID = vlocal.VideoLocalID;
        }

        return (vlocal, vlocalplace);
    }

    private bool TrySetHashFromXrefs(string filename, VideoLocal vlocal)
    {
        var crossRefs = RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (crossRefs.Count == 0)
            return false;

        vlocal.Hash = crossRefs[0].Hash;
        vlocal.HashSource = (int)HashSource.DirectHash;

        var hash = RepoFactory.VideoLocalHashDigest.GetByVideoIDAndHashType(vlocal.VideoLocalID, "ED2K") is { Count: > 0 } hashDigests
            ? hashDigests[0]
            : new() { VideoLocalID = vlocal.VideoLocalID, Type = "ED2K" };
        hash.Value = crossRefs[0].Hash;
        RepoFactory.VideoLocalHashDigest.Save(hash);

        _logger.LogTrace("Got hash from xrefs: {Filename} ({Hash})", FilePath, crossRefs[0].Hash);
        return true;
    }

    private bool TrySetHashFromFileNameHash(string filename, VideoLocal vlocal)
    {
        // TODO support reading MD5 and SHA1 from files via the standard way
        var hashes = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (hashes is { Count: > 1 })
        {
            // if we have more than one record it probably means there is some sort of corruption
            // lets delete the local records
            foreach (var fnh in hashes)
            {
                RepoFactory.FileNameHash.Delete(fnh.FileNameHashID);
            }
        }

        // re-init this to check if we erased them
        hashes = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);

        if (hashes is not { Count: 1 }) return false;

        _logger.LogTrace("Got hash from LOCAL cache: {Filename} ({Hash})", FilePath, hashes[0].Hash);
        vlocal.Hash = hashes[0].Hash;
        vlocal.HashSource = (int)HashSource.FileNameCache;

        var hash = RepoFactory.VideoLocalHashDigest.GetByVideoIDAndHashType(vlocal.VideoLocalID, "ED2K") is { Count: > 0 } hashDigests
            ? hashDigests[0]
            : new() { VideoLocalID = vlocal.VideoLocalID, Type = "ED2K" };
        hash.Value = hashes[0].Hash;
        RepoFactory.VideoLocalHashDigest.Save(hash);

        return true;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="vlocal"></param>
    /// <param name="vlocalplace"></param>
    /// <returns>true if the file was removed</returns>
    private async Task<bool> ProcessDuplicates(VideoLocal vlocal, VideoLocal_Place vlocalplace)
    {
        // If the VideoLocalID == 0, then it's a new file that wasn't merged after hashing, so it can't be a dupe
        if (vlocal.VideoLocalID == 0) return false;

        // remove missing files
        var preps = vlocal.Places.Where(a =>
        {
            if (string.Equals(vlocalplace.Path, a.Path)) return false;
            if (a.Path == null) return true;
            return !File.Exists(a.Path);
        }).ToList();
        RepoFactory.VideoLocalPlace.Delete(preps);

        var dupPlace = vlocal.Places.FirstOrDefault(a => !string.Equals(vlocalplace.Path, a.Path));
        if (dupPlace == null) return false;

        _logger.LogWarning("Found Duplicate File");
        _logger.LogWarning("---------------------------------------------");
        _logger.LogWarning("New File: {FullServerPath}", vlocalplace.Path);
        _logger.LogWarning("Existing File: {FullServerPath}", dupPlace.Path);
        _logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (!settings.Import.AutomaticallyDeleteDuplicatesOnImport) return false;

        await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(vlocalplace);
        return true;
    }
}
