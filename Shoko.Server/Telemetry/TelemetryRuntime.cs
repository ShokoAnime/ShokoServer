using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Telemetry.Database;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Owns the OpenTelemetry providers exporting the server's telemetry over OTLP. They are built by hand as soon as the
/// settings are read, rather than through the web host, so they cover the plugin upgrades and scan, setup mode and a
/// failed start, none of which depend on a host that is never disposed.
/// </summary>
internal sealed class TelemetryRuntime
{
    /// <summary>
    /// How long a flush may hold up the process exiting, so a collector that is down costs little at exit.
    /// </summary>
    private const int ShutdownBudgetMilliseconds = 3000;

    /// <summary>
    /// Every plugin following the convention of naming its sources and meters after Shoko is exported too.
    /// </summary>
    private const string ShokoNamePattern = "Shoko.*";

    private const string DisableUriRedactionSwitch = "System.Net.Http.DisableUriRedaction";

    private static readonly string[] _builtInMeters =
    [
        "System.Runtime",
        "System.Net.Http",
        "System.Net.NameResolution",
        "MySqlConnector",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
        "Microsoft.AspNetCore.Http.Connections",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
    ];

    // Without network.peer.address, which would give every address a remote service resolves to its own series.
    private static readonly string[] _httpClientConnectionTags =
    [
        "http.connection.state",
        "network.protocol.version",
        "server.address",
        "server.port",
        "url.scheme",
    ];

    private readonly TracerProvider? _tracerProvider;

    private readonly MeterProvider? _meterProvider;

    private readonly TelemetryExportFailureListener _failureListener;

    private readonly TextMapPropagator? _previousTextMapPropagator;

    private readonly DistributedContextPropagator? _previousContextPropagator;

    private int _isShutDown;

    private TelemetryRuntime(TelemetryExportOptions options, string serviceVersion, ILogger logger)
    {
        var resource = ResourceBuilder.CreateDefault().AddService(options.Settings.ServiceName, serviceVersion: serviceVersion);
        _failureListener = new(logger);
        if (options.Settings.TracesEnabled)
        {
            _previousTextMapPropagator = Propagators.DefaultTextMapPropagator;
            _previousContextPropagator = DistributedContextPropagator.Current;
            Sdk.SetDefaultTextMapPropagator(new NoOutputTextMapPropagator());
            DistributedContextPropagator.Current = DistributedContextPropagator.CreateNoOutputPropagator();

            // The runtime otherwise cuts every outgoing URL at the query, down to `?*`. TelemetryRedactionProcessor hides
            // the secrets instead. The runtime reads this once, so a request made before this point keeps it redacted.
            AppContext.SetSwitch(DisableUriRedactionSwitch, true);
            _tracerProvider = BuildTracerProvider(options, resource);
        }

        if (options.Settings.MetricsEnabled)
            _meterProvider = BuildMeterProvider(options, resource);

        if (options.Settings.LogsEnabled)
            LoggerFactory = BuildLoggerFactory(options, resource);

        // The session factories are built later, in the late start, so this is in time for them.
        if (options.Settings.DatabaseSpansEnabled && (HasTraces || HasMetrics))
            DatabaseTelemetry.Enable(options.Settings.CaptureDatabaseStatements);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <summary>
    /// Whether traces are being exported.
    /// </summary>
    public bool HasTraces => _tracerProvider is not null;

    /// <summary>
    /// Whether metrics are being exported.
    /// </summary>
    public bool HasMetrics => _meterProvider is not null;

    /// <summary>
    /// The factory of the logger exporting the server's logs, or <c>null</c> when they are not exported.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// Starts exporting, if the settings ask for it. Never throws: telemetry is not worth failing a start over.
    /// </summary>
    /// <param name="settings">The telemetry settings.</param>
    /// <param name="serviceVersion">The server's version, to report the telemetry under.</param>
    /// <param name="logger">Told about bad settings and failing exports.</param>
    /// <returns>The runtime, or <c>null</c> when nothing is to be exported.</returns>
    public static TelemetryRuntime? Start(TelemetrySettings settings, string serviceVersion, ILogger logger)
    {
        var options = TelemetryExportOptions.Create(settings, out var warnings);
        foreach (var warning in warnings)
            logger.LogWarning("{Warning}", warning);
        if (options is null)
            return null;

        try
        {
            var runtime = new TelemetryRuntime(options, serviceVersion, logger);
            logger.LogInformation(
                "Exporting OpenTelemetry to {Endpoint} over {Protocol}. Traces: {Traces}, metrics: {Metrics}, logs: {Logs}.",
                options.Endpoint,
                settings.Protocol,
                settings.TracesEnabled,
                settings.MetricsEnabled,
                settings.LogsEnabled
            );
            return runtime;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not start the OpenTelemetry export; the server goes on without it.");
            return null;
        }
    }

    /// <summary>
    /// Flushes what is waiting to be exported and stops, within <see cref="ShutdownBudgetMilliseconds"/>. Called when
    /// the process exits, the one thing every way of stopping the server shares: the tray app never stops its host, and
    /// the host's own stopping events fire before the queue has stopped.
    /// </summary>
    public void Shutdown()
    {
        if (Interlocked.Exchange(ref _isShutDown, 1) is not 0)
            return;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        var tasks = new List<Task>(3);
        if (LoggerFactory is { } loggerFactory)
        {
            tasks.Add(Task.Run(() =>
            {
                LogService.ConnectOtlpExport(null);
                loggerFactory.Dispose();
            }));
        }

        if (_tracerProvider is { } tracerProvider)
            tasks.Add(Task.Run(() => Stop(tracerProvider, tracerProvider.Shutdown)));
        if (_meterProvider is { } meterProvider)
            tasks.Add(Task.Run(() => Stop(meterProvider, meterProvider.Shutdown)));

        try
        {
            Task.WaitAll([.. tasks], ShutdownBudgetMilliseconds);
        }
        catch (AggregateException)
        {
            // The process is leaving, and there is nothing left to do about a failed flush.
        }

        _failureListener.Dispose();
        DatabaseTelemetry.Disable();
        if (_previousTextMapPropagator is not null)
            Sdk.SetDefaultTextMapPropagator(_previousTextMapPropagator);
        if (_previousContextPropagator is not null)
            DistributedContextPropagator.Current = _previousContextPropagator;

        static void Stop(IDisposable provider, Func<int, bool> shutdown)
        {
            shutdown(ShutdownBudgetMilliseconds);
            provider.Dispose();
        }
    }

    private void OnProcessExit(object? sender, EventArgs args)
        => Shutdown();

    private static TracerProvider BuildTracerProvider(TelemetryExportOptions options, ResourceBuilder resource)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .SetSampler(new TelemetrySampler(options.Settings.TraceSampleRatio, options.Settings.JobTraceSampleRatio))
            .AddSource(ShokoNamePattern)
            .AddAspNetCoreInstrumentation(instrumentation =>
            {
                // Exception text can hold anything, and the API already reports its failures in the logs.
                instrumentation.RecordException = false;
                instrumentation.Filter = IsTraced;
                instrumentation.EnrichWithHttpResponse = DropWithoutEndpoint;
            })
            .AddHttpClientInstrumentation(instrumentation => instrumentation.RecordException = false);
        if (options.Settings.DatabaseSpansEnabled)
            builder.AddEntityFrameworkCoreInstrumentation();

        return builder
            .AddProcessor(new TelemetryRedactionProcessor(options.Settings.CaptureDatabaseStatements))
            .AddOtlpExporter(exporter => Configure(exporter, options, "traces"))
            .Build()!;
    }

