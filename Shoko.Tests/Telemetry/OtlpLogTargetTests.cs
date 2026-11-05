using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NLog;
using NLog.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Telemetry;
using Xunit;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace Shoko.Tests.Telemetry;

[Collection(nameof(TelemetryRuntimeCollection))]
public sealed class OtlpLogTargetTests : IDisposable
{
    private readonly string _logsPath = Path.Combine(Path.GetTempPath(), $"shoko-otlp-log-tests-{Guid.NewGuid():N}");

    private readonly LoggingConfiguration? _previousConfiguration = LogManager.Configuration;

    public void Dispose()
    {
        LogService.ApplyLoggingSettings(new());
        LogManager.Configuration = _previousConfiguration;
        if (Directory.Exists(_logsPath))
            Directory.Delete(_logsPath, recursive: true);
    }

    /// <summary>
    /// A target in a log factory of its own, so nothing else in the process writes to it.
    /// </summary>
    private static (OtlpLogTarget Target, LogFactory Factory) NewTarget()
    {
        var target = new OtlpLogTarget();
        var config = new LoggingConfiguration();
        config.AddRuleForAllLevels(target);
        return (target, new LogFactory { Configuration = config });
    }

    private void InitLogger()
    {
        Directory.CreateDirectory(_logsPath);
        var paths = new Mock<IApplicationPaths>();
        paths.Setup(a => a.LogsPath).Returns(_logsPath);
        LogService.InitLogger(paths.Object);
    }

    private static LoggingRule? GetOtlpRule()
        => LogManager.Configuration!.LoggingRules.FirstOrDefault(rule => rule.Targets.Any(target => target.Name == OtlpLogTarget.TargetName));

    [Fact]
    public void HeldLogs_AreReplayedInOrderWithTheirOriginalTimes()
    {
        var (target, factory) = NewTarget();
        var logger = factory.GetLogger("Some.Logger");
        var first = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        logger.Log(new LogEventInfo(NLogLevel.Warn, "Some.Logger", "first") { TimeStamp = first });
        logger.Info("second");
        var sink = new SinkFactory();

        target.Connect(sink);

        Assert.Equal(["first", "second"], sink.Logs.Select(log => log.Message));
        Assert.Equal(LogLevel.Warning, sink.Logs[0].Level);
        Assert.Equal("Some.Logger", sink.Logs[0].Category);
        Assert.Equal(first, sink.Logs[0].Timestamp);
        Assert.Null(OtlpLogTimestampProcessor.NextTimestamp);
    }

    [Fact]
    public void HeldLogs_AreBoundedKeepingTheNewest()
    {
        var (target, factory) = NewTarget();
        var logger = factory.GetLogger("Some.Logger");
        for (var i = 0; i < OtlpLogTarget.MaxBufferedEvents + 10; i++)
            logger.Info("message {0}", i);
        var sink = new SinkFactory();

        target.Connect(sink);

        Assert.Equal(OtlpLogTarget.MaxBufferedEvents, sink.Logs.Count);
        Assert.Equal("message 10", sink.Logs[0].Message);
    }

    [Fact]
    public void LiveLogs_AreForwardedWithoutAReplayedTime()
    {
        var (target, factory) = NewTarget();
        var sink = new SinkFactory();
        target.Connect(sink);

        factory.GetLogger("Some.Logger").Error(new InvalidOperationException("boom"), "live");

        Assert.Equal("live", sink.Logs.Single().Message);
        Assert.Equal(LogLevel.Error, sink.Logs.Single().Level);
        Assert.IsType<InvalidOperationException>(sink.Logs.Single().Exception);
        Assert.Null(sink.Logs.Single().Timestamp);
    }

    [Fact]
    public void WithoutAnExport_HeldLogsAreDroppedAndNoMoreAreTaken()
    {
        var (target, factory) = NewTarget();
        var logger = factory.GetLogger("Some.Logger");
        logger.Info("held");
        Assert.True(target.IsActive);

        target.Connect(null);
        logger.Info("after");

        Assert.False(target.IsActive);
        var sink = new SinkFactory();
        target.Connect(sink);
        Assert.Empty(sink.Logs);
    }

    [Theory]
    [InlineData("Shoko.Server.Telemetry.TelemetryRuntime")]
    [InlineData("System.Net.Http.HttpClient.OtlpTraceExporter.ClientHandler")]
    [InlineData("OpenTelemetry.Exporter")]
    public void TheExportsOwnLoggers_AreNotExported(string loggerName)
    {
        var (target, factory) = NewTarget();
        var sink = new SinkFactory();
        target.Connect(sink);

        factory.GetLogger(loggerName).Warn("feedback");

        Assert.Empty(sink.Logs);
    }

    [Fact]
    public void ALogWrittenWhileExporting_IsNotExportedAgain()
    {
        var (target, factory) = NewTarget();
        var logger = factory.GetLogger("Some.Logger");
        var sink = new SinkFactory { OnLog = () => logger.Info("feedback") };
        target.Connect(sink);

        logger.Info("first");

        Assert.Equal("first", sink.Logs.Single().Message);
    }

    [Fact]
    public void Secrets_AreScrubbed()
    {
        var (target, factory) = NewTarget();
        var sink = new SinkFactory();
        target.Connect(sink);

        factory.GetLogger("Some.Logger").Info("GET /x?apikey=hunter2");

        Assert.Equal("GET /x?apikey=Redacted", sink.Logs.Single().Message);
    }

