using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Filters;
using NLog.Layouts;
using NLog.Targets;
using Quartz.Logging;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Logging.Models;
using Shoko.Abstractions.Logging.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Extensions;
using Shoko.Server.Logging;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using ELogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

#nullable enable
namespace Shoko.Server.Services;

public class LogService(ILogger<LogService> logger, IApplicationPaths applicationPaths, ISettingsProvider settingsProvider) : ILogService
{
    private static readonly System.Threading.Lock _loggerConfigLock = new();

    public static bool IsLoggingInitialized { get; private set; }

    #region Maintenance

    private readonly Timer _timer = new(TimeSpan.FromHours(12));

    public void StartMaintenance()
    {
        _timer.Stop();
        if (!settingsProvider.GetSettings().Logging.RotationEnabled)
            return;

        _timer.Elapsed -= HandleTimerElapsed;
        _timer.Elapsed += HandleTimerElapsed;
        RunRotationMaintenance();
        _timer.Start();
    }

    public void RunRotationMaintenance()
    {
        DeleteLogs();
        CompressLogs();
    }

    #region Maintenance | Internals

    private void HandleTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        RunRotationMaintenance();
    }

    private void CompressLogs()
    {
        var settings = settingsProvider.GetSettings().Logging;
        if (!settings.RotationCompress)
            return;
        var currentLog = GetCurrentLogFilePath();
        var logDir = EnsureLogDirectory();

        // Compress .jsonl → .jsonl.gz
        foreach (var file in logDir.GetFiles("*.jsonl").Where(file => !string.Equals(file.FullName, currentLog, StringComparison.OrdinalIgnoreCase)))
        {
            var destination = file.FullName + ".gz";
            var existingCompressed = new FileInfo(destination);
            if (existingCompressed.Exists)
            {
                if (existingCompressed.LastWriteTimeUtc >= file.LastWriteTimeUtc)
                {
                    file.Delete();
                    continue;
                }

                existingCompressed.Delete();
            }

            using (var source = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var destinationStream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var gzip = new GZipStream(destinationStream, CompressionLevel.Optimal);
                source.CopyTo(gzip);
            }
            file.Delete();
        }

        // Compress legacy .log → .log.gz
        foreach (var file in logDir.GetFiles("*.log"))
        {
            var destination = file.FullName + ".gz";
            var existingCompressed = new FileInfo(destination);
            if (existingCompressed.Exists)
            {
                if (existingCompressed.LastWriteTimeUtc >= file.LastWriteTimeUtc)
                {
                    file.Delete();
                    continue;
                }

                existingCompressed.Delete();
            }

            using (var source = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var destinationStream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var gzip = new GZipStream(destinationStream, CompressionLevel.Optimal);
                source.CopyTo(gzip);
            }
            file.Delete();
        }

        // Migrate legacy .zip (containing .log) → .log.gz
        foreach (var file in logDir.GetFiles("*.zip"))
        {
            var baseName = Path.GetFileNameWithoutExtension(file.FullName);
            var destination = Path.Combine(file.DirectoryName!, baseName + ".log.gz");
            var existingCompressed = new FileInfo(destination);
            if (existingCompressed.Exists && existingCompressed.LastWriteTimeUtc >= file.LastWriteTimeUtc)
            {
                file.Delete();
                continue;
            }

            try
            {
                using var zipArchive = new ZipArchive(File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read);
                var entry = zipArchive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));
                if (entry is null)
                {
                    file.Delete();
                    continue;
                }

                if (existingCompressed.Exists)
                    existingCompressed.Delete();

                using var destinationStream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var gzip = new GZipStream(destinationStream, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.CopyTo(gzip);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to migrate zip log file: {File}", file.FullName);
            }

            file.Delete();
        }
    }

    private void DeleteLogs()
    {
        var settings = settingsProvider.GetSettings().Logging;
        if (!settings.RotationDeleteEnabled || !settings.RotationDeleteDays.HasValue || settings.RotationDeleteDays.Value < 0)
            return;

        var currentLog = GetCurrentLogFilePath();
        var threshold = DateTime.UtcNow.AddDays(-settings.RotationDeleteDays.Value);
        foreach (var file in EnsureLogDirectory().GetFiles().Where(file => DetermineFormat(file) is ({ }, true) && !string.Equals(file.FullName, currentLog, StringComparison.OrdinalIgnoreCase) && file.LastWriteTimeUtc < threshold))
            file.Delete();
    }

    #endregion

    #endregion

    #region Log File Operations

    public IReadOnlyList<LogFileInfo> GetAllLogFiles()
        => EnsureLogDirectory().GetFiles()
            .Select(ToLogFileInfo)
            .WhereNotNull()
            .OrderByDescending(fileInfo => fileInfo.IsCurrent)
            .ThenByDescending(file => file.Date)
            .ThenByDescending(file => file.DailyNumber is 0)
            .ThenByDescending(file => file.DailyNumber)
            .ToList();

    public LogFileInfo GetCurrentLogFile()
        => ToLogFileInfo(new FileInfo(GetCurrentLogFilePath()))!;

    public LogFileInfo? GetLogFileByID(Guid fileID)
        => GetAllLogFiles().FirstOrDefault(file => file.ID == fileID);

    public LogReadResult ReadLogFile(LogFileInfo fileInfo, LogReadOptions? options = null)
    {
        if (fileInfo.Format is not LogFileFormat.JsonL)
            throw new InvalidOperationException("Only JSONL logs support reading.");

        options ??= new();
        uint skipped = 0;
        var nextOffset = options.Offset;
        var entries = new List<LogEntry>();
        var compiled = CompileOptionsOrThrow(options);
        foreach (var entry in ReadEntries(fileInfo, compiled, options.Descending))
        {
            if (skipped < options.Offset)
            {
                skipped++;
                continue;
            }
            if (options.Limit > 0 && entries.Count >= options.Limit)
                break;
            entries.Add(entry);
            nextOffset++;
        }
        if (entries.Count == 0)
            return new() { NextOffset = null, Entries = [] };

        return new() { NextOffset = nextOffset, Entries = entries };
    }

    public LogDownloadResult DownloadLogFile(LogFileInfo fileInfo, LogDownloadOptions? options = null)
    {
        if (fileInfo.Format is not LogFileFormat.JsonL)
        {
            if (fileInfo.IsCompressed)
            {
                if (fileInfo.FullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var stream = OpenZipEntryStream(fileInfo.FullPath, out var entryName);
                    return new()
                    {
                        ContentType = "text/plain",
                        FileName = entryName,
                        Stream = stream,
                    };
                }

                return new()
                {
                    ContentType = "text/plain",
                    FileName = Path.ChangeExtension(fileInfo.FileName, ".log"),
                    Stream = OpenGZipStream(fileInfo.FullPath),
                };
            }

            return new()
            {
                ContentType = "text/plain",
                FileName = fileInfo.FileName,
                Stream = File.Open(fileInfo.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            };
        }

        options ??= new();
        var compiledOptions = CompileOptionsOrThrow(options);
        if (options.Format is LogSerializeFormat.Json)
        {
            if (options.HasFilters)
                return new()
                {
                    ContentType = "application/x-ndjson",
                    FileName = fileInfo.FileName,
                    Stream = CreateFilteredJsonStream(fileInfo, compiledOptions),
                };

            var stream = fileInfo.IsCompressed
                ? OpenGZipStream(fileInfo.FullPath)
                : File.Open(fileInfo.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new()
            {
                ContentType = "application/x-ndjson",
                FileName = fileInfo.FileName,
                Stream = stream,
            };
        }

        var entries = ReadEntries(fileInfo, compiledOptions);
        var formattedStream = CreateFormattedStream(entries, options.Format);
        return new()
        {
            ContentType = "text/plain",
            FileName = Path.ChangeExtension(fileInfo.FileName, ".log"),
            Stream = formattedStream,
        };
    }

    public void DeleteLogFile(LogFileInfo fileInfo)
    {
        if (fileInfo.IsCurrent)
            throw new InvalidOperationException("Cannot delete the current log file.");
        if (File.Exists(fileInfo.FullPath))
            File.Delete(fileInfo.FullPath);
    }

    #region Log File Operations | Internals

    private string GetCurrentLogFilePath()
        => LogManager.Configuration?.FindTargetByName("file") is not FileTarget fileTarget
            ? string.Empty
            : Path.GetFullPath(fileTarget.FileName.Render(new LogEventInfo { Level = NLogLevel.Info }));

    private LogFileInfo? ToLogFileInfo(FileInfo file)
    {
        if (DetermineFormat(file) is not ({ } format, var isCompressed))
            return null;

        var fileName = ToDisplayFileName(file.Name, format, isCompressed);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var dailyNumber = 0u;
        if (fileNameWithoutExtension.Contains('_'))
        {
            var (a, b) = fileNameWithoutExtension.Split('_');
            if (!uint.TryParse(b, out dailyNumber))
                return null;
            fileNameWithoutExtension = a;
        }
        if (!DateOnly.TryParse(fileNameWithoutExtension, out var date))
            return null;

        var currentPath = GetCurrentLogFilePath();
        return new()
        {
            ID = UuidUtility.GetV5(file.FullName),
            Date = date,
            DailyNumber = dailyNumber,
            FileName = fileName,
            FullPath = file.FullName,
            Size = file.Length,
            IsCurrent = string.Equals(file.FullName, currentPath, StringComparison.OrdinalIgnoreCase),
            LastModifiedAt = file.LastWriteTimeUtc,
            IsCompressed = isCompressed,
            Format = format,
        };
    }

    private static (LogFileFormat? Format, bool IsCompressed) DetermineFormat(FileInfo file)
    {
        if (file.Extension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
            return (LogFileFormat.JsonL, false);
        if (file.Name.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
            return (LogFileFormat.JsonL, true);
        if (file.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return (LogFileFormat.Legacy, false);
        if (file.Name.EndsWith(".log.gz", StringComparison.OrdinalIgnoreCase))
            return (LogFileFormat.Legacy, true);
        if (file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return (LogFileFormat.Legacy, true);
        return default;
    }

    private static string ToDisplayFileName(string fileName, LogFileFormat format, bool isCompressed)
        => (format, isCompressed) switch
        {
            (LogFileFormat.JsonL, true) => Path.GetFileNameWithoutExtension(fileName),
            (LogFileFormat.Legacy, true) => Path.ChangeExtension(fileName, ".log"),
            _ => fileName,
        };

    private static Stream OpenZipEntryStream(string fullPath, out string entryName)
    {
        var zipStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries
            .OrderByDescending(e => e.LastWriteTime)
            .FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));
        if (entry is null)
        {
            archive.Dispose();
            zipStream.Dispose();
            throw new InvalidOperationException("The archive does not contain a readable entry.");
        }
        entryName = entry.Name;
        return new WrappedZipStream(archive, entry.Open(), zipStream);
    }

    private MemoryStream CreateFilteredJsonStream(LogFileInfo fileInfo, CompiledLogBaseOptions options)
    {
        var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var entry in ReadEntries(fileInfo, options))
                writer.WriteLine(JsonSerializer.Serialize(entry));
        }

        ms.Position = 0;
        return ms;
    }

    private sealed class WrappedZipStream(ZipArchive archive, Stream inner, Stream fileStream) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                archive.Dispose();
                fileStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion

    #endregion

    #region Range Operations

    public LogReadResult ReadRange(LogReadOptions? options = null)
    {
        options ??= new() { Descending = true };
        var files = GetAllLogFiles()
            .Where(file => file.Format is LogFileFormat.JsonL)
            .OrderBy(file => file.LastModifiedAt, options.Descending ? Comparer<DateTime>.Create((x, y) => y.CompareTo(x)) : Comparer<DateTime>.Default)
            .ToList();

        uint skipped = 0;
        var nextOffset = options.Offset;
        var entries = new List<LogEntry>();
        var compiledOptions = CompileOptionsOrThrow(options);
        foreach (var file in files)
        {
            foreach (var entry in ReadEntries(file, compiledOptions, options.Descending))
            {
                if (skipped < options.Offset)
                {
                    skipped++;
                    continue;
                }

                if (options.Limit > 0 && entries.Count >= options.Limit)
                    return new() { NextOffset = nextOffset, Entries = entries };

                entries.Add(entry);
                nextOffset++;
            }
        }
        if (entries.Count == 0)
            return new() { NextOffset = null, Entries = [] };

        return new() { NextOffset = nextOffset, Entries = entries };
    }

    public LogDownloadResult DownloadRange(LogDownloadOptions? options = null)
    {
        options ??= new();
        var readOpts = new LogReadOptions
        {
            From = options.From,
            To = options.To,
            Offset = 0,
            Limit = 0,
            Descending = false,
            Levels = options.Levels,
            Logger = options.Logger,
            Caller = options.Caller,
            Message = options.Message,
            Exception = options.Exception,
            ProcessId = options.ProcessId,
            ThreadId = options.ThreadId,
        };
        var entries = ReadRange(readOpts).Entries;
        var stream = CreateFormattedStream(entries, options.Format);
        return new()
        {
            ContentType = options.Format is LogSerializeFormat.Json ? "application/x-ndjson" : "text/plain",
            FileName = BuildRangeDownloadFileName(options.From, options.To, options.Format),
            Stream = stream,
        };
    }

    #region Range Operations | Internals

    private static MemoryStream CreateFormattedStream(IEnumerable<LogEntry> entries, LogSerializeFormat format)
    {
        var content = entries
            .Select(entry => entry.ToString(format))
            .Join(Environment.NewLine);
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static string BuildRangeDownloadFileName(DateTime? from, DateTime? to, LogSerializeFormat format)
    {
        var suffix = format is LogSerializeFormat.Json ? "jsonl" : "log";
        var fromPart = from?.ToUniversalTime().ToString("yyyyMMddTHHmmssZ") ?? "min";
        var toPart = to?.ToUniversalTime().ToString("yyyyMMddTHHmmssZ") ?? "now";
        return $"range-{fromPart}-{toPart}.{suffix}";
    }

    #endregion

    #endregion

    #region Shared Internals

    private static readonly TimeSpan _logFilterRegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    private DirectoryInfo EnsureLogDirectory()
    {
        var currentLog = GetCurrentLogFilePath();
        var directory = string.IsNullOrWhiteSpace(currentLog)
            ? applicationPaths.LogsPath
            : Path.GetDirectoryName(currentLog) ?? applicationPaths.LogsPath;
        var info = new DirectoryInfo(directory);
        if (!info.Exists)
            info.Create();
        return info;
    }

    private bool TryParseLogEntry(string line, out LogEntry entry)
    {
        entry = null!;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            var timestamp = root.TryGetProperty("timestamp", out var timestampElement) &&
                DateTime.TryParse(timestampElement.GetString(), out var ts)
                ? ts.ToUniversalTime()
                : DateTime.UtcNow;

            var threadId = root.TryGetProperty("threadId", out var threadIdElement) &&
                threadIdElement.GetString() is { } threadIdString && int.TryParse(threadIdString, out var parsedThreadId)
                ? parsedThreadId
                : 0;

            var processId = root.TryGetProperty("processId", out var processIdElement) &&
                processIdElement.GetString() is { } processIdString && int.TryParse(processIdString, out var parsedProcessId)
                ? parsedProcessId
                : 0;

            var parsedLevel = root.TryGetProperty("level", out var levelElement) && NLogLevel.FromString(levelElement.GetString()!) is { } level
                ? level.Ordinal switch
                {
                    0 => ELogLevel.Trace,
                    1 => ELogLevel.Debug,
                    2 => ELogLevel.Information,
                    3 => ELogLevel.Warning,
                    4 => ELogLevel.Error,
                    5 => ELogLevel.Critical,
                    6 or _ => ELogLevel.None,
                }
                : ELogLevel.Information;

            entry = new()
            {
                TimeStamp = timestamp,
                Level = parsedLevel,
                Logger = root.TryGetProperty("logger", out var loggerElement) ? loggerElement.GetString() ?? string.Empty : string.Empty,
                Caller = root.TryGetProperty("caller", out var callerElement) ? callerElement.GetString() ?? string.Empty : string.Empty,
                ThreadId = threadId,
                ProcessId = processId,
                Message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty,
                Exception = root.TryGetProperty("exception", out var exceptionElement) ? exceptionElement.GetString() : null,
            };
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Skipped malformed log line.");
            return false;
        }
    }

    private IEnumerable<LogEntry> ReadEntries(LogFileInfo file, CompiledLogBaseOptions? options, bool descending = false)
    {
        if (file.IsCompressed)
        {
            using var stream = OpenGZipStream(file.FullPath);
            using var reader = new StreamReader(stream);
            foreach (var entry in ParseLogEntries(ReadLinesForward(reader), options, descending))
                yield return entry;
            yield break;
        }

        if (descending)
        {
            foreach (var entry in ParseLogEntries(ReadLinesReverse(file.FullPath), options))
                yield return entry;
            yield break;
        }

        using var fileStream = File.Open(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var fileReader = new StreamReader(fileStream);
        foreach (var entry in ParseLogEntries(ReadLinesForward(fileReader), options))
            yield return entry;
    }

    private static Stream OpenGZipStream(string fullPath)
        => new GZipStream(
            File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            CompressionMode.Decompress
        );

    private static IEnumerable<string> ReadLinesForward(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static IEnumerable<string> ReadLinesReverse(string fullPath)
    {
        const int bufferSize = 8192;
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length == 0)
            yield break;

        var buffer = new byte[bufferSize];
        var reversedLineBytes = new List<byte>(256);
        var position = stream.Length;
        var skipTrailingNewline = true;

        while (position > 0)
        {
            var bytesToRead = (int)Math.Min(bufferSize, position);
            position -= bytesToRead;
            stream.Seek(position, SeekOrigin.Begin);
            var bytesRead = stream.Read(buffer, 0, bytesToRead);
            for (var i = bytesRead - 1; i >= 0; i--)
            {
                var currentByte = buffer[i];
                if (currentByte == '\n')
                {
                    if (skipTrailingNewline && reversedLineBytes.Count == 0)
                    {
                        skipTrailingNewline = false;
                        continue;
                    }

                    if (reversedLineBytes.Count == 0)
                        continue;

                    yield return DecodeReversedLine(reversedLineBytes);
                    reversedLineBytes.Clear();
                    continue;
                }

                if (currentByte != '\r')
                    reversedLineBytes.Add(currentByte);

                skipTrailingNewline = false;
            }
        }

        if (reversedLineBytes.Count > 0)
            yield return DecodeReversedLine(reversedLineBytes);
    }

    private static string DecodeReversedLine(List<byte> reversedLineBytes)
    {
        reversedLineBytes.Reverse();
        return Encoding.UTF8.GetString(reversedLineBytes.ToArray());
    }

    private IEnumerable<LogEntry> ParseLogEntries(IEnumerable<string> lines, CompiledLogBaseOptions? options = null, bool descending = false)
    {
        if (descending)
        {
            var entries = new List<LogEntry>();
            foreach (var line in lines)
            {
                if (TryParseFilteredLine(line, options, out var entry))
                    entries.Add(entry);
            }

            for (var i = entries.Count - 1; i >= 0; i--)
                yield return entries[i];
            yield break;
        }

        foreach (var line in lines)
        {
            if (TryParseFilteredLine(line, options, out var entry))
                yield return entry;
        }
    }

    private bool TryParseFilteredLine(string line, CompiledLogBaseOptions? options, [NotNullWhen(true)] out LogEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(line))
            return false;
        if (!TryParseLogEntry(line, out var parsedEntry))
            return false;
        if (options is not null)
        {
            if (options.From.HasValue && parsedEntry.TimeStamp < options.From.Value)
                return false;
            if (options.To.HasValue && parsedEntry.TimeStamp > options.To.Value)
                return false;
            if (options.Levels is { Count: > 0 } && !options.Levels.Contains(parsedEntry.Level))
                return false;
            if (options.ProcessId.HasValue && parsedEntry.ProcessId != options.ProcessId.Value)
                return false;
            if (options.ThreadId.HasValue && parsedEntry.ThreadId != options.ThreadId.Value)
                return false;
            if (options.Logger is { } lc && !LogFilterMatchField(parsedEntry.Logger, lc))
                return false;
            if (options.Caller is { } cc && !LogFilterMatchField(parsedEntry.Caller, cc))
                return false;
            if (options.Message is { } mc && !LogFilterMatchField(parsedEntry.Message, mc))
                return false;
            if (options.Exception is { } ec && !LogFilterMatchField(parsedEntry.Exception ?? string.Empty, ec))
                return false;
        }
        entry = parsedEntry;
        return true;
    }

    private static bool LogFilterMatchField(string haystack, CompiledTextCriterion c)
    {
        var comparison = c.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var matches = c.Mode switch
        {
            LogTextMatchMode.Contains => haystack.IndexOf(c.Payload, comparison) >= 0,
            LogTextMatchMode.Equals => string.Equals(haystack, c.Payload, comparison),
            LogTextMatchMode.StartsWith => haystack.StartsWith(c.Payload, comparison),
            LogTextMatchMode.EndsWith => haystack.EndsWith(c.Payload, comparison),
            LogTextMatchMode.Fuzzy => haystack.FuzzyMatches(c.Payload),
            LogTextMatchMode.Regex => c.CompiledRegex!.IsMatch(haystack),
            _ => false,
        };
        return c.Inverse ? !matches : matches;
    }

    private static CompiledLogBaseOptions CompileOptionsOrThrow(LogBaseOptions baseOptions)
    {
        if (!TryCompileLogEntryFilter(baseOptions, out var compiled, out var err))
            throw new GenericValidationException("Unable to parse log options before use.", err);
        return compiled;
    }

    /// <summary>
    ///   Builds a compiled options object or returns per-field validation errors for invalid DSL.
    /// </summary>
    internal static bool TryCompileLogEntryFilter(LogBaseOptions baseOptions, [NotNullWhen(true)] out CompiledLogBaseOptions? compiledOptions, out Dictionary<string, IReadOnlyList<string>> errors)
    {
        errors = [];
        if (!baseOptions.HasFilters)
        {
            compiledOptions = new();
            return true;
        }

        CompiledTextCriterion? logger = null, caller = null, message = null, exception = null;
        if (baseOptions.Logger is not null && !TryCompileLogFilterField(baseOptions.Logger, nameof(baseOptions.Logger), out logger, out var error))
            errors.Add(nameof(baseOptions.Logger), [error]);
        if (baseOptions.Caller is not null && !TryCompileLogFilterField(baseOptions.Caller, nameof(baseOptions.Caller), out caller, out error))
            errors.Add(nameof(baseOptions.Caller), [error]);
        if (baseOptions.Message is not null && !TryCompileLogFilterField(baseOptions.Message, nameof(baseOptions.Message), out message, out error))
            errors.Add(nameof(baseOptions.Message), [error]);
        if (baseOptions.Exception is not null && !TryCompileLogFilterField(baseOptions.Exception, nameof(baseOptions.Exception), out exception, out error))
            errors.Add(nameof(baseOptions.Exception), [error]);
        if (errors.Count > 0)
        {
            compiledOptions = null;
            return false;
        }

        compiledOptions = new CompiledLogBaseOptions
        {
            From = baseOptions.From?.ToUniversalTime(),
            To = baseOptions.To?.ToUniversalTime(),
            Levels = baseOptions.Levels,
            ProcessId = baseOptions.ProcessId,
            ThreadId = baseOptions.ThreadId,
            Logger = logger,
            Caller = caller,
            Message = message,
            Exception = exception,
        };
        return true;
    }

    private static bool TryCompileLogFilterField(string dsl, string fieldName, out CompiledTextCriterion? criterion, [NotNullWhen(false)] out string? error)
    {
        criterion = null;
        if (!TryParseLogFilterDsl(dsl, fieldName, out var mode, out var inverse, out var ignoreCase, out var payload, out error))
            return false;

        Regex? rx = null;
        if (mode is LogTextMatchMode.Regex && !TryCompileLogFilterRegexPayload(payload, fieldName, out rx, out error))
            return false;

        criterion = new CompiledTextCriterion
        {
            Mode = mode,
            Inverse = inverse,
            IgnoreCase = ignoreCase,
            Payload = payload,
            CompiledRegex = rx,
        };
        return true;
    }

    private static bool TryParseLogFilterDsl(
        string dsl,
        string fieldName,
        out LogTextMatchMode mode,
        out bool inverse,
        out bool ignoreCase,
        out string payload,
        [NotNullWhen(false)] out string? error)
    {
        mode = LogTextMatchMode.Contains;
        inverse = false;
        ignoreCase = false;
        payload = dsl;
        error = null;

        var colon = dsl.IndexOf(':');
        if (colon < 0)
        {
            mode = LogTextMatchMode.Contains;
            payload = dsl;
            return true;
        }

        if (colon == 0)
        {
            error = $"{fieldName}: DSL cannot start with ':'.";
            return false;
        }

        var head = dsl.AsSpan(0, colon);
        var modeChar = head[0];
        mode = modeChar switch
        {
            'c' => LogTextMatchMode.Contains,
            '=' => LogTextMatchMode.Equals,
            '^' => LogTextMatchMode.StartsWith,
            '$' => LogTextMatchMode.EndsWith,
            '~' => LogTextMatchMode.Fuzzy,
            '*' => LogTextMatchMode.Regex,
            _ => (LogTextMatchMode)255,
        };
        if ((int)mode == 255)
        {
            error = $"{fieldName}: Unknown mode character '{modeChar}'.";
            return false;
        }

        inverse = false;
        ignoreCase = false;
        for (var i = 1; i < head.Length; i++)
        {
            var c = head[i];
            if (c is '!')
            {
                if (inverse)
                {
                    error = $"{fieldName}: Duplicate '!' in modifiers.";
                    return false;
                }

                inverse = true;
            }
            else if (c is '#')
            {
                if (ignoreCase)
                {
                    error = $"{fieldName}: Duplicate '#' in modifiers.";
                    return false;
                }

                ignoreCase = true;
            }
            else
            {
                error = $"{fieldName}: Invalid character '{c}' in modifiers (only ! and # allowed).";
                return false;
            }
        }

        if (ignoreCase && mode is LogTextMatchMode.Fuzzy or LogTextMatchMode.Regex)
            ignoreCase = false;

        payload = dsl[(colon + 1)..];
        return true;
    }

    private static bool TryCompileLogFilterRegexPayload(
        string payload,
        string fieldName,
        [NotNullWhen(true)] out Regex? regex,
        [NotNullWhen(false)] out string? error)
    {
        regex = null;
        error = null;

        string pattern;
        var options = RegexOptions.CultureInvariant;

        if (payload.Length >= 2 && payload[0] == '/')
        {
            var lastSlash = payload.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                error = $"{fieldName}: Regex payload has leading '/' but no closing '/'.";
                return false;
            }

            pattern = payload[1..lastSlash];
            var flags = payload.AsSpan(lastSlash + 1);
            foreach (var f in flags)
            {
                switch (f)
                {
                    case 'i':
                        options |= RegexOptions.IgnoreCase;
                        break;
                    default:
                        error = $"{fieldName}: Unsupported regex flag '{f}'.";
                        return false;
                }
            }
        }
        else
            pattern = payload;

        try
        {
            regex = new Regex(pattern, options, _logFilterRegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            error = $"{fieldName}: Invalid regex: {ex.Message}";
            return false;
        }

        return true;
    }

    internal enum LogTextMatchMode
    {
        Contains,
        Equals,
        StartsWith,
        EndsWith,
        Fuzzy,
        Regex,
    }

    /// <summary>
    ///   One compiled text-field criterion (logger, caller, message, or exception).
    /// </summary>
    internal sealed class CompiledTextCriterion
    {
        public required LogTextMatchMode Mode { get; init; }
        public required bool Inverse { get; init; }
        public required bool IgnoreCase { get; init; }
        /// <summary>For non-regex modes: pattern / substring.</summary>
        public required string Payload { get; init; }
        public Regex? CompiledRegex { get; init; }
    }

    /// <summary>
    ///   Parsed and compiled <see cref="LogBaseOptions"/> for efficient matching.
    /// </summary>
    internal sealed class CompiledLogBaseOptions
    {
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public IReadOnlyList<ELogLevel>? Levels { get; init; }
        public int? ProcessId { get; init; }
        public int? ThreadId { get; init; }
        public CompiledTextCriterion? Logger { get; init; }
        public CompiledTextCriterion? Caller { get; init; }
        public CompiledTextCriterion? Message { get; init; }
        public CompiledTextCriterion? Exception { get; init; }
    }

    #endregion

    #region Static Methods

    private static bool _rotationEnabled = false;

    private static bool _traceLogging = false;

    private static List<LogLevelRuleConfiguration> _logLevelRules = [];

    private static LogSerializeFormat _consoleFormat = LogSerializeFormat.Console;

    static LogService()
    {
        LogManager.Setup().SetupExtensions(b => b.RegisterLayoutRenderer<ShortLevelLayoutRenderer>());
    }

    public static void InitLogger(IApplicationPaths applicationPaths)
    {
        lock (_loggerConfigLock)
        {
            var config = new LoggingConfiguration();

            var fileTarget = BuildFileTarget(applicationPaths);
            var consoleTarget = BuildConsoleTarget(LogSerializeFormat.Console);
            var signalrTarget = BuildSignalRTarget();
            var voidTarget = new NullTarget("void");
            config.AddTarget(fileTarget);
            config.AddTarget(consoleTarget);
            config.AddTarget(signalrTarget);
            config.AddTarget(voidTarget);

            RebuildLoggingRules(config, GetDefaultLogLevelRules(), voidTarget, fileTarget, consoleTarget, signalrTarget);
            ApplyMessageRedactionFilter(config, fileTarget, consoleTarget, signalrTarget);
#if DEBUG
            // Enable debug logging
            config.LoggingRules.FirstOrDefault(a => a.Targets.Contains(fileTarget))
                ?.EnableLoggingForLevel(NLogLevel.Debug);
#endif
            LogManager.Configuration = config;
            LogProvider.SetLogProvider(new NLog.Extensions.Logging.NLogLoggerFactory());
            LogManager.ReconfigExistingLoggers();
            IsLoggingInitialized = true;
        }
    }

    public static void ApplyLoggingSettings(LoggingSettings logging)
    {
        lock (_loggerConfigLock)
        {
            var config = LogManager.Configuration;
            if (config is null)
                return;

            var updated = false;
            if (ApplyLoggingLevelRules(logging, config))
                updated = true;
            if (ApplyConsoleFormat(logging, config))
                updated = true;
            if (SetTraceLogging(logging.TraceLog))
                updated = true;
            if (updated)
                LogManager.ReconfigExistingLoggers();

            if (_rotationEnabled != logging.RotationEnabled)
            {
                _rotationEnabled = logging.RotationEnabled;
                Utils.ServiceContainer?.GetRequiredService<ILogService>().StartMaintenance();
            }
        }
    }

    private static bool ApplyLoggingLevelRules(LoggingSettings logging, LoggingConfiguration config)
    {
        if (_logLevelRules.SequenceEqual(logging.LogLevelRules))
            return false;
        if (config.FindTargetByName<FileTarget>("file") is not { } fileTarget)
            return false;
        if (config.FindTargetByName<ColoredConsoleTarget>("console") is not { } consoleTarget)
            return false;
        if (config.FindTargetByName<Target>("signalr") is not { } signalrTarget)
            return false;
        if (config.FindTargetByName<Target>("void") is not { } voidTarget)
            return false;

        _logLevelRules = logging.LogLevelRules
            .Select(a => new LogLevelRuleConfiguration() { LoggerNamePattern = a.LoggerNamePattern, MaxLevel = a.MaxLevel, Final = a.Final })
            .ToList();
        var mergedRules = MergeLogLevelRules(logging.LogLevelRules);
        RebuildLoggingRules(config, mergedRules, voidTarget, fileTarget, consoleTarget, signalrTarget);
        ApplyMessageRedactionFilter(config, fileTarget, consoleTarget, signalrTarget);
        return true;
    }

    private static bool ApplyConsoleFormat(LoggingSettings logging, LoggingConfiguration config)
    {
        var consoleTarget = config.FindTargetByName<ColoredConsoleTarget>("console");
        if (consoleTarget is null || _consoleFormat == logging.ConsoleFormat)
            return false;

        _consoleFormat = logging.ConsoleFormat;
        consoleTarget.Layout = GetConsoleLayout(logging.ConsoleFormat);
        return true;
    }

    private static bool SetTraceLogging(bool enabled)
    {
        var config = LogManager.Configuration;
        if (config == null || _traceLogging == enabled)
            return false;

        _traceLogging = enabled;
        var fileRule = config.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b.Name == "file"));
        var signalrRule = config.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b.Name == "signalr"));
        if (enabled)
        {
            fileRule?.EnableLoggingForLevels(NLogLevel.Trace, NLogLevel.Debug);
            signalrRule?.EnableLoggingForLevels(NLogLevel.Trace, NLogLevel.Debug);
        }
        else
        {
            fileRule?.DisableLoggingForLevels(NLogLevel.Trace, NLogLevel.Debug);
            signalrRule?.DisableLoggingForLevels(NLogLevel.Trace, NLogLevel.Debug);
        }
        return true;
    }

    private static FileTarget BuildFileTarget(IApplicationPaths applicationPaths)
        => new("file")
        {
            FileName = Path.Join(applicationPaths.LogsPath, "${shortdate}.jsonl"),
            ArchiveAboveSize = 52428800,
            ArchiveFileName = Path.Join(applicationPaths.LogsPath, "${shortdate}.{#####}.jsonl"),
            KeepFileOpen = false,
            Layout = GetJsonLayout(),
        };

    private static ColoredConsoleTarget BuildConsoleTarget(LogSerializeFormat format)
        => new("console")
        {
            Layout = GetConsoleLayout(format),
        };

    private static SignalRTarget BuildSignalRTarget()
        => new()
        {
            Name = "signalr",
            Layout = "${threadid},${processid},${message}",
        };

    private static IReadOnlyList<LogLevelRuleConfiguration> GetDefaultLogLevelRules()
        => [
            new() { LoggerNamePattern = "Microsoft.AspNetCore.*", MaxLevel = ELogLevel.Information, Final = true },
            new() { LoggerNamePattern = "Quartz*", MaxLevel = ELogLevel.Information, Final = true },
            new() { LoggerNamePattern = "Shoko.Server.Scheduling.ThreadPooledJobStore", MaxLevel = ELogLevel.Information, Final = true },
            new() { LoggerNamePattern = "Shoko.Server.Scheduling.Delegates.*", MaxLevel = ELogLevel.Information, Final = true },
            new() { LoggerNamePattern = "Shoko.Server.API.Authentication.CustomAuthHandler", MaxLevel = ELogLevel.Information, Final = true },
            new() { LoggerNamePattern = "Microsoft.Extensions.Http.Logging.*", MaxLevel = ELogLevel.Warning, Final = true },
            new() { LoggerNamePattern = "System.Net.Http.HttpClient.*", MaxLevel = ELogLevel.Warning, Final = true },
        ];

    private static IReadOnlyList<LogLevelRuleConfiguration> MergeLogLevelRules(IEnumerable<LogLevelRuleConfiguration>? userRules)
    {
        var merged = GetDefaultLogLevelRules().ToDictionary(rule => rule.LoggerNamePattern, StringComparer.OrdinalIgnoreCase);
        if (userRules is null)
            return merged.Values.ToList();
        foreach (var rule in userRules.Where(r => !string.IsNullOrWhiteSpace(r.LoggerNamePattern)))
            merged[rule.LoggerNamePattern] = rule;
        return merged.Values.ToList();
    }

    private static void RebuildLoggingRules(
        LoggingConfiguration config,
        IReadOnlyList<LogLevelRuleConfiguration> levelRules,
        Target voidTarget,
        FileTarget fileTarget,
        ColoredConsoleTarget consoleTarget,
        Target signalrTarget)
    {
        config.LoggingRules.Clear();
        foreach (var overrideRule in levelRules)
        {
            if (overrideRule.MaxLevel is not { } maxLevel)
                continue;
            var nlogMaxLevel = NLogLevel.FromString(maxLevel.ToNLogString());
            var rule = new LoggingRule(overrideRule.LoggerNamePattern, NLogLevel.Trace, nlogMaxLevel, voidTarget)
            {
                Final = overrideRule.Final
            };
            config.LoggingRules.Add(rule);
        }

        config.LoggingRules.Add(new LoggingRule("*", NLogLevel.Info, fileTarget));
        config.LoggingRules.Add(new LoggingRule("*", NLogLevel.Trace, consoleTarget));
        config.LoggingRules.Add(new LoggingRule("*", NLogLevel.Trace, signalrTarget));
    }

    private static void ApplyMessageRedactionFilter(
        LoggingConfiguration config,
        FileTarget fileTarget,
        ColoredConsoleTarget consoleTarget,
        Target signalrTarget)
    {
        foreach (var loggingRule in config.LoggingRules)
        {
            var hasFileTarget = loggingRule.Targets.Contains(fileTarget);
            var hasConsoleTarget = loggingRule.Targets.Contains(consoleTarget);
            var hasSignalRTarget = loggingRule.Targets.Contains(signalrTarget);
            if (!hasFileTarget && !hasConsoleTarget && !hasSignalRTarget)
                continue;

            loggingRule.FilterDefaultAction = FilterResult.Log;
            loggingRule.Filters.Clear();
            loggingRule.Filters.Add(new ConditionBasedFilter
            {
                Action = FilterResult.Ignore,
                Condition = "(contains(message, 'password') or contains(message, 'token') or contains(message, 'key')) and starts-with(message, 'Settings.')",
            });
        }
    }

    private static Layout GetConsoleLayout(LogSerializeFormat format)
        => format switch
        {
            LogSerializeFormat.Simple => "[${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff}] [${shortlevel}] ${logger:shortname=true}: ${message}${onexception:: ${exception:format=tostring}}",
            LogSerializeFormat.Full => "[${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff zzz}] [${shortlevel}] [${threadid:padding=3}] ${logger}: ${message}${onexception:${newline}${exception:format=tostring}}",
            LogSerializeFormat.Legacy => "[${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff}] ${level}|${logger} > ${message}${onexception:: ${exception:format=tostring}}",
            LogSerializeFormat.Json => GetJsonLayout(),
            LogSerializeFormat.Console => "${date:format=HH\\:mm\\:ss}| ${logger:shortname=true} --- ${message}${onexception:\\: ${exception:format=tostring}}",
            _ => "${date:format=HH\\:mm\\:ss}| ${logger:shortname=true} --- ${message}${onexception:\\: ${exception:format=tostring}}",
        };

    private static JsonLayout GetJsonLayout()
    {
        var layout = new JsonLayout();
        layout.Attributes.Add(new JsonAttribute("timestamp", "${date:format=o}"));
        layout.Attributes.Add(new JsonAttribute("level", "${level}"));
        layout.Attributes.Add(new JsonAttribute("logger", "${logger}"));
        layout.Attributes.Add(new JsonAttribute("caller", "${callsite:className=true:methodName=true}"));
        layout.Attributes.Add(new JsonAttribute("threadId", "${threadid}"));
        layout.Attributes.Add(new JsonAttribute("processId", "${processid}"));
        layout.Attributes.Add(new JsonAttribute("message", "${message}"));
        layout.Attributes.Add(new JsonAttribute("exception", "${exception:format=tostring}"));
        return layout;
    }

    #endregion
}
