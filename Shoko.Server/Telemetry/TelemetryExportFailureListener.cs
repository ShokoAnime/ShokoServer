using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Tells the admin when exports are failing. The exporter only reports to its event source, so without this a collector
/// that is down or refusing the data would go unnoticed. Warns at most once per <see cref="WarningInterval"/>, since a
/// collector that is down fails every export.
/// </summary>
internal sealed class TelemetryExportFailureListener : EventListener
{
    private const string ExporterEventSourceName = "OpenTelemetry-Exporter-OpenTelemetryProtocol";

    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(5);

    // Assigned after the base constructor, which already reports the event sources that exist.
    private readonly ILogger _logger;

    private long _nextWarningTicks;

    public TelemetryExportFailureListener(ILogger logger)
    {
        _logger = logger;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == ExporterEventSourceName)
            EnableEvents(eventSource, EventLevel.Warning);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.Level > EventLevel.Warning || _logger is null)
            return;

        var now = Environment.TickCount64;
        var next = Interlocked.Read(ref _nextWarningTicks);
        if (now < next || Interlocked.CompareExchange(ref _nextWarningTicks, now + (long)WarningInterval.TotalMilliseconds, next) != next)
            return;

        // Names the event only: its payload can hold what the collector answered.
        _logger.LogWarning("The OTLP export is failing ({Event}). Check that the collector is up and the telemetry settings are right.", eventData.EventName);
    }
}
