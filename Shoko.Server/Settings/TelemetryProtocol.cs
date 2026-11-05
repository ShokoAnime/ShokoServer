namespace Shoko.Server.Settings;

/// <summary>
/// The OTLP protocols the server can export over.
/// </summary>
public enum TelemetryProtocol
{
    /// <summary>
    /// Protobuf over HTTP, usually on port 4318.
    /// </summary>
    HttpProtobuf = 0,

    /// <summary>
    /// gRPC, usually on port 4317.
    /// </summary>
    Grpc = 1,
}
