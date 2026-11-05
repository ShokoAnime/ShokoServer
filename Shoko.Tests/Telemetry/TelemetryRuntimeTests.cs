using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Server.Settings;
using Shoko.Server.Telemetry;
using Shoko.Server.Telemetry.Database;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Telemetry;

/// <summary>
/// These listen on the process's global trace sources and change its global propagators, so they don't run in parallel.
/// </summary>
[Collection(nameof(TelemetryRuntimeCollection))]
public class TelemetryRuntimeTests
{
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public void WithoutAnEndpoint_NothingStarts()
        => Assert.Null(TelemetryRuntime.Start(new() { TracesEnabled = true, MetricsEnabled = true }, "1.0.0", NullLogger.Instance));

    [Fact]
    public void WithoutASignal_NothingStarts()
        => Assert.Null(TelemetryRuntime.Start(new() { Endpoint = "http://localhost:4318" }, "1.0.0", NullLogger.Instance));

    [Fact]
    public void WhenNothingStarts_TheServersSpansAreNotCreated()
    {
        TelemetryRuntime.Start(new(), "1.0.0", NullLogger.Instance);

        Assert.Null(ShokoTelemetry.Source.StartActivity("not-listened-to"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OnlyTheSignalsTurnedOnStart(bool traces, bool metrics)
    {
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{GetFreePort()}", TracesEnabled = traces, MetricsEnabled = metrics }, "1.0.0", NullLogger.Instance)!;
        try
        {
            Assert.Equal(traces, runtime.HasTraces);
            Assert.Equal(metrics, runtime.HasMetrics);
        }
        finally
        {
            runtime.Shutdown();
        }
    }

    [Fact]
    public async Task Traces_AreExportedToTheSignalsAddressWithTheHeaders()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var collector = new HttpListener();
        collector.Prefixes.Add($"http://127.0.0.1:{port}/");
        collector.Start();
        var received = collector.GetContextAsync();

        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{port}", TracesEnabled = true, Headers = "X-Test=hello" }, "1.0.0", NullLogger.Instance)!;
        ShokoTelemetry.Source.StartActivity("a-span")!.Dispose();
        var shutdown = Task.Run(runtime.Shutdown, cancellationToken);

        var context = await received.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.Equal("/v1/traces", context.Request.Url!.AbsolutePath);
        Assert.Equal("hello", context.Request.Headers["X-Test"]);
        context.Response.Close();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    [Fact]
    public void Shutdown_StaysWithinItsBudgetWhenTheCollectorNeverAnswers()
    {
        // Accepts connections but never answers, which is the worst a collector can do.
        using var collector = new TcpListener(IPAddress.Loopback, 0);
        collector.Start();
        var port = ((IPEndPoint)collector.LocalEndpoint).Port;
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{port}", TracesEnabled = true, MetricsEnabled = true, ExportTimeoutMs = 30000 }, "1.0.0", NullLogger.Instance)!;
        ShokoTelemetry.Source.StartActivity("a-span")!.Dispose();

        var stopwatch = Stopwatch.StartNew();
        runtime.Shutdown();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Shutdown_CanBeCalledAgainAndPutsThePropagatorBack()
    {
        var before = DistributedContextPropagator.Current;
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{GetFreePort()}", TracesEnabled = true }, "1.0.0", NullLogger.Instance)!;
        Assert.NotSame(before, DistributedContextPropagator.Current);

        runtime.Shutdown();
        runtime.Shutdown();

        Assert.Same(before, DistributedContextPropagator.Current);
        Assert.Null(ShokoTelemetry.Source.StartActivity("after-shutdown"));
    }

    [Fact]
    public void ADeadCollector_IsWarnedAboutOnce()
    {
        var logger = new CountingLogger();
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{GetFreePort()}", TracesEnabled = true }, "1.0.0", logger)!;
        ShokoTelemetry.Source.StartActivity("a-span")!.Dispose();
        runtime.Shutdown();

        Assert.Equal(1, logger.Warnings.Count(warning => warning.Contains("export is failing")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OutgoingRequests_CarryNoTraceContext(bool coreClient)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var server = new HttpListener();
        server.Prefixes.Add($"http://127.0.0.1:{port}/");
        server.Start();
        var received = server.GetContextAsync();

        using var client = coreClient ? CoreHttpClientFactory.Instance.CreateClient(CoreHttpClientFactory.DefaultClientName) : null;
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{GetFreePort()}", TracesEnabled = true }, "1.0.0", NullLogger.Instance)!;
        try
        {
            using var plainClient = coreClient ? null : new HttpClient();
            using var span = ShokoTelemetry.Source.StartActivity("a-span");
            Assert.NotNull(span);

            var request = (client ?? plainClient)!.GetAsync($"http://127.0.0.1:{port}/", cancellationToken);
            var context = await received.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            var headers = context.Request.Headers.AllKeys;
            context.Response.Close();
            await request;

            Assert.DoesNotContain("traceparent", headers, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("tracestate", headers, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            runtime.Shutdown();
        }
    }

    [Theory]
    [InlineData("https://a.example/?apikey=hunter2&b=1", "https://a.example/?apikey=Redacted&b=1")]
    [InlineData("https://a.example/?access_token=hunter2", "https://a.example/?access_token=Redacted")]
    [InlineData("user=me&pass=hunter2", "user=me&pass=Redacted")]
    [InlineData("PING s=hunter2", "PING s=Redacted")]
    [InlineData("X-Plex-Token: hunter2", "X-Plex-Token: Redacted")]
    [InlineData("""{"password":"hunter2"}""", """{"password":"Redacted"}""")]
    [InlineData("items=3 and tags=4", "items=3 and tags=4")]
    [InlineData("a plain message", "a plain message")]
    public void TheScrubber_HidesSecretsOnly(string input, string expected)
        => Assert.Equal(expected, TelemetrySecretScrubber.Scrub(input));

    [Fact]
    public void WithoutCapturingStatements_TheRedactionProcessorRemovesThemFromEverySpan()
    {
        using var activity = new Activity("statement");
        activity.SetTag("db.query.text", "SELECT 1");
        activity.SetTag("db.statement", "SELECT 2");
        activity.SetTag("db.system.name", "sqlite");

        new TelemetryRedactionProcessor().OnEnd(activity);

        Assert.Null(activity.GetTagItem("db.query.text"));
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Equal("sqlite", activity.GetTagItem("db.system.name"));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void TheDatabaseTelemetry_OnlyTurnsOnWithASignalThatCanCarryIt(bool traces, bool metrics, bool expected)
    {
        var runtime = TelemetryRuntime.Start(
            new()
            {
                Endpoint = $"http://127.0.0.1:{GetFreePort()}",
                TracesEnabled = traces,
                MetricsEnabled = metrics,
                LogsEnabled = !traces && !metrics,
                DatabaseSpansEnabled = true,
            },
            "1.0.0",
            NullLogger.Instance
        )!;
        try
        {
            Assert.Equal(expected, DatabaseTelemetry.IsEnabled);
        }
        finally
        {
            runtime.Shutdown();
        }

        Assert.False(DatabaseTelemetry.IsEnabled);
    }

    [Fact]
    public void WithoutTheDatabaseSetting_TheDatabaseTelemetryStaysOff()
    {
        var runtime = TelemetryRuntime.Start(new() { Endpoint = $"http://127.0.0.1:{GetFreePort()}", TracesEnabled = true }, "1.0.0", NullLogger.Instance)!;
        try
        {
            Assert.False(DatabaseTelemetry.IsEnabled);
        }
        finally
        {
            runtime.Shutdown();
        }
    }

    [Theory]
    [InlineData("SELECT * FROM x", "SELECT")]
    [InlineData("  \r\n  insert into x values (1)", "INSERT")]
    [InlineData("WITH a AS (SELECT 1) SELECT * FROM a", "WITH")]
    [InlineData("select(1)", "SELECT")]
    [InlineData("hunter2 is not an operation", "OTHER")]
    [InlineData("", "OTHER")]
    [InlineData(null, "OTHER")]
    public void TheOperation_IsTheStatementsFirstWordFromAClosedList(string? statement, string expected)
        => Assert.Equal(expected, DatabaseTelemetry.GetOperation(statement));

    [Theory]
    [InlineData("select this_.ID as id1_0_ FROM AnimeSeries this_ WHERE this_.ID = ?", "AnimeSeries")]
    [InlineData("INSERT INTO AnimeSeries (Name) VALUES (?)", "AnimeSeries")]
    [InlineData("UPDATE AnimeSeries SET Name = ? WHERE ID = ?", "AnimeSeries")]
    [InlineData("DELETE FROM AnimeSeries WHERE ID = @p0", "AnimeSeries")]
    [InlineData("delete from [dbo].[AnimeSeries] where ID = @p0", "AnimeSeries")]
    [InlineData("SELECT * FROM `AnimeSeries`", "AnimeSeries")]
    [InlineData("CREATE TABLE IF NOT EXISTS AnimeSeries (ID INT)", "AnimeSeries")]
    [InlineData("SELECT * FROM (SELECT * FROM AnimeSeries) a", "AnimeSeries")]
    [InlineData("SELECT 'from secret' FROM AnimeSeries", "AnimeSeries")]
    [InlineData("UPDATE x.y.z SET a = 1", null)]
    [InlineData("SELECT 1", null)]
    [InlineData("PRAGMA journal_mode", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheCollection_IsTheFirstTableAStatementNamesOutsideAnyString(string? statement, string? expected)
        => Assert.Equal(expected, DatabaseTelemetry.GetCollection(DatabaseTelemetry.SanitizeStatement(statement)));

    [Theory]
    [InlineData("SELECT * FROM AniDB_Anime WHERE AnimeID = ?", "SELECT * FROM AniDB_Anime WHERE AnimeID = ?")]
    [InlineData("SELECT * FROM x WHERE a = @p0 AND b = ?p1 AND c = :p2", "SELECT * FROM x WHERE a = @p0 AND b = ?p1 AND c = :p2")]
    [InlineData("SELECT t1.id1_62_ FROM AniDB_Anime t1", "SELECT t1.id1_62_ FROM AniDB_Anime t1")]
    [InlineData("SELECT * FROM x WHERE name = 'it''s hunter2' AND y = N'z'", "SELECT * FROM x WHERE name = ? AND y = N?")]
    [InlineData("UPDATE x SET a = 12, b = 1.5, c = 0xFF WHERE d IN (1, 2)", "UPDATE x SET a = ?, b = ?, c = ? WHERE d IN (?, ?)")]
    [InlineData("SELECT 'never closed hunter2", "SELECT ?")]
    public void AStatement_KeepsItsShapeButNotItsValues(string statement, string expected)
        => Assert.Equal(expected, DatabaseTelemetry.SanitizeStatement(statement));

    [Fact]
    public void ALongStatement_IsCut()
        => Assert.Equal(8192, DatabaseTelemetry.SanitizeStatement(new string('x', 10000))!.Length);

    [Fact]
    public void Statements_AreCapturedByDefault()
        => Assert.True(new TelemetrySettings().CaptureDatabaseStatements);

    [Fact]
    public void TheRedactionProcessor_ReplacesValuesInStatementsOfOtherInstrumentation()
    {
        using var activity = new Activity("statement");
        activity.SetTag("db.query.text", "SELECT * FROM x WHERE name = 'hunter2'");

        new TelemetryRedactionProcessor(captureDatabaseStatements: true).OnEnd(activity);

        Assert.Equal("SELECT * FROM x WHERE name = ?", activity.GetTagItem("db.query.text"));
    }

    [Fact]
    public void ARepositorySpan_WaitsForTheDatabaseTelemetry()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DatabaseTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        Assert.Null(DatabaseTelemetry.StartRepositorySpan("save", "AnimeSeries", 1));

        DatabaseTelemetry.Enable(captureStatements: false);
        try
        {
            using var span = DatabaseTelemetry.StartRepositorySpan("save", "AnimeSeries", 2);

            Assert.Equal("repository.save AnimeSeries", span!.OperationName);
            Assert.Equal("AnimeSeries", span.GetTagItem("shoko.repository.entity"));
            Assert.Equal(2, span.GetTagItem("shoko.repository.count"));
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }
    }

    [Fact]
    public void TheRedactionProcessor_HidesSecretsInUrlAndStatementAttributes()
    {
        using var activity = new Activity("a-span");
        activity.SetTag("url.full", "https://a.example/?token=hunter2");
        activity.SetTag("db.query.text", "SELECT 1 -- password=hunter2");
        activity.SetTag("other", "token=kept");

        new TelemetryRedactionProcessor(captureDatabaseStatements: true).OnEnd(activity);

        Assert.Equal("https://a.example/?token=Redacted", activity.GetTagItem("url.full"));
        Assert.Equal("SELECT ? -- password=Redacted", activity.GetTagItem("db.query.text"));
        Assert.Equal("token=kept", activity.GetTagItem("other"));
    }

    [Theory]
    [InlineData(
        "http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid=1",
        "http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid=1"
    )]
    [InlineData(
        "http://api.anidb.net:9001/httpapi?client=animeplugin&request=mylist&user=me&pass=hunter2",
        "http://api.anidb.net:9001/httpapi?client=animeplugin&request=mylist&user=Redacted&pass=Redacted"
    )]
    [InlineData("https://a.example/3/tv/1?api_key=hunter2&language=en", "https://a.example/3/tv/1?api_key=Redacted&language=en")]
    [InlineData("https://a.example/?X-Plex-Token=hunter2", "https://a.example/?X-Plex-Token=Redacted")]
    [InlineData("https://a.example/?client_secret=hunter2&sessionId=hunter2&s=hunter2", "https://a.example/?client_secret=Redacted&sessionId=Redacted&s=Redacted")]
    [InlineData("https://a.example/?api%5Fkey=hunter2", "https://a.example/?api%5Fkey=Redacted")]
    [InlineData("?token=hunter2&page=2", "?token=Redacted&page=2")]
    [InlineData("https://a.example/path", "https://a.example/path")]
    public void AUrl_KeepsWhatWasAskedForButNotItsSecrets(string url, string expected)
    {
        using var activity = new Activity("a-span");
        activity.SetTag("url.full", url);

        new TelemetryRedactionProcessor().OnEnd(activity);

        Assert.Equal(expected, activity.GetTagItem("url.full"));
    }

    private sealed class CountingLogger : ILogger
    {
        private readonly Lock _lock = new();

        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (_lock)
                    return _warnings.ToList();
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel is not LogLevel.Warning)
                return;

            lock (_lock)
                _warnings.Add(formatter(state, exception));
        }
    }
}

[CollectionDefinition(nameof(TelemetryRuntimeCollection), DisableParallelization = true)]
public sealed class TelemetryRuntimeCollection;
