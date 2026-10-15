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

    [Theory]
    [InlineData(false, new[] { "a", "b", "c" }, "b")]
    [InlineData(true, new[] { "one", "two" }, "two")]
    public void ReadLogFile_ShouldApplyOffsetAndLimit(bool compressed, string[] messages, string expectedMessage)
    {
        var lines = messages
            .Select((message, index) => MakeLine(message, DateTime.UtcNow.AddMinutes(index - messages.Length)))
            .ToArray();
        var path = WriteLogFile(compressed, lines);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 1, Limit = 1 });

        Assert.Single(result.Entries);
        Assert.Equal(expectedMessage, result.Entries[0].Message);
        Assert.Equal<uint?>(2, result.NextOffset);
    }

    [Theory]
    [InlineData(false, new[] { "a", "b", "c" })]
    [InlineData(true, new[] { "one", "two", "three" })]
    public void ReadLogFile_ShouldReturnDescending(bool compressed, string[] messages)
    {
        var lines = messages
            .Select((message, index) => MakeLine(message, DateTime.UtcNow.AddMinutes(index - messages.Length)))
            .ToArray();
        var path = WriteLogFile(compressed, lines);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 0, Descending = true });

        Assert.Equal(Enumerable.Reverse(messages).ToArray(), result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldHandleMixedNewlines_WhenDescending()
    {
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-2);
        var t3 = DateTime.UtcNow.AddMinutes(-1);
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
        var content = $"{MakeLine("first", t1)}\r\n{MakeLine("second", t2)}\n{MakeLine("third", t3)}";
        File.WriteAllText(path, content);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 0, Descending = true });

        Assert.Equal(new[] { "third", "second", "first" }, result.Entries.Select(entry => entry.Message).ToArray());
    }

    [Theory]
    [InlineData(true, "new middle old")]
    [InlineData(false, "old middle new")]
    public void ReadRange_ShouldOrderEntriesByDescending(bool descending, string expectedOrder)
    {
        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-2);
        var t3 = DateTime.UtcNow.AddHours(-1);

        var plainPath = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
        File.WriteAllLines(plainPath,
        [
            MakeLine("old", t1),
            MakeLine("middle", t2),
        ]);
        File.SetLastWriteTimeUtc(plainPath, t2);

        var compressedPath = Path.Combine(_tempDirectory, "2020-01-02.jsonl.gz");
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
            Descending = descending,
        });

        Assert.Equal(expectedOrder, string.Join(' ', result.Entries.Select(entry => entry.Message)));
    }

    [Fact]
    public void ReadLogFile_ShouldRestrictEntries_ByFromAndTo_Inclusive()
    {
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-2);
        var t3 = DateTime.UtcNow.AddMinutes(-1);
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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
            ProcessID = 10,
            ThreadID = 20,
        });

        Assert.Single(result.Entries);
        Assert.Equal("a", result.Entries[0].Message);
    }

    [Theory]
    [InlineData(new[] { "alpha", "alphabravo" }, "c!:bravo", new[] { "alpha" })] // negated contains
    [InlineData(new[] { "keep-a", "drop", "keep-b" }, "=!:drop", new[] { "keep-a", "keep-b" })] // not equals
    [InlineData(new[] { "unrelated", "network timeout contacting upstream" }, "~:timeout", new[] { "network timeout contacting upstream" })] // fuzzy mode
    [InlineData(new[] { "quiet", "ERR loud" }, "*:/err/i", new[] { "ERR loud" })] // regex with the ignore-case flag
    [InlineData(new[] { "x", "a   b" }, "   ", new[] { "a   b" })] // a whitespace-only DSL is a contains literal
    public void ReadLogFile_ShouldFilter_ByMessageDsl(string[] messages, string messageDsl, string[] expectedMessages)
    {
        var path = WriteLogFile(false, messages.Select(message => MakeLine(message, DateTime.UtcNow)).ToArray());

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Message = messageDsl });

        Assert.Equal(expectedMessages, result.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_ShouldFilter_CallerStartsWith_CaseInsensitiveHash()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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

    [Theory]
    [InlineData("=:", new[] { "no-prop", "empty-prop" })] // equals empty matches a missing and an empty property
    [InlineData("=!:", new[] { "has-text" })] // not equals empty matches when exception text is present
    public void ReadLogFile_ShouldFilter_ByExceptionDsl(string exceptionDsl, string[] expectedMessages)
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
        File.WriteAllLines(path,
        [
            MakeLine("no-prop", t),
            MakeLine("empty-prop", t, includeException: true, exceptionValue: string.Empty),
            MakeLine("has-text", t, includeException: true, exceptionValue: "boom"),
        ]);

        var service = CreateService();
        var file = GetFileByPath(service, path);
        var result = service.ReadLogFile(file, new() { Offset = 0, Limit = 10, Exception = exceptionDsl });

        Assert.Equal(expectedMessages, result.Entries.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ReadLogFile_InvalidFilterDsl_ThrowsGenericValidationException()
    {
        var t = DateTime.UtcNow;
        var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
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

    private string WriteLogFile(bool compressed, string[] lines)
    {
        if (!compressed)
        {
            var path = Path.Combine(_tempDirectory, "2020-01-01.jsonl");
            File.WriteAllLines(path, lines);
            return path;
        }

        var gzipPath = Path.Combine(_tempDirectory, "2020-01-01.jsonl.gz");
        using var stream = File.Open(gzipPath, FileMode.Create);
        using var gzip = new GZipStream(stream, CompressionLevel.Optimal);
        using var writer = new StreamWriter(gzip);
        foreach (var line in lines)
            writer.WriteLine(line);
        return gzipPath;
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