    [Fact]
    public void TheRules_LimitTheTargetLikeTheFileAndRedactSettings()
    {
        InitLogger();
        var sink = new SinkFactory();
        LogService.ConnectOtlpExport(sink);
        var unique = Guid.NewGuid().ToString("N");

        // Kept from every target by the default level rules, which end at Information for this logger.
        LogManager.GetLogger("Shoko.QueueProcessor.Something").Info($"void {unique}");
        LogManager.GetLogger("Shoko.Tests.Something").Debug($"below {unique}");
        LogManager.GetLogger("Shoko.Tests.Something").Info($"Settings.AniDb.Password: {unique}");
        LogManager.GetLogger("Shoko.Tests.Something").Info($"kept {unique}");

        Assert.Equal([$"kept {unique}"], sink.Logs.Where(log => log.Message.Contains(unique)).Select(log => log.Message));
    }

    [Fact]
    public void TheLevel_CanBeChangedLive()
    {
        InitLogger();
        var sink = new SinkFactory();
        LogService.ConnectOtlpExport(sink);
        var logger = LogManager.GetLogger("Shoko.Tests.Something");

        LogService.ApplyLoggingSettings(new() { DefaultOtlpLogLevel = LogLevel.Error });
        logger.Warn("not exported");
        logger.Error("exported");
        LogService.ApplyLoggingSettings(new() { DefaultOtlpLogLevel = LogLevel.Debug });
        logger.Debug("exported too");

        Assert.Equal(["exported", "exported too"], sink.Logs.Where(log => log.Category == logger.Name).Select(log => log.Message));
    }

    [Fact]
    public void TraceLogging_DoesNotWidenTheExport()
    {
        InitLogger();
        LogService.ConnectOtlpExport(new SinkFactory());

        LogService.ApplyLoggingSettings(new() { TraceLog = true });

        Assert.False(GetOtlpRule()!.IsLoggingEnabledForLevel(NLogLevel.Debug));
        Assert.True(GetOtlpRule()!.IsLoggingEnabledForLevel(NLogLevel.Info));
    }

    [Fact]
    public void WithoutAnExport_TheRuleIsGoneAndStaysGone()
    {
        InitLogger();
        Assert.NotNull(GetOtlpRule());

        LogService.ConnectOtlpExport(null);
        Assert.Null(GetOtlpRule());

        LogService.ApplyLoggingSettings(new() { DefaultOtlpLogLevel = LogLevel.Debug });
        Assert.Null(GetOtlpRule());
    }

    [Fact]
    public async Task Logs_ReachTheCollectorWithTheirTraceAndOriginalTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var collector = new HttpListener();
        collector.Prefixes.Add($"http://127.0.0.1:{port}/");
        collector.Start();
        var received = collector.GetContextAsync();

        var (target, factory) = NewTarget();
        var logger = factory.GetLogger("Some.Logger");
        var written = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        logger.Log(new LogEventInfo(NLogLevel.Info, "Some.Logger", "held-message") { TimeStamp = written });

        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{port}", TracesEnabled = true, LogsEnabled = true }, "1.0.0", NullLogger.Instance)!;
        target.Connect(runtime.LoggerFactory);
        ActivityTraceId traceId;
        using (var span = ShokoTelemetry.Source.StartActivity("a-span"))
        {
            traceId = span!.TraceId;
            logger.Info("live-message");
        }

        var shutdown = Task.Run(runtime.Shutdown, cancellationToken);
        // Traces and logs arrive in either order, so answer until the logs have come.
        byte[] body;
        while (true)
        {
            var context = await received.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            var isLogs = context.Request.Url!.AbsolutePath == "/v1/logs";
            using var buffer = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(buffer, cancellationToken);
            context.Response.Close();
            if (isLogs)
            {
                body = buffer.ToArray();
                break;
            }

            received = collector.GetContextAsync();
        }

        await shutdown.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        Assert.True(Contains(body, Encoding.UTF8.GetBytes("held-message")));
        Assert.True(Contains(body, Encoding.UTF8.GetBytes("live-message")));

        // Protobuf keeps the time as little-endian nanoseconds and the trace ID as its raw bytes.
        var nanoseconds = (ulong)(written - DateTime.UnixEpoch).Ticks * 100;
        Assert.True(Contains(body, BitConverter.GetBytes(nanoseconds)), "The replayed log lost its original time.");
        Assert.True(Contains(body, Convert.FromHexString(traceId.ToHexString())), "The live log lost its trace.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool Contains(byte[] haystack, byte[] needle)
        => haystack.AsSpan().IndexOf(needle) >= 0;

    private sealed record SunkLog(string Category, LogLevel Level, string Message, Exception? Exception, DateTime? Timestamp);

    private sealed class SinkFactory : ILoggerFactory, ILogger
    {
        private string _category = string.Empty;

        public List<SunkLog> Logs { get; } = [];

        public Action? OnLog { get; init; }

        public ILogger CreateLogger(string categoryName)
        {
            _category = categoryName;
            return this;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new(_category, logLevel, formatter(state, exception), exception, OtlpLogTimestampProcessor.NextTimestamp));
            OnLog?.Invoke();
        }
    }
}
