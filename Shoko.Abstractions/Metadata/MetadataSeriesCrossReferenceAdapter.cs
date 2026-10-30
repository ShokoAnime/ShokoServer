using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A series-level link read through its typed contract.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal sealed class MetadataSeriesCrossReferenceAdapter<TProvider>(IMetadataSeriesCrossReference link)
    : MetadataCrossReferenceAdapter<IMetadataSeriesCrossReference, TProvider>(link), IMetadataSeriesCrossReference<TProvider>
    where TProvider : ISeries;
