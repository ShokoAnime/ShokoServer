using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Iesi.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Formatting = Newtonsoft.Json.Formatting;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_SyncMyList)]
public class CommandRequest_SyncMyList : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;
    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

    public override QueueStateStruct PrettyDescription => new QueueStateStruct
    {
        message = "Syncing MyList info from HTTP API",
        queueState = QueueStateEnum.SyncMyList,
        extraParams = new string[0]
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_SyncMyList");

        try
        {
            if (!ShouldRun()) return;

            // Get the list from AniDB
            var request = _requestFactory.Create<RequestMyList>(
                r =>
                {
                    r.Username = ServerSettings.Instance.AniDb.Username;
                    r.Password = ServerSettings.Instance.AniDb.Password;
                }
            );
            var response = request.Execute();

            if (response.Response == null)
            {
                Logger.LogWarning("AniDB did not return a successful code: {Code}", response.Code);
                return;
            }

            var serialized = JsonConvert.SerializeObject(response.Response, Formatting.Indented);
            Directory.CreateDirectory(ServerSettings.Instance.MyListDirectory);
            File.WriteAllText(Path.Join(ServerSettings.Instance.MyListDirectory, "mylist.json"), serialized);

            var totalItems = 0;
            var watchedItems = 0;
            var modifiedItems = 0;

            // Add missing files on AniDB
            // these patterns have been tested
            var onlineFiles = response.Response.Where(a => a.FileID is not null or 0).ToLookup(a => a.FileID.Value);
            var localFiles = RepoFactory.AniDB_File.GetAll().ToLookup(a => a.Hash);

            var missingFiles = AddMissingFiles(localFiles, onlineFiles);

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
                        modifiedItems = ProcessStates(aniDBUsers, vl, myItem, modifiedItems, modifiedSeries);
                        continue;
                    }

                    // We don't have the file
                    // If it's local only, then we don't update. The rest update in one way or another
                    if (ServerSettings.Instance.AniDb.MyList_DeleteType == AniDBFileDeleteType.DeleteLocalOnly)
                        continue;
                    filesToRemove.Add(myItem.FileID.Value);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "A MyList Item threw an error while syncing: {Ex}", ex);
                }
            }

            // Actually remove the files
            if (filesToRemove.Count > 0)
            {
                foreach (var id in filesToRemove)
                {
                    var deleteCommand =
                        _commandFactory.Create<CommandRequest_DeleteFileFromMyList>(a => a.FileID = id);
                    deleteCommand.Save();
                }
            }

            if (filesToRemove.Count > 0)
                Logger.LogInformation("MYLIST Missing Files: {Count} added to queue for deletion",
                    filesToRemove.Count);

            modifiedSeries.ForEach(a => a.QueueUpdateStats());

            Logger.LogInformation(
                "Process MyList: {TotalItems} Items, {MissingFiles} Added, {Count} Deleted, {WatchedItems} Watched, {ModifiedItems} Modified",
                totalItems, missingFiles, filesToRemove.Count, watchedItems, modifiedItems);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_SyncMyList: {Ex} ", ex);
        }
    }

    private int ProcessStates(List<SVR_JMMUser> aniDBUsers, SVR_VideoLocal vl, ResponseMyList myitem,
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

        var localState = ServerSettings.Instance.AniDb.MyList_StorageState;
        var shouldUpdate = false;
        var updateDate = myitem.ViewedAt;

        // we don't support multiple AniDB accounts, so we can just only iterate to set states
        if (ServerSettings.Instance.AniDb.MyList_ReadWatched && localWatchedDate == null && updateDate != null)
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
        else if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched && localWatchedDate != null && updateDate == null)
        {
            foreach (var juser in aniDBUsers)
            {
                modifiedItems++;
                vl.ToggleWatchedStatus(false, false, null, false, juser.JMMUserID, false, true);
                vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null)
                    .DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
            }
        }
        else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched && localWatchedDate == null && updateDate != null)
        {
            shouldUpdate = true;
            updateDate = null;
        }
        else if (ServerSettings.Instance.AniDb.MyList_SetWatched && localWatchedDate != null && !localWatchedDate.Equals(updateDate))
        {
            shouldUpdate = true;
            updateDate = localWatchedDate.Value.ToUniversalTime();
        }

        // check if the state needs to be updated
        if ((int)myitem.State != (int)localState) shouldUpdate = true;

        if (!shouldUpdate) return modifiedItems;

        var updateCommand =
            _commandFactory.Create<CommandRequest_UpdateMyListFileStatus>(a =>
            {
                a.Hash = vl.Hash;
                a.Watched = updateDate != null;
                a.WatchedDateAsSecs = Commons.Utils.AniDB.GetAniDBDateAsSeconds(updateDate);
                a.UpdateSeriesStats = false;
            });
        updateCommand.Save();

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
            var freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyList_UpdateFrequency);

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

    private int AddMissingFiles(ILookup<string, SVR_AniDB_File> localFiles,
        ILookup<int, ResponseMyList> onlineFiles)
    {
        if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return 0;
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

            var cmdAddFile = _commandFactory.Create<CommandRequest_AddFileToMyList>(a => a.Hash = vid.Hash);
            cmdAddFile.Save();
        }

        Logger.LogInformation(
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

    public override void GenerateCommandID()
    {
        CommandID = "CommandRequest_SyncMyList";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_SyncMyList(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
    }

    protected CommandRequest_SyncMyList() { }
}
