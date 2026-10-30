namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   A whole anime that is the same work as one of your series, or as one of
///   your films claiming the whole anime. Its
///   <see cref="MetadataLinkData.ProviderID"/> names a series or a film, and
///   reads back as the kind it was written as.
/// </summary>
public sealed record MetadataSeriesLinkData : MetadataLinkData;
