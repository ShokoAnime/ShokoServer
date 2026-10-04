using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Thrown by a provider when its source cannot be reached for now, such as
///   while it answers with server errors, so the caller can try again later.
/// </summary>
/// <remarks>
///   Only for a failure that is expected to pass. The API answers it with
///   <c>502 Bad Gateway</c> and a <c>Retry-After</c> header when
///   <see cref="RetryAfter"/> is known. A provider may derive its own
///   exception from this one.
/// </remarks>
public class MetadataProviderUnavailableException : Exception
{
    /// <summary>
    ///   Creates the exception.
    /// </summary>
    /// <param name="source">The source that could not be reached.</param>
    /// <param name="message">What went wrong, or <c>null</c> for a default message.</param>
    /// <param name="retryAfter">How long to wait before trying again, if known.</param>
    /// <param name="innerException">The failure behind it, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public MetadataProviderUnavailableException(MetadataSource source, string? message = null, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message ?? $"{source?.Name ?? "The metadata source"} cannot be reached right now.", innerException)
    {
        ArgumentNullException.ThrowIfNull(source);

        MetadataSource = source;
        RetryAfter = retryAfter;
    }

    /// <summary>
    ///   The source that could not be reached. Named apart from
    ///   <see cref="Exception.Source"/>, which is the failing assembly's name.
    /// </summary>
    public MetadataSource MetadataSource { get; }

    /// <summary>
    ///   How long to wait before trying again, or <c>null</c> when
    ///   the provider does not know.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
