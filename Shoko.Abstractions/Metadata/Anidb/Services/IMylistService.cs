using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata.Anidb.Services;

/// <summary>
///   Service for interacting with the AniDB MyList. All MyList operations,
///   whether immediate or scheduled through the job queue, should be routed
///   through this service.
/// </summary>
public interface IMylistService
{
    /// <summary>
    ///   The default fetch mode used when a method is called without an
    ///   explicit fetch mode.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the mode is set to <see cref="MylistFetchMode.Auto"/>.
    /// </exception>
    MylistFetchMode FetchMode { get; set; }

    #region Fetch

    /// <summary>
    ///   Gets the full MyList for the configured AniDB user. Serves the
    ///   locally cached copy when it is fresh, fetching from AniDB only
    ///   when the cache is stale or empty.
    /// </summary>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   A read-only list of all MyList entries for the configured user.
    /// </returns>
    Task<IReadOnlyList<MylistEntry>> GetEntriesAsync(MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets a single MyList entry by its list ID (lid). Returns the
    ///   locally cached entry when present, fetching from AniDB otherwise.
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if no entry exists for the given
    ///   list ID.
    /// </returns>
    Task<MylistEntry?> GetEntryAsync(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets a single MyList entry by the file's ED2K hash and size.
    ///   Returns the locally cached entry when present, fetching from AniDB
    ///   otherwise.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if no entry exists for the given
    ///   ED2K hash and size.
    /// </returns>
    Task<MylistEntry?> GetEntryAsync(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets a single MyList entry by the file's ID (fid). Returns the
    ///   locally cached entry when present, fetching from AniDB otherwise.
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if no entry exists for the given
    ///   file ID.
    /// </returns>
    Task<MylistEntry?> GetEntryAsync(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets a single generic MyList entry by its anime ID, episode type,
    ///   and episode number. Returns the locally cached entry when present,
    ///   fetching from AniDB otherwise.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if no entry exists for the given
    ///   anime ID, episode type, and episode number.
    /// </returns>
    Task<MylistEntry?> GetEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets the MyList entries tied to the given video. Files with an
    ///   AniDB release are represented by their file entry, while files
    ///   with manual links are represented by the generic entries of each
    ///   linked episode.
    /// </summary>
    /// <param name="video">
    ///   The video to get the MyList entries for.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entries tied to the video, or an empty list if the
    ///   video could not be found or has no entries.
    /// </returns>
    Task<IReadOnlyList<MylistEntry>> GetEntriesForVideoAsync(IVideo video, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    #endregion

    #region Add

    /// <summary>
    ///   Adds the file with the given file ID (fid) to the MyList, updating
    ///   the existing entry if one is already present. Skipped when the
    ///   locally cached entry is already in the desired state unless the
    ///   fetch mode omits the <see cref="MylistFetchMode.Cache"/> flag.
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default. Without the <see cref="MylistFetchMode.Cache"/> flag the
    ///   cached entry is bypassed and the command is always sent to AniDB.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if the file could not be found.
    /// </returns>
    Task<MylistEntry?> AddEntryAsync(int fileID, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to add the file with the given file ID (fid) to the
    ///   MyList.
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleAddEntry(int fileID, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Adds the file with the given ED2K hash and size to the MyList,
    ///   updating the existing entry if one is already present. Skipped
    ///   when the locally cached entry is already in the desired state
    ///   unless the fetch mode omits the <see cref="MylistFetchMode.Cache"/>
    ///   flag.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if the file could not be found.
    /// </returns>
    Task<MylistEntry?> AddEntryAsync(string ed2k, long fileSize, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to add the file with the given ED2K hash and size
    ///   to the MyList.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleAddEntry(string ed2k, long fileSize, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Adds a generic MyList entry for the given anime ID, episode type,
    ///   and episode number, updating the existing entry if one is already
    ///   present. Skipped when the locally cached entry is already in the
    ///   desired state unless the fetch mode omits the
    ///   <see cref="MylistFetchMode.Cache"/> flag.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if the episode could not be found.
    /// </returns>
    Task<MylistEntry?> AddEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to add a generic MyList entry for the given anime
    ///   ID, episode type, and episode number.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleAddEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Adds the given video to the MyList. Files with an AniDB release
    ///   are added as regular entries, while files with manual links are
    ///   added as generic entries for each linked episode. Also imports the
    ///   watched state in either direction when requested, and updates the
    ///   local MyList ID of the video. Skipped when the locally cached
    ///   entry is already in the desired state unless the fetch mode omits
    ///   the <see cref="MylistFetchMode.Cache"/> flag.
    /// </summary>
    /// <param name="video">
    ///   The video to add to the MyList.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="readStates">
    ///   Optional. Which watched states to import from AniDB into the local
    ///   user data. Defaults to <see cref="MylistReadStates.Auto"/>, which
    ///   uses the configured defaults.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <exception cref="Exception">
    ///   Thrown when the <paramref name="video"/> does not exist locally.
    /// </exception>
    /// <returns>
    ///   The MyList entry, or <c>null</c> if the video could not be found.
    /// </returns>
    Task<MylistEntry?> AddVideoAsync(IVideo video, MylistAddData? data = null, MylistReadStates readStates = MylistReadStates.Auto, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to add the given video to the MyList. Files with a
    ///   release from AniDB are added as regular entries, while files with
    ///   manual links are added as generic entries for each linked episode.
    /// </summary>
    /// <param name="video">
    ///   The video to add to the MyList.
    /// </param>
    /// <param name="data">
    ///   Optional. The add data to apply to the entry.
    /// </param>
    /// <param name="readStates">
    ///   Optional. Which watched states to import from AniDB into the local
    ///   user data. Defaults to <see cref="MylistReadStates.Auto"/>, which
    ///   uses the configured defaults.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleAddVideo(IVideo video, MylistAddData? data = null, MylistReadStates readStates = MylistReadStates.Auto, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Enqueues jobs to add all files with manual links to the MyList.
    /// </summary>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleAddAllManualLinks();

    #endregion

    #region Update

    /// <summary>
    ///   Updates the watched state and storage state of the MyList entry
    ///   with the given list ID (lid). Skipped when the locally cached
    ///   entry is already in the desired state unless the fetch mode omits
    ///   the <see cref="MylistFetchMode.Cache"/> flag. An update data with
    ///   no fields set is a no-op.
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="data">
    ///   The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The updated MyList entry, or <c>null</c> if the entry could not be
    ///   found.
    /// </returns>
    Task<MylistEntry?> UpdateEntryAsync(ulong listID, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to update the watched state and storage state of
    ///   the MyList entry with the given list ID (lid).
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="data">
    ///   Optional. The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleUpdateEntry(ulong listID, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Updates the watched state and storage state of the MyList entry
    ///   for the file with the given file ID (fid). Skipped when the
    ///   locally cached entry is already in the desired state unless the
    ///   fetch mode omits the <see cref="MylistFetchMode.Cache"/> flag. An
    ///   update data with no fields set is a no-op.
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="data">
    ///   The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The updated MyList entry, or <c>null</c> if the entry could not be
    ///   found.
    /// </returns>
    Task<MylistEntry?> UpdateEntryAsync(int fileID, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to update the watched state and storage state of
    ///   the MyList entry for the file with the given file ID (fid).
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="data">
    ///   Optional. The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleUpdateEntry(int fileID, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Updates the watched state and storage state of the file with the
    ///   given ED2K hash and size in the MyList. Skipped when the locally
    ///   cached entry is already in the desired state unless the fetch mode
    ///   omits the <see cref="MylistFetchMode.Cache"/> flag. An update data
    ///   with no fields set is a no-op.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="data">
    ///   The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The updated MyList entry, or <c>null</c> if the file could not be
    ///   found.
    /// </returns>
    Task<MylistEntry?> UpdateEntryAsync(string ed2k, long fileSize, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to update the watched state and storage state of
    ///   the file with the given ED2K hash and size in the MyList.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="data">
    ///   Optional. The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleUpdateEntry(string ed2k, long fileSize, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Updates the watched state and storage state of a generic MyList
    ///   entry identified by anime ID, episode type, and episode number.
    ///   Skipped when the locally cached entry is already in the desired
    ///   state unless the fetch mode omits the
    ///   <see cref="MylistFetchMode.Cache"/> flag. An update data with no
    ///   fields set is a no-op.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="data">
    ///   The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The updated MyList entry, or <c>null</c> if the entry could not be
    ///   found.
    /// </returns>
    Task<MylistEntry?> UpdateEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to update the watched state and storage state of a
    ///   generic MyList entry identified by anime ID, episode type, and
    ///   episode number.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="data">
    ///   Optional. The update data to apply to the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleUpdateEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Updates the watched state and storage state of the given video in
    ///   the MyList. Files with an AniDB release are updated as regular
    ///   entries, while files with manual links are updated as generic
    ///   entries for each linked episode. Skipped when the locally cached
    ///   entry is already in the desired state unless the fetch mode omits
    ///   the <see cref="MylistFetchMode.Cache"/> flag. An update data with
    ///   no fields set is a no-op.
    /// </summary>
    /// <param name="video">
    ///   The video to update in the MyList.
    /// </param>
    /// <param name="data">
    ///   The update data to apply to the entry.
    /// </param>
    /// <param name="updateSeriesStats">
    ///   Optional. When set to <c>true</c>, will update the series stats
    ///   after the update. If doing multiple updates on the same series at
    ///   once, it is recommended to set this to <c>false</c> for all but
    ///   the last update.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   The updated MyList entry, or <c>null</c> if the video could not be
    ///   found.
    /// </returns>
    Task<MylistEntry?> UpdateVideoAsync(IVideo video, MylistUpdateData data, bool updateSeriesStats = false, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to update the watched state and storage state of
    ///   the given video in the MyList. Files with an AniDB release are
    ///   updated as regular entries, while files with manual links are
    ///   updated as generic entries for each linked episode.
    /// </summary>
    /// <param name="video">
    ///   The video to update in the MyList.
    /// </param>
    /// <param name="data">
    ///   Optional. The update data to apply to the entry.
    /// </param>
    /// <param name="updateSeriesStats">
    ///   Optional. When set to <c>true</c>, will update the series stats
    ///   after the update. If doing multiple updates on the same series at
    ///   once, it is recommended to set this to <c>false</c> for all but
    ///   the last update.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleUpdateVideo(IVideo video, MylistUpdateData? data = null, bool updateSeriesStats = false, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    #endregion

    #region Remove

    /// <summary>
    ///   Removes the MyList entry with the given list ID (lid).
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default. Without the <see cref="MylistFetchMode.Cache"/> flag the
    ///   cached entry is bypassed and the command is always sent to AniDB.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the entry was removed on AniDB, <c>false</c> if
    ///   there was nothing to remove.
    /// </returns>
    Task<bool> RemoveEntryAsync(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to remove the MyList entry with the given list ID
    ///   (lid), or update its state, depending on the configured delete
    ///   type.
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleRemoveEntry(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Removes the MyList entry for the file with the given file ID (fid).
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the entry was removed on AniDB, <c>false</c> if
    ///   there was nothing to remove.
    /// </returns>
    Task<bool> RemoveEntryAsync(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to remove the MyList entry for the file with the
    ///   given file ID (fid), or update its state, depending on the
    ///   configured delete type.
    /// </summary>
    /// <param name="fileID">
    ///   The file ID (fid) associated with the entry.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleRemoveEntry(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Removes the MyList entry for the file with the given ED2K hash and
    ///   size.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the entry was removed on AniDB, <c>false</c> if
    ///   there was nothing to remove.
    /// </returns>
    Task<bool> RemoveEntryAsync(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to remove the MyList entry for the file with the
    ///   given ED2K hash and size, or update its state, depending on the
    ///   configured delete type.
    /// </summary>
    /// <param name="ed2k">
    ///   The ED2K hash associated with the file.
    /// </param>
    /// <param name="fileSize">
    ///   The file size tied to the ED2K hash associated with the file.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleRemoveEntry(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Removes the generic MyList entry for the given anime ID, episode
    ///   type, and episode number.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the entry was removed on AniDB, <c>false</c> if
    ///   there was nothing to remove.
    /// </returns>
    Task<bool> RemoveEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to remove the generic MyList entry for the given
    ///   anime ID, episode type, and episode number, or update its state,
    ///   depending on the configured delete type.
    /// </summary>
    /// <param name="animeID">
    ///   The anime ID (aid) of the entry.
    /// </param>
    /// <param name="episodeType">
    ///   The type of episode.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleRemoveEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);


    /// <summary>
    ///   Applies the configured delete type to the MyList entry with the given
    ///   list ID (lid): marks it with the corresponding storage state, removes
    ///   it outright, or leaves it alone entirely, depending on the type. The
    ///   remove and update operations themselves know nothing about delete
    ///   types; the choice is made here.
    /// </summary>
    /// <param name="listID">
    ///   The list ID (lid) of the entry.
    /// </param>
    /// <param name="deleteType">
    ///   Optional. The delete type to apply. Defaults to the configured one.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB. Defaults to
    ///   <see cref="MylistFetchMode.Auto"/>, which uses the configured
    ///   default.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleDisposeEntry(ulong listID, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <inheritdoc cref="ScheduleDisposeEntry(ulong, MylistDeleteType?, MylistFetchMode, bool)"/>
    /// <param name="fileID">
    ///   The file ID (fid) of the entry.
    /// </param>
    /// <param name="deleteType">
    ///   Optional. The delete type to apply. Defaults to the configured one.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    Task ScheduleDisposeEntry(int fileID, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <inheritdoc cref="ScheduleDisposeEntry(ulong, MylistDeleteType?, MylistFetchMode, bool)"/>
    /// <param name="ed2k">
    ///   The ED2K hash of the file.
    /// </param>
    /// <param name="fileSize">
    ///   The size of the file.
    /// </param>
    /// <param name="deleteType">
    ///   Optional. The delete type to apply. Defaults to the configured one.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    Task ScheduleDisposeEntry(string ed2k, long fileSize, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <inheritdoc cref="ScheduleDisposeEntry(ulong, MylistDeleteType?, MylistFetchMode, bool)"/>
    /// <param name="animeID">
    ///   The AniDB anime ID.
    /// </param>
    /// <param name="episodeType">
    ///   The episode type.
    /// </param>
    /// <param name="episodeNumber">
    ///   The episode number.
    /// </param>
    /// <param name="deleteType">
    ///   Optional. The delete type to apply. Defaults to the configured one.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    Task ScheduleDisposeEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    /// <summary>
    ///   Disposes of the MyList entries covering a video, applying the
    ///   configured delete type. A video with an AniDB release is disposed of
    ///   by its own entry; a manually linked one is disposed of through the
    ///   generic entry of each linked episode, which is left alone when
    ///   another manual link still relies on it.
    /// </summary>
    /// <param name="video">
    ///   The video to dispose of the MyList entries for.
    /// </param>
    /// <param name="deleteType">
    ///   Optional. The delete type to apply. Defaults to the configured one.
    /// </param>
    /// <param name="fetchMode">
    ///   Optional. How to fetch entries from AniDB.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleDisposeVideo(IVideo video, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false);

    #endregion

    #region Sync

    /// <summary>
    ///   Syncs the local library with the MyList on AniDB, adding missing
    ///   files, importing or exporting watched states, and removing entries
    ///   for files no longer in the library. Generic entries are matched to
    ///   episodes, while regular entries are matched to files.
    ///
    ///   Only one sync runs at a time. Calling this while a sync is already in
    ///   progress returns <c>null</c> as soon as that is detected, without
    ///   starting a second one and without waiting for the running one to
    ///   finish.
    /// </summary>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    /// <returns>
    ///   What the sync did, or <c>null</c> if it did not run because another
    ///   sync was already in progress.
    /// </returns>
    Task<MylistSyncResult?> SyncAsync(MylistSyncOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Enqueues a job to sync the local library with the MyList on AniDB.
    /// </summary>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. When set to <c>true</c>, will prioritize the job in the
    ///   queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleSync(MylistSyncOptions? options = null, bool prioritize = false);

    /// <inheritdoc cref="SyncAsync(MylistSyncOptions?, CancellationToken)"/>
    /// <summary>
    ///   Syncs only the given videos, reconciling their MyList entries the way
    ///   a full sync would. Videos missing from the MyList are added, and the
    ///   entries covering them have their watched and storage states
    ///   reconciled. Nothing is removed: an entry is only removed when its
    ///   local file is gone, which cannot be true of a video passed in here.
    /// </summary>
    /// <param name="videos">
    ///   The videos to confine the sync to.
    /// </param>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    Task<MylistSyncResult?> SyncAsync(IEnumerable<IVideo> videos, MylistSyncOptions? options = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ScheduleSync(MylistSyncOptions?, bool)"/>
    /// <summary>
    ///   Schedules a sync confined to the given videos.
    /// </summary>
    /// <param name="videos">
    ///   The videos to confine the sync to.
    /// </param>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    Task ScheduleSync(IEnumerable<IVideo> videos, MylistSyncOptions? options = null, bool prioritize = false);

    /// <inheritdoc cref="SyncAsync(MylistSyncOptions, CancellationToken)"/>
    /// <summary>
    ///   Syncs only the given episodes. The videos linked to them come along,
    ///   so with <see cref="MylistSyncTargets.All"/> this reconciles the file
    ///   entries for those episodes as well as their generic ones. An episode
    ///   watched locally that the MyList has no generic entry for gets one,
    ///   subject to
    ///   <see cref="MylistSyncOptions.WatchedEpisodeMode"/>, and a generic
    ///   entry recording nothing at all is removed.
    /// </summary>
    /// <param name="episodes">
    ///   The episodes to confine the sync to.
    /// </param>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="cancellationToken">
    ///   A cancellation token.
    /// </param>
    Task<MylistSyncResult?> SyncAsync(IEnumerable<IShokoEpisode> episodes, MylistSyncOptions? options = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ScheduleSync(MylistSyncOptions, bool)"/>
    /// <summary>
    ///   Schedules a sync confined to the given episodes.
    /// </summary>
    /// <param name="episodes">
    ///   The episodes to confine the sync to.
    /// </param>
    /// <param name="options">
    ///   Optional. Sync options overriding the server settings for this run.
    ///   Null fields fall back to the configured server settings.
    /// </param>
    /// <param name="prioritize">
    ///   Optional. Whether to prioritize the job in the queue.
    /// </param>
    Task ScheduleSync(IEnumerable<IShokoEpisode> episodes, MylistSyncOptions? options = null, bool prioritize = false);

    #endregion
}
