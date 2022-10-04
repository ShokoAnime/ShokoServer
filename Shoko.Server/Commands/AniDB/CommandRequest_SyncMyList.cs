using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Iesi.Collections.Generic;
using Microsoft.Extensions.Logging;
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
#if !DEBUG
        throw new NotSupportedException("I'm working on it...");
#endif

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

            var totalItems = 0;
            var watchedItems = 0;
            var modifiedItems = 0;

            // Add missing files on AniDB
            var onlineFiles = response.Response.Where(a => a.FileID.HasValue).ToLookup(a => a.FileID);
            var onlineEpisodes = response.Response
                .Where(a => !a.FileID.HasValue && a.AnimeID.HasValue && a.EpisodeID.HasValue)
                .ToLookup(a => (a.AnimeID, a.EpisodeID));
            var dictAniFiles = RepoFactory.AniDB_File.GetAll().ToLookup(a => a.Hash);
            var dictAniEps = RepoFactory.CrossRef_File_Episode.GetAll().Where(a => !dictAniFiles.Contains(a.Hash))
                .ToLookup(a => a.Hash);

            var missingFiles = AddMissingFiles(dictAniFiles, onlineFiles, dictAniEps, onlineEpisodes);

            var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
            var modifiedSeries = new LinkedHashSet<SVR_AnimeSeries>();

            // Remove Missing Files and update watched states (single loop)
            var filesToRemove = new List<int>();
            var myListIDsToRemove = new List<int>();
            foreach (var myitem in response.Response)
            {
                try
                {
                    totalItems++;
                    if (myitem.ViewedAt.HasValue) watchedItems++;

                    var hash = string.Empty;

                    var anifile = myitem.FileID == null
                        ? null
                        : RepoFactory.AniDB_File.GetByFileID(myitem.FileID.Value);
                    if (anifile != null)
                    {
                        hash = anifile.Hash;
                    }
                    else
                    {
                        // look for manually linked files
                        var xrefs = myitem.EpisodeID == null
                            ? null
                            : RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID.Value);
                        hash = xrefs.FirstOrDefault(xref => xref.CrossRefSource != (int)CrossRefSource.AniDB)?.Hash;
                    }

                    var vl = hash == null ? null : RepoFactory.VideoLocal.GetByHash(hash);
                    // If there's no video local, we don't have it
                    if (vl == null)
                    {
                        if (myitem.MyListID.HasValue)
                            myListIDsToRemove.Add(myitem.MyListID.Value);
                        else if (myitem.FileID.HasValue)
                        {
                            filesToRemove.Add(myitem.FileID.Value);
                        }

                        continue;
                    }

                    modifiedItems = ProcessStates(aniDBUsers, vl, myitem, modifiedItems, modifiedSeries);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"A MyList Item threw an error while syncing: {ex}");
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

            if (myListIDsToRemove.Count > 0)
            {
                foreach (var lid in myListIDsToRemove)
                {
                    var deleteCommand =
                        _commandFactory.Create<CommandRequest_DeleteFileFromMyList>(a => a.MyListID = lid);
                    deleteCommand.Save();
                }
            }

            if (myListIDsToRemove.Count + filesToRemove.Count > 0)
                Logger.LogInformation("MYLIST Missing Files: {Count} added to queue for deletion",
                    myListIDsToRemove.Count + filesToRemove.Count);

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
            .MaxBy(a => a.WatchedDate)?.WatchedDate;
        var localState = ServerSettings.Instance.AniDb.MyList_StorageState;
        foreach (var juser in aniDBUsers)
        {
            var localStatus = false;

            // doesn't matter which anidb user we use
            var jmmUserID = juser.JMMUserID;
            var userRecord = vl.GetUserRecord(juser.JMMUserID);
            if (userRecord != null) localStatus = userRecord.WatchedDate.HasValue;

            var action = string.Empty;
            if (localStatus == myitem.ViewedAt.HasValue) continue;

            // localStatus and AniDB Status are different
            DateTime? watchedDate = myitem.ViewedAt ?? DateTime.Now;
            if (localStatus)
            {
                // local = watched, anidb = unwatched
                if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched)
                {
                    modifiedItems++;
                    vl.ToggleWatchedStatus(false, false, watchedDate,
                        false, jmmUserID, false,
                        true);
                    action = "Used AniDB Status";
                }
                else if (ServerSettings.Instance.AniDb.MyList_SetWatched)
                {
                    vl.ToggleWatchedStatus(true, true, userRecord.WatchedDate, false, jmmUserID,
                        false, true);
                }
            }
            else
            {
                // means local is un-watched, and anidb is watched
                if (ServerSettings.Instance.AniDb.MyList_ReadWatched)
                {
                    modifiedItems++;
                    vl.ToggleWatchedStatus(true, false, watchedDate, false,
                        jmmUserID, false, true);
                    action = "Updated Local record to Watched";
                }
                else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched)
                {
                    vl.ToggleWatchedStatus(false, true, watchedDate, false, jmmUserID,
                        false, true);
                }
            }

            vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null)
                .DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
            Logger.LogInformation(
                $"MYLISTDIFF:: File {vl.FileName} - Local Status = {localStatus}, AniDB Status = {myitem.ViewedAt.HasValue} --- {action}");
        }

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

    private int AddMissingFiles(ILookup<string, SVR_AniDB_File> dictAniFiles,
        ILookup<int?, ResponseMyList> onlineFiles, ILookup<string, CrossRef_File_Episode> dictAniEps,
        ILookup<(int? AnimeID, int? EpisodeID), ResponseMyList> onlineEpisodes)
    {
        var missingFiles = 0;
        var missingEps = 0;
        foreach (var vid in RepoFactory.VideoLocal.GetAll()
                     .Where(a => !string.IsNullOrEmpty(a.Hash)).ToList())
        {
            // Does it have a linked AniFile
            if (TryGetFileID(dictAniFiles, vid.Hash, out var fileID))
            {
                // Is it in MyList
                if (onlineFiles.Contains(fileID)) continue;

                // means we have found a file in our local collection, which is not recorded online
                if (!ServerSettings.Instance.AniDb.MyList_AddFiles) continue;
                missingFiles++;
            }
            else if (TryGetEpisode(dictAniEps, vid.Hash, out var episodeXrefs))
            {
                foreach (var (animeID, episodeID) in episodeXrefs)
                {
                    // Is it in MyList
                    if (onlineEpisodes.Contains((animeID, episodeID))) continue;

                    // means we have found a file in our local collection, which is not recorded online
                    if (!ServerSettings.Instance.AniDb.MyList_AddFiles) continue;
                    missingEps++;
                }
            }

            var cmdAddFile = _commandFactory.Create<CommandRequest_AddFileToMyList>(a => a.Hash = vid.Hash);
            cmdAddFile.Save();
        }

        Logger.LogInformation(
            "MYLIST Missing Files: {MissingFiles} Missing Episodes: {MissingEps} Added to queue for inclusion",
            missingFiles, missingEps);
        return missingFiles;
    }

    private static bool TryGetFileID(ILookup<string, SVR_AniDB_File> dictAniFiles, string hash, out int fileID)
    {
        fileID = 0;
        if (!dictAniFiles.Contains(hash)) return false;
        var file = dictAniFiles[hash].FirstOrDefault();
        if (file == null) return false;
        if (file.FileID == 0) return false;
        fileID = file.FileID;
        return true;
    }

    private static bool TryGetEpisode(ILookup<string, CrossRef_File_Episode> dictAniEps, string hash,
        out IReadOnlyList<(int AnimeID, int EpisodeID)> Episodes)
    {
        var output = new List<(int AnimeID, int EpisodeID)>();
        Episodes = output;
        if (!dictAniEps.Contains(hash)) return false;
        var xrefs = dictAniEps[hash];
        foreach (var xref in xrefs)
        {
            if (xref == null) continue;
            output.Add((xref.AnimeID, xref.EpisodeID));
        }

        return output.Any();
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
        if (CommandDetails.Trim().Length > 0)
        {
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));
        }

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
}
