namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A network one entry aired or streamed on. A network not stored yet is
///   kept as a stub.
/// </summary>
public sealed record MetadataEntryNetworkData
{
    /// <summary>
    ///   The network.
    /// </summary>
    public required MetadataGuid NetworkID { get; init; }

    /// <summary>
    ///   The network's name, kept on the stub the core makes when the network
    ///   is not stored yet.
    /// </summary>
    public string? NetworkName { get; init; }
}
