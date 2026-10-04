using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Thrown by a provider asked to answer while it lacks what it needs, such
///   as an API key or credentials.
/// </summary>
/// <remarks>
///   The API answers it with <c>503 Service Unavailable</c> and no
///   <c>Retry-After</c> header, as waiting does not configure the provider.
///   A provider may derive its own exception from this one.
/// </remarks>
public class MetadataProviderNotConfiguredException : MetadataProviderUnavailableException
{
    /// <summary>
    ///   Creates the exception.
    /// </summary>
    /// <param name="source">The source whose provider is not configured.</param>
    /// <param name="message">What is missing, or <c>null</c> for a default message.</param>
    /// <param name="innerException">The failure behind it, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public MetadataProviderNotConfiguredException(MetadataSource source, string? message = null, Exception? innerException = null)
        : base(source, message ?? $"{source?.Name ?? "The metadata source"} is not configured.", null, innerException)
    {
    }
}
