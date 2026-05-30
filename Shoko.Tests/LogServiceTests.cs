using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Logging.Models;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#nullable enable
namespace Shoko.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"shoko-logservice-tests-{Guid.NewGuid():N}");
    private readonly LoggingConfiguration? _previousNlogConfiguration;

    public LogServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _previousNlogConfiguration = LogManager.Configuration;
        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget("file")
        {
            FileName = Path.Combine(_tempDirectory, "current.jsonl"),
        };
        config.AddRuleForAllLevels(fileTarget);
        LogManager.Configuration = config;
    }

    [Fact]
    public void ReadLogFile_ShouldApplyOffsetAndLimit_ForUncompressedJsonl()
    {
        var path = Path.Combine(_tempDirectory, "sample.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("a", DateTime.UtcNow.AddMinutes(-3)),
            MakeLine("b", DateTime.UtcNow.AddMinutes(-2)),
            MakeLine("c", DateTime.UtcNow.AddMinutes(-1)),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 1, Limit = 1 });

        Assert.Single(result.Entries);
        Assert.Equal("b", result.Entries[0].Message);
        Assert.Equal<uint?>(3, result.NextOffset);
    }

    [Fact]
    public void ReadLogFile_ShouldApplyOffsetAndLimit_ForCompressedJsonl()
    {
        var gzipPath = Path.Combine(_tempDirectory, "sample.jsonl.gz");
        using (var stream = File.Open(gzipPath, FileMode.Create))
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip))
        {
            writer.WriteLine(MakeLine("one", DateTime.UtcNow.AddMinutes(-2)));
            writer.WriteLine(MakeLine("two", DateTime.UtcNow.AddMinutes(-1)));
        }

        var service = CreateService();
        var file = GetFileByPath(service, gzipPath);
        var result = service.ReadLogFile(file, new() { Offset = 1, Limit = 1 });

        Assert.Single(result.Entries);
        Assert.Equal("two", result.Entries[0].Message);
        Assert.Equal<uint?>(3, result.NextOffset);
    }

    [Fact]
    public void ReadLogFile_ShouldReturnDescending_ForUncompressedJsonl()
    {
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-2);
        var t3 = DateTime.UtcNow.AddMinutes(-1);
        var path = Path.Combine(_tempDirectory, "descending.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("a", t1),
            MakeLine("b", t2),
            MakeLine("c", t3),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 0, Descending = true });

        Assert.Equal(new[] { "c", "b", "a" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldReturnDescending_ForCompressedJsonl()
    {
        var gzipPath = Path.Combine(_tempDirectory, "descending-compressed.jsonl.gz");
        using (var stream = File.Open(gzipPath, FileMode.Create))
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip))
        {
            writer.WriteLine(MakeLine("one", DateTime.UtcNow.AddMinutes(-3)));
            writer.WriteLine(MakeLine("two", DateTime.UtcNow.AddMinutes(-2)));
            writer.WriteLine(MakeLine("three", DateTime.UtcNow.AddMinutes(-1)));
        }

        var service = CreateService();
        var file = GetFileByPath(service, gzipPath);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 0, Descending = true });

        Assert.Equal(new[] { "three", "two", "one" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldHandleMixedNewlines_WhenDescending()
    {
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-2);
        var t3 = DateTime.UtcNow.AddMinutes(-1);
        var path = Path.Combine(_tempDirectory, "mixed-newlines.jsonl");
        var content = $"{MakeLine("first", t1)}\r\n{MakeLine("second", t2)}\n{MakeLine("third", t3)}";
        File.WriteAllText(path, content);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 0, Descending = true });

        Assert.Equal(new[] { "third", "second", "first" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ReadRange_ShouldReturnNewestFirst_ByDefault()
    {
        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-2);
        var t3 = DateTime.UtcNow.AddHours(-1);

        var plainPath = Path.Combine(_tempDirectory, "plain.jsonl");
        File.WriteAllLines(plainPath,
        [
            MakeLine("old", t1),
            MakeLine("middle", t2),
        ]);
        File.SetLastWriteTimeUtc(plainPath, t2);

        var compressedPath = Path.Combine(_tempDirectory, "compressed.jsonl.gz");
        using (var stream = File.Open(compressedPath, FileMode.Create))
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip))
        {
            writer.WriteLine(MakeLine("new", t3));
        }
        File.SetLastWriteTimeUtc(compressedPath, t3);

        var service = CreateService();
        var result = service.ReadRange(new()
        {
            From = t1.AddMinutes(-1),
            To = t3.AddMinutes(1),
            Offset = 0,
            Limit = 0,
            Descending = true,
        });

        Assert.Equal(new[] { "new", "middle", "old" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldRestrictEntries_ByFromAndTo_Inclusive()
    {
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-2);
        var t3 = DateTime.UtcNow.AddMinutes(-1);
        var path = Path.Combine(_tempDirectory, "from-to.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("a", t1),
            MakeLine("b", t2),
            MakeLine("c", t3),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new()
        {
            From = t2,
            To = t2,
            Offset = 0,
            Limit = 10,
        });

        Assert.Single(result.Entries);
        Assert.Equal("b", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_ByLevelAndMessage()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("keep", t, "Info"),
            MakeLine("drop", t, "Error"),
            MakeLine("also-keep", t, "Info"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new()
        {
            Offset = 0,
            Limit = 10,
            Levels = [LogLevel.Information],
            Message = "keep",
        });

        Assert.Equal(new[] { "keep", "also-keep" }, result.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_ByProcessAndThreadId()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-ids.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("a", t, processId: 10, threadId: 20),
            MakeLine("b", t, processId: 99, threadId: 20),
            MakeLine("c", t, processId: 10, threadId: 21),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new()
        {
            Offset = 0,
            Limit = 10,
            ProcessId = 10,
            ThreadId = 20,
        });

        Assert.Single(result.Entries);
        Assert.Equal("a", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_MessageNegatedContains()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-neg-c.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("alpha", t),
            MakeLine("alphabravo", t),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = "c!:bravo" });

        Assert.Single(result.Entries);
        Assert.Equal("alpha", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_MessageNotEquals()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-neq.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("keep-a", t),
            MakeLine("drop", t),
            MakeLine("keep-b", t),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = "=!:drop" });

        Assert.Equal(new[] { "keep-a", "keep-b" }, result.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_CallerStartsWith_CaseInsensitiveHash()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-caller-hash.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("one", t, caller: "get /api"),
            MakeLine("two", t, caller: "POST /api"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Caller = "^#:GET" });

        Assert.Single(result.Entries);
        Assert.Equal("one", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_CallerNegatedStartsWith_ModifierOrdersEquivalent()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-caller-mod-order.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("ok", t, caller: "POST /x"),
            MakeLine("no", t, caller: "GET /x"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var a = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Caller = "^!#:GET" });
        var b = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Caller = "^#!:GET" });

        Assert.Equal(new[] { "ok" }, a.Entries.Select(e => e.Message).ToArray());
        Assert.Equal(new[] { "ok" }, b.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_MessageFuzzyMode()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-fuzzy.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("unrelated", t),
            MakeLine("network timeout contacting upstream", t),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = "~:timeout" });

        Assert.Single(result.Entries);
        Assert.Equal("network timeout contacting upstream", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_MessageRegexWithIgnoreCaseFlag()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-regex-i.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("quiet", t),
            MakeLine("ERR loud", t),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = "*:/err/i" });

        Assert.Single(result.Entries);
        Assert.Equal("ERR loud", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_ExceptionEqualsEmpty_MatchesNullAndEmptyProperty()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-exc-empty.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("no-prop", t),
            MakeLine("empty-prop", t, includeException: true, exceptionValue: string.Empty),
            MakeLine("has-text", t, includeException: true, exceptionValue: "boom"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Exception = "=:" });

        Assert.Equal(new[] { "no-prop", "empty-prop" }, result.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_ExceptionNotEqualsEmpty_MatchesWhenExceptionTextPresent()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-exc-nonempty.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("no-prop", t),
            MakeLine("empty-prop", t, includeException: true, exceptionValue: string.Empty),
            MakeLine("has-text", t, includeException: true, exceptionValue: "boom"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Exception = "=!:" });

        Assert.Single(result.Entries);
        Assert.Equal("has-text", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_WhitespaceOnlyMessageDsl_AsContainsLiteral()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-ws-dsl.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("x", t),
            MakeLine("a   b", t),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = "   " });

        Assert.Single(result.Entries);
        Assert.Equal("a   b", result.Entries[0].Message);
    }

    [Fact]
    public void ReadLogFile_InvalidFilterDsl_ThrowsGenericValidationException()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "filter-bad-dsl.jsonl");
        File.WriteAllLines(path, [MakeLine("x", t)]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        Assert.Throws<GenericValidationException>(() => service.ReadLogFile(file, new()
        {
            Offset = 0,
            Limit = 10,
            Message = "*:[(",
        }));
    }

    [Fact]
    public void ReadRange_ShouldReturnOldestFirst_WhenDescendingIsFalse()
    {
        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-2);
        var t3 = DateTime.UtcNow.AddHours(-1);

        var plainPath = Path.Combine(_tempDirectory, "plain-range.jsonl");
        File.WriteAllLines(plainPath,
        [
            MakeLine("old", t1),
            MakeLine("middle", t2),
        ]);
        File.SetLastWriteTimeUtc(plainPath, t2);

        var compressedPath = Path.Combine(_tempDirectory, "compressed-range.jsonl.gz");
        using (var stream = File.Open(compressedPath, FileMode.Create))
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip))
        {
            writer.WriteLine(MakeLine("new", t3));
        }
        File.SetLastWriteTimeUtc(compressedPath, t3);

        var service = CreateService();
        var result = service.ReadRange(new()
        {
            From = t1.AddMinutes(-1),
            To = t3.AddMinutes(1),
            Offset = 0,
            Limit = 0,
            Descending = false,
        });

        Assert.Equal(new[] { "old", "middle", "new" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    public void Dispose()
    {
        LogManager.Configuration = _previousNlogConfiguration;
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    private LogService CreateService()
    {
        var settings = new ServerSettings();
        settings.Logging.RotationEnabled = false;
        settings.Logging.RotationCompress = false;
        settings.Logging.RotationDeleteEnabled = false;

        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(provider => provider.GetSettings(It.IsAny<bool>())).Returns(settings);
        var appPaths = new Mock<IApplicationPaths>();
        appPaths.SetupGet(paths => paths.LogsPath).Returns(_tempDirectory);

        return new LogService(NullLogger<LogService>.Instance, appPaths.Object, settingsProvider.Object);
    }

    private static string JsonEscape(string s)
        => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string MakeLine(
        string message,
        DateTime timestamp,
        string nlogLevel = "Info",
        int processId = 1,
        int threadId = 1,
        string logger = "Test",
        string caller = "Test::Method",
        bool includeException = false,
        string exceptionValue = "")
    {
        var exc = includeException
            ? ",\"exception\":\"" + JsonEscape(exceptionValue) + "\""
            : string.Empty;
        return "{\"timestamp\":\"" + timestamp.ToUniversalTime().ToString("O") +
               "\",\"level\":\"" + nlogLevel + "\",\"logger\":\"" + JsonEscape(logger) + "\",\"caller\":\"" +
               JsonEscape(caller) + "\",\"threadId\":\"" + threadId +
               "\",\"processId\":\"" + processId + "\",\"message\":\"" + JsonEscape(message) + "\"" + exc +
               ",\"context\":{\"source\":\"test\"}}";
    }

    private static LogFileInfo GetFileByPath(LogService service, string path)
        => Assert.Single(service.GetAllLogFiles(), file => string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase));
}
