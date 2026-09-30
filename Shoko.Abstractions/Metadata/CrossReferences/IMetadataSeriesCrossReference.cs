namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
/// A cross-reference keyed on a Shoko series: the whole anime is the same work
/// as the provider entry.
/// </summary>
/// <remarks>
/// A film claiming a whole anime is kept at this level too, and its
/// <see cref="IMetadataCrossReference.ProviderID"/> reads back as the film,
/// e.g. <c>&lt;source&gt;://movie/&lt;id&gt;</c>.
/// </remarks>
public interface IMetadataSeriesCrossReference : IMetadataCrossReference;

/// <summary>
/// A series cross-reference with its provider entry typed.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
public interface IMetadataSeriesCrossReference<out TProvider> : IMetadataSeriesCrossReference, IMetadataCrossReference<TProvider>
    where TProvider : IMetadata;
