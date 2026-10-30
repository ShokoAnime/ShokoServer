namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   An ordering export held in memory.
/// </summary>
/// <param name="Content">The file's bytes.</param>
/// <param name="Result">What was written.</param>
public sealed record MetadataOrderingExportFile(byte[] Content, MetadataOrderingExportResult Result);
