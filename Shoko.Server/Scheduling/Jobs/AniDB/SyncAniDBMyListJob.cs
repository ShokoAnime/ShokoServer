using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Iesi.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class SyncAniDBMyListJob : BaseJob
{
    // TODO make this use Quartz scheduling
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServerSettings _settings;

    public bool ForceRefresh { get; set; }

    public override string TypeName => "Sync AniDB MyList";

    public override string Title => "Syncing AniDB MyList";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncAniDBMyListJob));

        if (!ShouldRun()) return;

        // Get the list from AniDB
        var request = _requestFactory.Create<RequestMyList>(
            r =>
            {
                r.Username = _settings.AniDb.Username;
                r.Password = _settings.AniDb.Password;
            }
        );
        var response = request.Send();

        if (response.Response == null)
        {
            _logger.LogWarning("AniDB did not return a successful code: {Code}", response.Code);
            return;
        }

        await CreateMyListBackup(response);

        var totalItems = 0;
        var watchedItems = 0;
        var modifiedItems = 0;

        // Add missing files on AniDB
        // these patterns have been tested
        var onlineFiles = response.Response.Where(a => a.FileID is not null and not 0).ToLookup(a => a.FileID.Value);
        var localFiles = RepoFactory.AniDB_File.GetAll().ToLookup(a => a.Hash);

        var missingFiles = await AddMissingFiles(localFiles, onlineFiles);

        var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
        var modifiedSeries = new LinkedHashSet<SVR_AnimeSeries>();

        // Remove Missing Files and update watched states (single loop)
        var filesToRemove = new HashSet<int>();

        foreach (var myItem in onlineFiles.SelectMany(a => a))
        {
            try
            {
                totalItems++;
                if (myItem.ViewedAt.HasValue) watchedItems++;

                // the null is checked in the collection
                var aniFile = RepoFactory.AniDB_File.GetByFileID(myItem!.FileID!.Value);

                // the AniDB_File should never have a null hash, but just in case
                var vl = aniFile?.Hash == null ? null : RepoFactory.VideoLocal.GetByHash(aniFile.Hash);

                if (vl != null)
                {
                    // We have it, so process watched states and update storage states if needed
                    modifiedItems = await ProcessStates(aniDBUsers, vl, myItem, modifiedItems, modifiedSeries);
                    continue;
                }

                // we don't have it by hash. It could be generic. We can try to sync by episode
                // For now, we're just skipping them
                // TODO actually handle the generic files where possible
                // if it has a FileState other than Normal, it's a generic file.
                if (myItem.FileState != MyList_FileState.Normal) continue;

                // We don't have the file
                // If it's local only, then we don't update. The rest update in one way or another
                if (_settings.AniDb.MyList_DeleteType == AniDBFileDeleteType.DeleteLocalOnly)
                    continue;
                filesToRemove.Add(myItem.FileID.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A MyList Item threw an error while syncing");
            }
        }

        // Actually remove the files
        if (filesToRemove.Count > 0)
        {
            foreach (var id in filesToRemove)
            {
                await (await _schedulerFactory.GetScheduler()).StartJob<DeleteFileFromMyListJob>(a => a.FileID = id);
            }
        }

        if (filesToRemove.Count > 0)
            _logger.LogInformation("MYLIST Missing Files: {Count} added to queue for deletion",
                filesToRemove.Count);

        modifiedSeries.ForEach(a => a.QueueUpdateStats());

        _logger.LogInformation(
            "Process MyList: {TotalItems} Items, {MissingFiles} Added, {Count} Deleted, {WatchedItems} Watched, {ModifiedItems} Modified",
            totalItems, missingFiles, filesToRemove.Count, watchedItems, modifiedItems);
    }

    private async Task CreateMyListBackup(HttpResponse<List<ResponseMyList>> response)
    {
        var serialized = JsonConvert.SerializeObject(response.Response, Formatting.Indented);
        var myListDirectory = new DirectoryInfo(Utils.MyListDirectory);
        myListDirectory.Create();

        var currentBackupPath = Path.Join(myListDirectory.FullName, "mylist.json");
        await File.WriteAllTextAsync(currentBackupPath, serialized);

        // Create timestamped MyList zip archive
        // Backup rotation depends on filename being universally sortable ("u" format specifier)
        var archivePath = Path.Join(myListDirectory.FullName, DateTimeOffset.UtcNow.ToString("u").Replace(':', '_') + ".zip");
        await using var backupFs = new FileStream(archivePath, FileMode.OpenOrCreate);
        using var archive = new ZipArchive(backupFs, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(currentBackupPath, Path.GetFileName(currentBackupPath));

        // Delete oldest backups when more than 30 exist
        // Only gets zip files that start with ISO 8601 date format YYYY-MM-DD
        var backupFiles = myListDirectory.GetFiles("????-??-?? *.zip").OrderByDescending(f => f.Name).ToList();
        var backUpFilesToDelete = backupFiles.Skip(_settings.AniDb.MyList_RetainedBackupCount).ToList();
        foreach (var file in backUpFilesToDelete)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // ignored
            }
        }
    }

    private async Task<int> ProcessStates(List<SVR_JMMUser> aniDBUsers, SVR_VideoLocal vl, ResponseMyList myitem,
        int modifiedItems, ISet<SVR_AnimeSeries> modifiedSeries)
    {
        // check watched states, read the states if needed, and update differences
        // aggregate and assume if one AniDB User has watched it, it should be marked
        // if multiple have, then take the latest
        // compare the states and update if needed
        var localWatchedDate = aniDBUsers.Select(a => vl.GetUserRecord(a.JMMUserID)).Where(a => a?.WatchedDate != null)
            .Max(a => a.WatchedDate);
        if (localWatchedDate is not null && localWatchedDate.Value.Millisecond > 0)
            localWatchedDate = localWatchedDate.Value.AddMilliseconds(-localWatchedDate.Value.Millisecond);

        var localState = _settings.AniDb.MyList_StorageState;
        var shouldUpdate = false;
        var updateDate = myitem.ViewedAt;

        // we don't support multiple AniDB accounts, so we can just only iterate to set states
        if (_settings.AniDb.MyList_ReadWatched && localWatchedDate == null && updateDate != null)
        {
            foreach (var juser in aniDBUsers)
            {
                var watchedDate = myitem.ViewedAt;
                modifiedItems++;
                vl.ToggleWatchedStatus(true, false, watchedDate, false, juser.JMMUserID, false, true);
                vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null)
                    .DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
            }
        }
        // if we did the previous, then we don't want to undo it
        else if (_settings.AniDb.MyList_ReadUnwatched && localWatchedDate != null && updateDate == null)
        {
            foreach (var juser in aniDBUsers)
            {
                modifiedItems++;
                vl.ToggleWatchedStatus(false, false, null, false, juser.JMMUserID, false, true);
                vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null)
                    .DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
            }
        }
        else if (_settings.AniDb.MyList_SetUnwatched && localWatchedDate == null && updateDate != null)
        {
            shouldUpdate = true;
            updateDate = null;
        }
        else if (_settings.AniDb.MyList_SetWatched && localWatchedDate != null && !localWatchedDate.Equals(updateDate))
        {
            shouldUpdate = true;
            updateDate = localWatchedDate.Value.ToUniversalTime();
        }

        // check if the state needs to be updated
        if ((int)myitem.State != (int)localState) shouldUpdate = true;

        if (!shouldUpdate) return modifiedItems;

        await (await _schedulerFactory.GetScheduler()).StartJob<UpdateMyListFileStatusJob>(a =>
        {
            a.Hash = vl.Hash;
            a.Watched = updateDate != null;
            a.WatchedDateAsSecs = Commons.Utils.AniDB.GetAniDBDateAsSeconds(updateDate);
            a.UpdateSeriesStats = false;
        });

        return modifiedItems;
    }

    private bool ShouldRun()
    {
        // we will always assume that an anime was downloaded via http first
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.AniDBMyListSync, UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(_settings.AniDb.MyList_UpdateFrequency);

            // if we have run this in the last 24 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!ForceRefresh) return false;
            }
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

        return true;
    }

    private async Task<int> AddMissingFiles(ILookup<string, SVR_AniDB_File> localFiles,
        ILookup<int, ResponseMyList> onlineFiles)
    {
        if (!_settings.AniDb.MyList_AddFiles) return 0;
        var missingFiles = 0;
        foreach (var vid in RepoFactory.VideoLocal.GetAll()
                     .Where(a => !string.IsNullOrEmpty(a.Hash)).ToList())
        {
            // Does it have a linked AniFile
            if (TryGetFileID(localFiles, vid.Hash, out var fileID))
            {
                // Is it in MyList
                if (onlineFiles.Contains(fileID)) continue;

                // means we have found a file in our local collection, which is not recorded online
                missingFiles++;
            }
            else continue;

            await (await _schedulerFactory.GetScheduler()).StartJob<AddFileToMyListJob>(a => a.Hash = vid.Hash);
        }

        _logger.LogInformation(
            "MYLIST Missing Files: {MissingFiles} Added to queue for inclusion",
            missingFiles);
        return missingFiles;
    }

    private static bool TryGetFileID(ILookup<string, SVR_AniDB_File> localFiles, string hash, out int fileID)
    {
        fileID = 0;
        if (!localFiles.Contains(hash)) return false;
        var file = localFiles[hash].FirstOrDefault();
        if (file == null) return false;
        if (file.FileID == 0) return false;
        fileID = file.FileID;
        return true;
    }

    public SyncAniDBMyListJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected SyncAniDBMyListJob()
    {
    }
}
