using System.Diagnostics;

namespace Shoko.Server.Telemetry;

/// <summary>
/// The server's own trace source. Its spans go nowhere and cost next to nothing until <see cref="TelemetryRuntime"/>
/// listens to them.
/// </summary>
internal static class ShokoTelemetry
{
    /// <summary>
    /// The name of the server's trace source.
    /// </summary>
    public const string ServerSourceName = "Shoko.Server";

    /// <summary>
    /// The server's trace source.
    /// </summary>
    public static readonly ActivitySource Source = new(ServerSourceName);
}
