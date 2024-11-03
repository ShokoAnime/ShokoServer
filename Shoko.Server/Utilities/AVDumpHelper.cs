#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Utilities;

public static partial class AVDumpHelper
{
    #region Private Variables

    private static readonly string WorkingDirectory = Path.Combine(Utils.ApplicationPath, "AVDump");

    private static readonly string RuntimeConfigPath = Path.Combine(WorkingDirectory, "AVDump3CL.runtimeconfig.json");

    private static readonly string ArchivePath = Path.Combine(Utils.ApplicationPath, "avdump.zip");

    private const string AVDumpURL = @"AVD3_URL_GOES_HERE";

    private static readonly string AVDumpExecutable = Path.Combine(WorkingDirectory, Utils.IsLinuxOrMac ? "AVDump3CL.dll" : "AVDump3CL.exe");

    private static readonly ConcurrentDictionary<int, AVDumpSession> ActiveSessions = new();

    [GeneratedRegex(@"^\s*(?<currentFiles>\d+)\/(?<totalFiles>\d+)\s+Files\s+\|\s+(?<currentBytes>\d+)\/(?<totalBytes>\d+)\s+\w{1,4}\s+\|", RegexOptions.Compiled)]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"^\s*Total\s+\[(?<progress>[\s#]+)\]\s+(?<speed1m>\-?\d+)\s+(?<speed5m>\-?\d+)\s+(?<speed15m>\-?\d+)(?<speedUnit>[A-Za-z]+)/s\s*$", RegexOptions.Compiled)]
    private static partial Regex SummaryRegex();

    [GeneratedRegex(@"^\s*\-+\s*$", RegexOptions.Compiled)]
    private static partial Regex SeperatorRegex();

    [GeneratedRegex(@"\s+\(WrongUsernameOrApiKey\)$", RegexOptions.Compiled)]
    private static partial Regex InvalidCredentialsRegex();

    [GeneratedRegex(@"\s+\(Timeout\)$", RegexOptions.Compiled)]
    private static partial Regex TimeoutRegex();

    [GeneratedRegex(@"^\s*ACreq\(Done:\s+(?<succeeded>\d+)\s+Failed:\s+(?<failed>\d+)\s+Pending:\s+(?<pending>\d+)\)\s*$", RegexOptions.Compiled)]
    private static partial Regex AnidbCreqRegex();

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private static readonly object _prepareLock = new();

    private static readonly object _startLock = new();

    #endregion
    #region Public Variables

    /// <summary>
    /// The currently expected AVDump version to use.
    /// </summary>
    public static string? AVDumpVersion =>
        int.TryParse(AVDumpURL.Split('/').LastOrDefault()?.Split('_').ElementAtOrDefault(1), out var version) ? version.ToString() : null;

    /// <summary>
    /// Checks if the AVDump component is installed.
    /// </summary>
    /// <value>True if avdump is installed, otherwise false.</value>
    public static bool IsAVDumpInstalled =>
        File.Exists(AVDumpExecutable);

    /// <summary>
    /// Get the version of the installed AVDump componet, provided a compatible
    /// AVDump executable is installed on the system, otherwise returns null.
    /// </summary>
    /// <value>The version number, or null.</value>
    public static string? InstalledAVDumpVersion
    {
        get
        {
            if (!IsAVDumpInstalled)
                return null;

            var result = string.Empty;
            using var subProcess = GetSubProcessForOS("--Version");
            subProcess.Start();
            Task.WaitAll(
                subProcess.StandardOutput.ReadToEndAsync().ContinueWith(task => result = task.Result),
                subProcess.WaitForExitAsync()
            );

            // Assumption is the mother of all f*ck ups, but idc. Assuming the position of the
            // version is faster than actually checking.
            return result
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault()
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
        }
    }

    #endregion
    #region Public Methods

    public static IReadOnlyList<AVDumpSession> GetActiveSessions() =>
        ActiveSessions.Values.DistinctBy(a => a.SessionID).ToList();

    /// <summary>
    /// Get the active dump session for a video.
    /// </summary>
    /// <param name="video"></param>
    /// <returns></returns>
    public static AVDumpSession? GetSessionForVideo(SVR_VideoLocal video)
    {
        // Check if we have an active session for the video (x1).
        return ActiveSessions.TryGetValue(video.VideoLocalID, out var session) ? session : null;
    }

    /// <summary>
    /// Update the installed AVDump component.
    /// </summary>
    public static bool UpdateAVDump() =>
        PrepareAVDump(true);

    /// <summary>
    /// Run AVDump for a file, streaming events from the process, and also
    /// storing some results in the database if successful.
    /// </summary>
    /// <param name="videos">The associated video for the file.</param>
    /// <param name="synchronous">Wait for completion</param>
    /// <returns>The dump results for v1 compatibility.</returns>
    public static AVDumpSession DumpFiles(IEnumerable<KeyValuePair<int, string>> videos, bool synchronous = false)
    {
        // Guard against stupidity and/or unforeseen errors.
        var videoDict = videos.ToDictionary(v => v.Key, v => v.Value);
        if (videoDict.Count == 0)
            return new("Cannot dump 0 files.");

        // The sub-routine takes care of sending the avdump event if it fails,
        // the return message is for v1 compatibility.
        if (!PrepareAVDump())
            return new("Failed to install or update the AVDump component.");

        var settings = Utils.SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
        {
            var message = "Missing AVDump API Key in the settings.";
            ShokoEventHandler.Instance.OnAVDumpMessage(AVDumpEventType.MissingApiKey);
            logger.Warn(message);
            return new(message);
        }

        AVDumpSession session;
        int preExistingSessions;
        lock (_startLock)
        {
            preExistingSessions = ActiveSessions.Count;
            session = new AVDumpSession(videoDict.Keys, videoDict.Values);
            var checkedIds = new HashSet<int>();
            foreach (var videoId in videoDict.Keys)
            {
                if (ActiveSessions.TryGetValue(videoId, out _) ||
                    !ActiveSessions.TryAdd(videoId, session))
                {
                    var message = "Unable start an AVDump session for a VideoLocal already in a session.";
                    logger.Warn(message);
                    foreach (var checkedVideoId in checkedIds)
                        ActiveSessions.TryRemove(checkedVideoId, out _);

                    session.IsRunning = false;
                    session.StandardOutput = message;
                    return session;
                }

                checkedIds.Add(videoId);
            }
        }

        var task = Task.Factory.StartNew(() =>
        {
            ShokoEventHandler.Instance.OnAVDumpStart(session);

            try
            {
                // Prepare the sub-process and attach the event handler.
                var stdOutBuilder = new StringBuilder();
                var stdErrBuilder = new StringBuilder();
                using var subProcess = GetSubProcessForOS([
                    $"--Timeout={settings.AniDb.AVDump.CreqTimeout}:{settings.AniDb.AVDump.CreqMaxRetries}",
                    $"--Concurrent={settings.AniDb.AVDump.MaxConcurrency}",
                    "--HideBuffers=true",
                    "--HideFileProgress=true",
                    "--DisableFileMove=true",
                    "--DisableFileRename=true",
                    "--Consumers=ED2K",
                    $"--Auth={settings.AniDb.Username.Trim()}:{settings.AniDb.AVDumpKey?.Trim()}",
                    // Workaround for when we try to start multiple dump sessions.
                    $"--LPort={(preExistingSessions == 0 ? settings.AniDb.AVDumpClientPort : 0)}", "--PrintEd2kLink=true",
                    ..videoDict.Values,
                ]);
                subProcess.OutputDataReceived += (_, eventArgs) => OnStdOutMessage(eventArgs, session, stdOutBuilder);
                subProcess.ErrorDataReceived += (_, eventArgs) => OnStdErrMessage(eventArgs, session, stdOutBuilder);

                // Start dumping.
                logger.Info($"Running AVDump session with id {session.SessionID}: \"{string.Join("\", \"", videoDict.Values)}\"");
                subProcess.Start();
                subProcess.BeginOutputReadLine();
                subProcess.BeginErrorReadLine();
                subProcess.WaitForExit();

                // Post-process the output.
                session.IsRunning = false;
                session.StandardOutput = stdOutBuilder.ToString();
                session.StandardError = stdErrBuilder.ToString();
                session.IsSuccess = string.IsNullOrEmpty(session.StandardError) &&
                                    session.ED2Ks.Count == session.VideoIDs.Count &&
                                    session.SucceededCreqCount == session.VideoIDs.Count &&
                                    session.FailedCreqCount == 0 &&
                                    session.PendingCreqCount == 0;
                session.EndedAt = DateTime.Now;
                if (session.IsSuccess)
                {
                    foreach (var videoId in videoDict.Keys)
                    {
                        var video = RepoFactory.VideoLocal.GetByID(videoId);
                        if (video == null)
                            continue;

                        video.LastAVDumped = session.EndedAt;
                        video.LastAVDumpVersion = InstalledAVDumpVersion;
                        RepoFactory.VideoLocal.Save(video);
                    }
                }
                else
                {
                    logger.Warn(
                        $"Failed to complete AVDump session {session.SessionID}:\n\nFiles:\n{string.Join("\n", videoDict.Values)}\n\nStandard Output:\n{session.StandardOutput}{(session.StandardError.Length > 0 ? $"\nStandard Error:\n{session.StandardError}" : "")}");
                }

                // Report the results.
                ShokoEventHandler.Instance.OnAVDumpEnd(session);
            }
            catch (Exception ex)
            {
                var message =
                    $"An error occurred while running AVDump session {session.SessionID}:\n\nFiles:\n{string.Join("\n", videoDict.Values)}\n\nStack Trace:\n{ex.StackTrace}";

                // Update the session.
                session.IsRunning = false;
                session.StandardOutput = message;
                session.StandardError = ex.StackTrace ?? string.Empty;
                session.IsSuccess = false;
                session.EndedAt = DateTime.Now;

                logger.Error(message);
                ShokoEventHandler.Instance.OnAVDumpGenericException(session, ex);
            }
            finally
            {
                lock (_startLock)
                    foreach (var videoId in videoDict.Keys)
                        ActiveSessions.TryRemove(videoId, out _);
            }
        }).ConfigureAwait(false);
        if (synchronous) task.GetAwaiter().GetResult();

        return session;
    }

    private static void OnStdErrMessage(DataReceivedEventArgs eventArgs, AVDumpSession session, StringBuilder stdErrBuilder)
    {
        // Last event (when the stream is closing) will send `null`.
        // Also ignore any empty lines sent to standard error.
        if (string.IsNullOrWhiteSpace(eventArgs.Data))
            return;

        stdErrBuilder.Append(eventArgs.Data.Trim() + "\n");
        ShokoEventHandler.Instance.OnAVDumpMessage(session, AVDumpEventType.Error, eventArgs.Data);
    }

    private static void OnStdOutMessage(DataReceivedEventArgs eventArgs, AVDumpSession session, StringBuilder stdOutBuilder)
    {
        // Last event (when the stream is closing) will send `null`.
        // Ignore empty lines, separators, the summary, or creq updates in the output for now.
        if (string.IsNullOrWhiteSpace(eventArgs.Data) || SeperatorRegex().IsMatch(eventArgs.Data) || SummaryRegex().IsMatch(eventArgs.Data)) return;

        // Calculate overall progress.
        var result = ProgressRegex().Match(eventArgs.Data);
        if (result.Success)
        {
            var currentBytes = double.Parse(result.Groups["currentBytes"].Value);
            var totalBytes = double.Parse(result.Groups["totalBytes"].Value);
            var currentProgress = currentBytes / totalBytes * 100;
            if (currentProgress <= session.Progress) return;

            session.Progress = currentProgress;
            ShokoEventHandler.Instance.OnAVDumpProgress(session, currentProgress);
            return;
        }

        result = AnidbCreqRegex().Match(eventArgs.Data);
        if (result.Success)
        {
            var succeeded = int.Parse(result.Groups["succeeded"].Value);
            var failed = int.Parse(result.Groups["failed"].Value);
            var pending = int.Parse(result.Groups["pending"].Value);
            if (succeeded == session.SucceededCreqCount && failed == session.FailedCreqCount && pending == session.PendingCreqCount)
                return;

            session.SucceededCreqCount = succeeded;
            session.FailedCreqCount = failed;
            session.PendingCreqCount = pending;
            ShokoEventHandler.Instance.OnAVDumpCreqUpdate(session, succeeded, failed, pending);
            return;
        }

        // Emit an invalid credentials event if we couldn't authenticate with AniDB.
        if (InvalidCredentialsRegex().IsMatch(eventArgs.Data))
            ShokoEventHandler.Instance.OnAVDumpMessage(AVDumpEventType.InvalidCredentials);

        // Emit a timeout event if the connection to anidb timed out.
        if (TimeoutRegex().IsMatch(eventArgs.Data))
            ShokoEventHandler.Instance.OnAVDumpMessage(AVDumpEventType.Timeout);

        if (eventArgs.Data.Trim().StartsWith("ed2k://|file|"))
        {
            var ed2kLink = eventArgs.Data.Trim();
            session.ED2Ks.Add(ed2kLink);
            ShokoEventHandler.Instance.OnAVDumpMessage(session, AVDumpEventType.ED2KLink, ed2kLink);
            return;
        }

        // Append everything else to the outputs. We use \r\n for v1 compatibility.
        stdOutBuilder.Append(eventArgs.Data + "\n");
        ShokoEventHandler.Instance.OnAVDumpMessage(session, AVDumpEventType.Message, eventArgs.Data);
    }

    #endregion
    #region Private Methods

    /// <summary>
    /// Prepare AVDump for use.
    /// </summary>
    private static bool PrepareAVDump(bool force = false)
    {
        lock (_prepareLock)
        {
            // Automatically update the installed avdump version if we expect
            // a newer version.
            var expectedVersion = AVDumpVersion;
            var installedVersion = InstalledAVDumpVersion;
            if (!force && !string.IsNullOrEmpty(expectedVersion) && !string.Equals(expectedVersion, installedVersion))
                force = true;

            if (!force && File.Exists(AVDumpExecutable))
            {
                if (File.Exists(RuntimeConfigPath) && File.ReadAllText(RuntimeConfigPath).Contains("6.0")) ReplaceNet6();
                return true;
            }

            ShokoEventHandler.Instance.OnAVDumpMessage(AVDumpEventType.InstallingAVDump);

            if (string.IsNullOrEmpty(expectedVersion))
            {
                var ex = new Exception("Unable to install AVDump3 automatically. Please manually install it before continuing.");
                ShokoEventHandler.Instance.OnAVDumpInstallException(ex);
                logger.Error(ex);
                return false;
            }

            // Download the archive if it's not available locally.
            if (!File.Exists(ArchivePath))
            {
                try
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "JMM");
                    using var stream = client.GetStreamAsync(AVDumpURL).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (stream == null)
                        return false;

                    using var fileStream = File.Create(ArchivePath);
                    stream.CopyTo(fileStream);
                }
                catch (Exception ex)
                {
                    ShokoEventHandler.Instance.OnAVDumpInstallException(ex);
                    logger.Error(ex);
                    return false;
                }
            }

            // Extract the archive.
            try
            {
                // First clear out the existing version.
                if (Directory.Exists(WorkingDirectory))
                    Directory.Delete(WorkingDirectory, true);

                // Then add the new version.
                Directory.CreateDirectory(WorkingDirectory);
                using Stream stream = File.OpenRead(ArchivePath);
                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(WorkingDirectory, new ExtractionOptions
                        {
                            // This may have serious problems in the future, but for now, AVDump is flat
                            ExtractFullPath = false,
                            Overwrite = true,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ShokoEventHandler.Instance.OnAVDumpInstallException(ex);
                logger.Error(ex, "Unable to install AVDump3; {ErrorMessage}", ex.Message);
                return false;
            }

            try
            {
                File.Delete(ArchivePath);
            }
            catch
            {
                // eh we tried
            }

            ReplaceNet6();

            ShokoEventHandler.Instance.OnAVDumpMessage(AVDumpEventType.InstalledAVDump);
            return true;
        }
    }

    private static void ReplaceNet6()
    {
        try
        {
            if (File.Exists(RuntimeConfigPath))
            {
                var current = File.ReadAllText(RuntimeConfigPath);
                var replaced = current.Replace("6.0", "8.0");
                File.WriteAllText(RuntimeConfigPath, replaced);
            }
        }
        catch (Exception e)
        {
            ShokoEventHandler.Instance.OnAVDumpInstallException(e);
            logger.Error(e, "Unable to install AVDump3");
        }
    }

    /// <summary>
    /// Get a sub-process to run AVDump for the current OS, with the argument
    /// list appended to the process argument list.
    /// </summary>
    /// <param name="argumentList">Arguments to append to the start info for the
    /// process.</param>
    /// <returns>A new process to run AVDump for the current OS.</returns>
    private static Process GetSubProcessForOS(params string[] argumentList)
    {
        var startInfo = new ProcessStartInfo(AVDumpExecutable)
        {
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (Utils.IsLinuxOrMac)
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(AVDumpExecutable);
        }

        foreach (var arg in argumentList)
            startInfo.ArgumentList.Add(arg);

        return new Process { StartInfo = startInfo };
    }

    #endregion
    #region Public Classes

    public class AVDumpSession
    {
        private static int NextSessionID { get; set; }

        public int SessionID { get; }

        public IReadOnlyList<int> VideoIDs { get; }

        public IReadOnlyList<string> AbsolutePaths { get; }

        public bool IsRunning { get; set; }

        public bool IsSuccess { get; set; }

        public double Progress { get; set; }

        public int SucceededCreqCount { get; set; }

        public int PendingCreqCount { get; set; }

        public int FailedCreqCount { get; set; }

        public HashSet<string> ED2Ks { get; set; }

        public string StandardOutput { get; set; }

        public string StandardError { get; set; }

        public DateTime StartedAt { get; }

        public DateTime? EndedAt { get; set; }

        public AVDumpSession(IEnumerable<int> videoIds, IEnumerable<string> paths)
        {
            if (NextSessionID == int.MaxValue)
                NextSessionID = 0;
            SessionID = ++NextSessionID;
            VideoIDs = videoIds.ToList();
            AbsolutePaths = paths.ToList();
            StartedAt = DateTime.Now;
            IsRunning = true;
            ED2Ks = [];
            StandardOutput = string.Empty;
            StandardError = string.Empty;
        }

        public AVDumpSession(string stdOut)
        {
            SessionID = 0;
            VideoIDs = new List<int>();
            AbsolutePaths = new List<string>();
            ED2Ks = [];
            StandardOutput = stdOut;
            StandardError = string.Empty;
        }
    }

    #endregion
}