    private static MeterProvider BuildMeterProvider(TelemetryExportOptions options, ResourceBuilder resource)
        => Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter(ShokoNamePattern)
            .AddMeter(_builtInMeters)
            .AddView("http.client.open_connections", new MetricStreamConfiguration { TagKeys = _httpClientConnectionTags })
            .AddView("http.client.connection.duration", new MetricStreamConfiguration { TagKeys = _httpClientConnectionTags })
            .AddOtlpExporter((exporter, reader) =>
            {
                Configure(exporter, options, "metrics");
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.Settings.MetricExportIntervalSeconds * 1000;
            })
            .Build()!;

    /// <summary>
    /// Builds the logger factory the logs of <see cref="OtlpLogTarget"/> go through. The logging rules already decided
    /// which logs to export, so this takes every level.
    /// </summary>
    private static ILoggerFactory BuildLoggerFactory(TelemetryExportOptions options, ResourceBuilder resource)
        => Microsoft.Extensions.Logging.LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(openTelemetry =>
            {
                openTelemetry.SetResourceBuilder(resource);
                openTelemetry.IncludeFormattedMessage = true;
                openTelemetry.AddProcessor(new OtlpLogTimestampProcessor());
                openTelemetry.AddOtlpExporter(exporter => Configure(exporter, options, "logs"));
            })
        );

    /// <summary>
    /// Sets every exporter option explicitly, since the options otherwise read the standard <c>OTEL_EXPORTER_OTLP_*</c>
    /// environment variables, which should not be able to change where the telemetry goes.
    /// </summary>
    internal static void Configure(OtlpExporterOptions exporter, TelemetryExportOptions options, string signal)
    {
        exporter.Endpoint = options.GetSignalEndpoint(signal);
        exporter.Protocol = options.Settings.Protocol is TelemetryProtocol.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
        exporter.Headers = options.Headers;
        exporter.TimeoutMilliseconds = options.Settings.ExportTimeoutMs;
    }

    /// <summary>
    /// Leaves out <c>/signalr</c>, whose requests last as long as the connection and would parent everything they do.
    /// </summary>
    private static bool IsTraced(HttpContext context)
        => !context.Request.Path.StartsWithSegments("/signalr");

    /// <summary>
    /// Drops requests no endpoint took, which are the web UI's static files and whatever probes the server, without
    /// needing to know the mount paths. Routing has not run yet when the filter does, so this has to wait for the answer.
    /// </summary>
    private static void DropWithoutEndpoint(Activity activity, HttpResponse response)
    {
        if (response.HttpContext.GetEndpoint() is null)
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
