using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   One provider's place in the order a source's entity type is answered
///   in.
/// </summary>
/// <remarks>
///   Of the providers claiming a source and entity type, the first enabled
///   one answers and the rest stand by: the next enabled one takes over when
///   it is turned off or removed.
/// </remarks>
/// <param name="ProviderID">The provider's ID.</param>
/// <param name="IsEnabled">Whether it may answer.</param>
public sealed record MetadataProviderAssignment(Guid ProviderID, bool IsEnabled);
