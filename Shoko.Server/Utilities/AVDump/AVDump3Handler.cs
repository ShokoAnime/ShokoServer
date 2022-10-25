using System;
using System.Linq;
using System.Threading;
using AVDump3Lib.Modules;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Utilities.AVDump;

public class AVDump3Handler
{
    private readonly ILogger<AVDump3Handler> _logger;
    private readonly AVD3ModuleManagement _moduleManagement;
    private readonly AVDump3Module _avdModule;

    public AVDump3Handler(ILogger<AVDump3Handler> logger)
    {
        _logger = logger;
        _moduleManagement = AVDump3Module.Create();
        _avdModule = _moduleManagement.GetModule<AVDump3Module>();
        if (_avdModule == null)
            throw new InvalidOperationException(
                $"The type {nameof(AVDump3Module)} was not able to be loaded from AVDump3's DI container");
    }

    private FileProgress[] _progress;
    // locks can't be null, so separate object to ensure it is not
    private readonly object _progressLock = new();

    public FileProgress[] Progress
    {
        get
        {
            lock (_progressLock)
            {
                return _progress = _avdModule.GetProgress()?.Where(a => a != null).Select(a => new FileProgress
                (
                    FilePath: a.FilePath,
                    ProcessedBytes: a.ProcessedBytes,
                    TotalBytes: a.TotalBytes,
                    Completed: a.Completed
                )).ToArray();
            }
        }
        set
        {
            lock (_progressLock)
            {
                _progress = value?.Where(a => a != null).Select(a => new FileProgress
                (
                    FilePath: a.FilePath,
                    ProcessedBytes: a.ProcessedBytes,
                    TotalBytes: a.TotalBytes,
                    Completed: a.Completed
                )).ToArray();
            }
        }
    }

    public string Run(params string[] pathsToProcess)
    {
        if (pathsToProcess.Any(a => string.IsNullOrEmpty(a) || !System.IO.File.Exists(a))) return string.Empty;
        if (!_avdModule.Finished) return string.Empty;
        if (!AVDump3Module.Run(_moduleManagement)) return string.Empty;

        var previousPercent = 0;
        var progressTimer = new Timer(_ =>
        {
            try
            {
                var progress = Progress;
                if (progress == null) return;

                var currentPercent = GetProgressPercent(progress);
                if (currentPercent <= previousPercent) return;

                _logger.LogTrace("AVDump {Percent}% Finished", currentPercent);
                previousPercent = currentPercent;
            }
            catch
            {
                // ignore
            }
        }, null, 500, 500);

        try
        {
            _avdModule.Process(pathsToProcess.ToArray());
            var progress = Progress;
            progressTimer.Dispose();
            _logger.LogTrace("AVDump {Percent}% Finished", GetProgressPercent(progress));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error running AVDump3 for Files: {File}, {Ex}",
                string.Join(",", pathsToProcess), e);
        }

        return _avdModule.Output;
    }

    private static int GetProgressPercent(FileProgress[] progress)
    {
        var total = progress.Sum(a => a.TotalBytes);
        var processed = progress.Sum(a => a.ProcessedBytes);
        if (total == 0) return 0;

        var currentPercent = (int)((double)processed / total * 100);
        return currentPercent;
    }
}

