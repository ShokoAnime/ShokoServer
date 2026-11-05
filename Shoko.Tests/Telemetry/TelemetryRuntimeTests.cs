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

    [Fact]
    public void TheRedactionProcessor_HidesSecretsInUrlAndStatementAttributes()
    {
        using var activity = new Activity("a-span");
        activity.SetTag("url.full", "https://a.example/?token=hunter2");
        activity.SetTag("db.query.text", "SELECT 1 -- password=hunter2");
        activity.SetTag("other", "token=kept");

        new TelemetryRedactionProcessor(captureDatabaseStatements: true).OnEnd(activity);

        Assert.Equal("https://a.example/?token=Redacted", activity.GetTagItem("url.full"));
        Assert.Equal("SELECT 1 -- password=Redacted", activity.GetTagItem("db.query.text"));
        Assert.Equal("token=kept", activity.GetTagItem("other"));
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
