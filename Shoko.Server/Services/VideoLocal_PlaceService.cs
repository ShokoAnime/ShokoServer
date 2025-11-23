using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHibernate;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.MediaInfo;
using Shoko.Server.MediaInfo.Subtitles;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services.Ogg;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class VideoLocal_PlaceService(
    ILogger<VideoLocal_PlaceService> _logger,
    ISchedulerFactory _schedulerFactory,
    ISettingsProvider _settingsProvider,
    DatabaseFactory _databaseFactory,
    VideoLocalService _videoLocalService,
    VideoLocalRepository _videoLocal,
    VideoLocal_PlaceRepository _videoLocalPlace
)
{
    #region Relocation (Move & Rename)

    #region Methods

    public void CleanupManagedFolder(IManagedFolder managedFolder)
    {
        var directories = Directory.EnumerateDirectories(managedFolder.Path, "*", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true });
        RecursiveDeleteEmptyDirectories(directories, managedFolder.Path);
    }

    public void RecursiveDeleteEmptyDirectories(string? toBeChecked, string? directoryToClean)
        => RecursiveDeleteEmptyDirectories([toBeChecked], directoryToClean);

    public void RecursiveDeleteEmptyDirectories(IEnumerable<string?> toBeChecked, string? directoryToClean)
    {
        if (string.IsNullOrEmpty(directoryToClean))
            return;
        try
        {
            directoryToClean = directoryToClean.TrimEnd(Path.DirectorySeparatorChar);
            var directoriesToClean = toBeChecked
                .SelectMany(path =>
                {
                    int? isExcludedAt = null;
                    var paths = new List<(string path, int level)>();
                    while (!string.IsNullOrEmpty(path))
                    {
                        var level = path == directoryToClean ? 0 : path[(directoryToClean.Length + 1)..].Split(Path.DirectorySeparatorChar).Length;
                        if (path == directoryToClean)
                            break;
                        if (_settingsProvider.GetSettings().Import.ExcludeExpressions.Any(reg => reg.IsMatch(path)))
                            isExcludedAt = level;
                        paths.Add((path, level));
                        path = Path.GetDirectoryName(path);
                    }
                    return isExcludedAt.HasValue
                        ? paths.Where(tuple => tuple.level < isExcludedAt.Value)
                        : paths;
                })
                .DistinctBy(tuple => tuple.path)
                .OrderByDescending(tuple => tuple.level)
                .ThenBy(tuple => tuple.path)
                .Select(tuple => tuple.path)
                .ToList();
            foreach (var directoryPath in directoriesToClean)
            {
                if (Directory.Exists(directoryPath) && IsDirectoryEmpty(directoryPath))
                {
                    _logger.LogTrace("Removing EMPTY directory at {Path}", directoryPath);

                    try
                    {
                        Directory.Delete(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        if (ex is DirectoryNotFoundException or FileNotFoundException) return;
                        _logger.LogWarning(ex, "Unable to DELETE directory: {Directory}", directoryPath);
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException) return;
            _logger.LogError(e, "There was an error removing the empty directories in {Dir}\n{Ex}", directoryToClean, e);
        }
    }

    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion Helpers
    #endregion Relocation (Move & Rename)

    double CalculateDurationOggFile(string filename)
    {
        try
        {
            var oggFile = OggFile.ParseFile(filename);
            return oggFile.Duration;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to parse duration from Ogg-Vorbis file {filename}.", filename);
            return 0;
        }
    }

    public bool RefreshMediaInfo(VideoLocal_Place place, VideoLocal video)
    {
        _logger.LogTrace("Getting media info for: {Place}", place.Path ?? place.ID.ToString());
        if (!place.IsAvailable)
        {
            _logger.LogError("File {Place} failed to be retrieved for MediaInfo", place.ID.ToString());
            return false;
        }

        var path = place.Path;
        try
        {
            var mediaInfo = MediaInfoUtility.GetMediaInfo(path);
            if (mediaInfo is { GeneralStream: { Duration: 0, Format: "ogg" } })
                mediaInfo.GeneralStream.Duration = CalculateDurationOggFile(path);

            if (mediaInfo is { IsUsable: true })
            {
                var subs = SubtitleHelper.GetSubtitleStreams(place.Path);
                if (subs.Count > 0)
                    mediaInfo.media.track.AddRange(subs);

                video.MediaInfo = mediaInfo;
                video.MediaVersion = VideoLocal.MEDIA_VERSION;
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to read the media information of file {Place} ERROR: {Ex}", path,
                e);
        }

        _logger.LogError("File {Place} failed to read MediaInfo", path);
        return false;
    }

    public async Task RemoveRecordAndDeletePhysicalFile(VideoLocal_Place place, bool deleteFolder = true, bool updateMyList = true)
    {
        _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

        if (!File.Exists(place.Path))
        {
            _logger.LogInformation("Unable to find file. Removing Record: {Place}", place.Path ?? place.RelativePath);
            await RemoveRecord(place, updateMyList);
            return;
        }

        try
        {
            File.Delete(place.Path);
            DeleteExternalSubtitles(place.Path);
        }
        catch (FileNotFoundException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.Path, ex);
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.Path), place.ManagedFolder!.Path);

        await RemoveRecord(place, updateMyList);
    }

    public async Task RemoveAndDeleteFileWithOpenTransaction(ISession session, VideoLocal_Place place, HashSet<SVR_AnimeSeries> seriesToUpdate, bool deleteFolders = true, bool updateMyList = true)
    {
        try
        {
            _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

            if (!File.Exists(place.Path))
            {
                _logger.LogInformation("Unable to find file. Removing Record: {FullServerPath}", place.Path);
                await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
                return;
            }

            try
            {
                File.Delete(place.Path);
                DeleteExternalSubtitles(place.Path);
            }
            catch (FileNotFoundException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.Path, ex);
                return;
            }

            if (deleteFolders)
                RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.Path), place.ManagedFolder!.Path);

            await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
            // For deletion of files from Trakt, we will rely on the Daily sync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not delete file and remove record for \"{Place}\": {Ex}", place.Path ?? place.ID.ToString(),
                ex);
        }
    }

    private void DeleteExternalSubtitles(string originalFileName)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(originalFileName);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var srcParent = Path.GetDirectoryName(originalFileName);
                if (string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                try
                {
                    File.Delete(subPath);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to delete file: \"{SubtitleFile}\"", subtitleFile.Filename);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error deleting external subtitles: {Ex}", ex);
        }
    }

    public async Task RemoveRecord(VideoLocal_Place place, bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var seriesToUpdate = new List<SVR_AnimeSeries>();
        var v = place.VideoLocal;
        var scheduler = await _schedulerFactory.GetScheduler();

        using (var session = _databaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                    await _videoLocalService.ScheduleRemovalFromMyList(v);

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    _videoLocalPlace.DeleteWithOpenTransaction(s, place);

                    seriesToUpdate.AddRange(
                        v
                            .AnimeEpisodes
                            .DistinctBy(a => a.AnimeSeriesID)
                            .Select(a => a.AnimeSeries)
                            .WhereNotNull()
                    );
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, v);
                    transaction.Commit();
                });
            }
            else
            {
                if (v is not null)
                {
                    try
                    {
                        ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    transaction.Commit();
                });
            }
        }

        await Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task RemoveRecordWithOpenTransaction(ISession session, VideoLocal_Place place, ICollection<SVR_AnimeSeries> seriesToUpdate,
        bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var v = place.VideoLocal;

        if (v?.Places?.Count <= 1)
        {
            if (updateMyListStatus)
                await _videoLocalService.ScheduleRemovalFromMyList(v);

            var eps = v.AnimeEpisodes?.WhereNotNull().ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.AnimeSeries).WhereNotNull().ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlace.DeleteWithOpenTransaction(session, place);
                _videoLocal.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            });
        }
        else
        {
            if (v is not null)
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                }
                catch
                {
                    // ignore
                }
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlace.DeleteWithOpenTransaction(session, place);
                transaction.Commit();
            });
        }
    }
}
