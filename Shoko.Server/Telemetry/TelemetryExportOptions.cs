using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Settings;

namespace Shoko.Server.Telemetry;

/// <summary>
/// The telemetry settings, checked and ready to export with. Environment variables skip the settings schema, so every
/// value is checked here. A bad value turns the export off with a warning rather than failing the server's start.
/// </summary>
internal sealed class TelemetryExportOptions
{
    private TelemetryExportOptions(TelemetrySettings settings, Uri endpoint, string headers)
    {
        Settings = settings;
        Endpoint = endpoint;
        Headers = headers;
    }

    /// <summary>
    /// The settings these options were made from.
    /// </summary>
    public TelemetrySettings Settings { get; }

    /// <summary>
    /// The collector's address, without a trailing slash.
    /// </summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// The headers to export with, in the form the exporter takes.
    /// </summary>
    public string Headers { get; }

    /// <summary>
    /// Gets the address to export a signal to. A programmatic exporter endpoint is used as the full address over HTTP,
    /// so the signal's path is added here. gRPC takes the collector's address as it is.
    /// </summary>
    /// <param name="signal">The signal's path segment: <c>traces</c>, <c>metrics</c> or <c>logs</c>.</param>
    /// <returns>The address.</returns>
    public Uri GetSignalEndpoint(string signal)
        => Settings.Protocol is TelemetryProtocol.Grpc ? Endpoint : new($"{Endpoint.AbsoluteUri.TrimEnd('/')}/v1/{signal}");

    /// <summary>
    /// Checks the settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="warnings">What an admin should be told, without any header value.</param>
    /// <returns>The options, or <c>null</c> when nothing should be exported.</returns>
    public static TelemetryExportOptions? Create(TelemetrySettings settings, out IReadOnlyList<string> warnings)
    {
        var found = new List<string>();
        warnings = found;
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            return null;

        if (!settings.TracesEnabled && !settings.MetricsEnabled && !settings.LogsEnabled)
        {
            found.Add("An OTLP endpoint is set, but traces, metrics and logs are all turned off, so nothing will be exported.");
            return null;
        }

        var endpoint = ParseEndpoint(settings.Endpoint.Trim(), found);
        var headers = ParseHeaders(settings.Headers, found);
        if (!Enum.IsDefined(settings.Protocol))
            found.Add($"The OTLP protocol {(int)settings.Protocol} is not a known one.");
        if (settings.ExportTimeoutMs <= 0)
            found.Add("The OTLP export timeout must be above zero.");
        if (settings.MetricExportIntervalSeconds <= 0)
            found.Add("The metric export interval must be above zero.");
        if (string.IsNullOrWhiteSpace(settings.ServiceName))
            found.Add("The OTLP service name cannot be empty.");
        if (!IsRatio(settings.TraceSampleRatio))
            found.Add("The trace sample ratio must be between 0 and 1.");
        if (!IsRatio(settings.JobTraceSampleRatio))
            found.Add("The job trace sample ratio must be between 0 and 1.");
        if (endpoint is null || headers is null || found.Count > 0)
        {
            found.Add("The OpenTelemetry export is turned off until the settings are fixed.");
            return null;
        }

        if (headers.Length > 0 && endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
            found.Add("OTLP headers are being sent over plain HTTP to another machine, so anything secret in them can be read on the network.");

        return new(settings, endpoint, headers);
    }

    private static bool IsRatio(double value)
        => value is >= 0 and <= 1;

    private static Uri? ParseEndpoint(string value, List<string> warnings)
    {
        // Only says what is wrong with the address, since it could hold a password.
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            warnings.Add("The OTLP endpoint must be an absolute http or https address.");
            return null;
        }

        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            warnings.Add("The OTLP endpoint cannot hold a user name, password, query or fragment, since it is written to the log. Use the OTLP headers instead.");
            return null;
        }

        return new(uri.AbsoluteUri.TrimEnd('/'));
    }

    private static string? ParseHeaders(string? value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var pairs = value.Split(',', StringSplitOptions.TrimEntries).Select(pair => pair.Split('=', 2, StringSplitOptions.TrimEntries)).ToList();
        if (pairs.Any(pair => pair.Length != 2 || pair[0].Length == 0 || pair[1].Length == 0 || pair.Any(part => part.Any(char.IsControl))))
        {
            warnings.Add("The OTLP headers must be written as key=value,key=value.");
            return null;
        }

        return string.Join(',', pairs.Select(pair => $"{pair[0]}={pair[1]}"));
    }
}
