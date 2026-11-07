using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for exporting the server's own traces, metrics and logs over OTLP to a collector the admin runs, such as
/// Grafana Alloy. Nothing is sent anywhere until <see cref="Endpoint"/> is set and at least one signal is turned on.
/// </summary>
[Section(DisplaySectionType.Minimal)]
public class TelemetrySettings
{
    /// <summary>
    /// The collector's OTLP address, such as <c>http://localhost:4318</c>. Leave empty to turn the export off. Over
    /// <see cref="TelemetryProtocol.HttpProtobuf"/>, the path of each signal is added to it. It cannot hold a user
    /// name, a password, a query or a fragment, since it is written to the log; use <see cref="Headers"/> for those.
    /// </summary>
    [SectionName("Collector")]
    [Display(Name = "OTLP Endpoint")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_ENDPOINT")]
    [DefaultValue("")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The OTLP protocol the collector takes at <see cref="Endpoint"/>, usually port 4318 for HTTP and 4317 for gRPC.
    /// </summary>
    [SectionName("Collector")]
    [Display(Name = "OTLP Protocol")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_PROTOCOL")]
    [DefaultValue(TelemetryProtocol.HttpProtobuf)]
    public TelemetryProtocol Protocol { get; set; } = TelemetryProtocol.HttpProtobuf;

    /// <summary>
    /// Headers sent with every export, as <c>key=value,key=value</c>, for a collector that wants authentication.
    /// </summary>
    [SectionName("Collector")]
    [Display(Name = "OTLP Headers")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_HEADERS")]
    [PasswordPropertyText]
    public string Headers { get; set; } = string.Empty;

    /// <summary>
    /// How long one export may take, in milliseconds.
    /// </summary>
    [SectionName("Collector")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Export Timeout (ms)")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_TIMEOUT_MS")]
    [DefaultValue(5000)]
    [Range(100, 60000)]
    public int ExportTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// The service name the telemetry is reported under.
    /// </summary>
    [SectionName("Collector")]
    [Display(Name = "Service Name")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_SERVICE_NAME")]
    [DefaultValue("shoko-server")]
    public string ServiceName { get; set; } = "shoko-server";

    /// <summary>
    /// Export traces.
    /// </summary>
    [SectionName("Signals")]
    [Display(Name = "Export Traces")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_TRACES_ENABLED")]
    [DefaultValue(false)]
    public bool TracesEnabled { get; set; }

    /// <summary>
    /// Export metrics.
    /// </summary>
    [SectionName("Signals")]
    [Display(Name = "Export Metrics")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_METRICS_ENABLED")]
    [DefaultValue(false)]
    public bool MetricsEnabled { get; set; }

    /// <summary>
    /// Export the server's logs, from this start onwards. <see cref="LoggingSettings.DefaultOtlpLogLevel"/> sets how
    /// much of them.
    /// </summary>
    [SectionName("Signals")]
    [Display(Name = "Export Logs")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_LOGS_ENABLED")]
    [DefaultValue(false)]
    public bool LogsEnabled { get; set; }

    /// <summary>
    /// The share of traces to keep, from 0 to 1, for everything but queue jobs.
    /// </summary>
    [SectionName("Sampling")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Trace Sample Ratio")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_TRACE_SAMPLE_RATIO")]
    [DefaultValue(1.0)]
    [Range(0.0, 1.0)]
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>
    /// The share of queue job attempts to keep traces of, from 0 to 1. A large import can run tens of thousands of
    /// jobs, so lower this to keep every API request while sampling jobs. Job metrics are not sampled.
    /// </summary>
    [SectionName("Sampling")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Job Trace Sample Ratio")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_JOB_TRACE_SAMPLE_RATIO")]
    [DefaultValue(1.0)]
    [Range(0.0, 1.0)]
    public double JobTraceSampleRatio { get; set; } = 1.0;

    /// <summary>
    /// How often to export metrics, in seconds.
    /// </summary>
    [SectionName("Signals")]
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Metric Export Interval (seconds)")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_METRIC_INTERVAL_SECONDS")]
    [DefaultValue(60)]
    [Range(1, 3600)]
    public int MetricExportIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Add a span for every database command to the traces. A busy server runs a lot of them, so this is off by
    /// default.
    /// </summary>
    [SectionName("Database")]
    [Display(Name = "Database Spans")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_DB_SPANS_ENABLED")]
    [DefaultValue(false)]
    public bool DatabaseSpansEnabled { get; set; }

    /// <summary>
    /// Include the SQL text in database spans, with placeholders in place of its values, to validate the queries. Has
    /// no effect unless <see cref="DatabaseSpansEnabled"/> is on.
    /// </summary>
    [SectionName("Database")]
    [Visibility(
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(DatabaseSpansEnabled),
        ToggleWhenSetTo = true,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Capture Database Statements")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_OTLP_DB_STATEMENTS_ENABLED")]
    [DefaultValue(true)]
    public bool CaptureDatabaseStatements { get; set; } = true;
}
