using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Services;
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
        var requestSpans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = requestSpan =>
            {
                if (Equals(requestSpan.GetTagItem("server.port"), port))
                    requestSpans.Add(requestSpan);
            },
        };
        ActivitySource.AddActivityListener(listener);

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

            // Keeping the context to itself must not cost the request its span.
            Assert.Equal(span.TraceId, Assert.Single(requestSpans).TraceId);
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

    private sealed class ReadItem
    {
        public int ID { get; init; }

        public int Group { get; init; }
    }

    [Fact]
    public void RepositoryReads_AreTalliedOnTheLocalRootSpanAndCounted()
    {
        using var source = new ActivitySource(nameof(RepositoryReads_AreTalliedOnTheLocalRootSpanAndCounted));
        using var spans = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(spans);

        var counted = new List<bool?>();
        using var meter = new MeterListener();
        meter.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "shoko.repository.reads")
                listener.EnableMeasurementEvents(instrument);
        };
        meter.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var pairs = tags.ToArray();
            if (Equals(pairs.Single(tag => tag.Key == "shoko.repository.entity").Value, nameof(ReadItem)))
                counted.Add(pairs.Single(tag => tag.Key == "shoko.repository.found").Value as bool?);
        });
        meter.Start();

        var cache = new PocoCache<int, ReadItem>([new() { ID = 1, Group = 5 }], item => item.ID);
        var index = cache.CreateIndex(item => item.Group);
        using var root = source.StartActivity("request")!;

        // Off, nothing is kept.
        cache.Get(1);
        DatabaseTelemetry.WriteRepositoryReads(root);
        Assert.Null(root.GetTagItem("shoko.repository.reads"));

        DatabaseTelemetry.Enable(captureStatements: false);
        try
        {
            using (source.StartActivity("child"))
            {
                cache.Get(1);
                cache.Get(2);
                cache.GetAll();
                index.GetOne(5);
                index.GetMultiple(6);
            }
        }
        finally
        {
            DatabaseTelemetry.Disable();
        }

        new RepositoryReadsProcessor().OnEnd(root);

        Assert.Equal(5L, root.GetTagItem("shoko.repository.reads"));
        Assert.Equal($"{nameof(ReadItem)}=5", root.GetTagItem("shoko.repository.reads.by_entity"));
        Assert.Equal(5, counted.Count);
        Assert.Equal(2, counted.Count(found => found is false));
    }

    private sealed class ManifestHandler : HttpMessageHandler
    {
        public bool Fails { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Fails
                ? throw new HttpRequestException("down")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"Stable\":[],\"Dev\":[]}") });
    }

    [Fact]
    public async Task TheManifestCache_ReportsItsHitsMissesAndStaleCopies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = Directory.CreateTempSubdirectory("shoko-telemetry-");
        try
        {
            var name = $"{Guid.NewGuid()}.json";
            var results = new List<object?>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ShokoTelemetry.ServerSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = span =>
                {
                    if (Equals(span.GetTagItem("shoko.cache.name"), name))
                        results.Add(span.GetTagItem("shoko.cache.result"));
                },
            };
            ActivitySource.AddActivityListener(listener);

            var handler = new ManifestHandler();
            var clients = new Mock<IHttpClientFactory>();
            clients.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(applicationPaths => applicationPaths.DataPath).Returns(folder.FullName);
            var reader = new ReleaseManifestReader(NullLogger<ReleaseManifestReader>.Instance, clients.Object, paths.Object);

            await reader.FetchManifestAsync("http://manifest.test/", name, false, cancellationToken);
            await reader.FetchManifestAsync("http://manifest.test/", name, false, cancellationToken);
            handler.Fails = true;
            await reader.FetchManifestAsync("http://manifest.test/", name, true, cancellationToken);

            Assert.Equal([ShokoTelemetry.CacheMiss, ShokoTelemetry.CacheHit, ShokoTelemetry.CacheStale], results);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task TheAnidbReleaseCache_ReportsAMiss()
    {
        var results = new List<object?>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ShokoTelemetry.ServerSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span =>
            {
                if (Equals(span.GetTagItem("shoko.cache.name"), "anidb-release"))
                    results.Add(span.GetTagItem("shoko.cache.result"));
            },
        };
        ActivitySource.AddActivityListener(listener);
        var provider = new AnidbReleaseProvider(null!, null!, null!, null!, null!, null!, null!);

        // Not one of AniDB's, so it goes no further than the cache.
        Assert.Null(await provider.GetReleaseInfoById("elsewhere://1", TestContext.Current.CancellationToken));

        Assert.Equal([ShokoTelemetry.CacheMiss], results);
    }

    [Fact]
    public void AHashProviderSpan_SaysWhatItWasAskedFor()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ShokoTelemetry.ServerSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var span = ShokoTelemetry.StartHashProvider("Built-In", ["SHA1", "ED2K"]);

        Assert.Equal("video.hash Built-In", span!.OperationName);
        Assert.Equal("Built-In", span.GetTagItem("shoko.video.hash.provider"));
        Assert.Equal("ED2K,SHA1", span.GetTagItem("shoko.video.hash.types"));
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
