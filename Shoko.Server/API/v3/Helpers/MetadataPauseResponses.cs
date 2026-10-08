using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// The answers the generic metadata routes give while a source is suspended,
/// while its provider cannot be reached, or while it is not configured.
/// </summary>
public static class MetadataPauseResponses
{
    /// <summary>
    /// How many seconds to wait when nothing tells how long a provider stays
    /// out of reach.
    /// </summary>
    public const int DefaultRetryAfterSeconds = 60;

    /// <summary>
    /// How many whole seconds are left of a source's suspensions, rounded up.
    /// </summary>
    /// <param name="status">What holds the source back.</param>
    /// <returns>
    /// The seconds left, or <see cref="DefaultRetryAfterSeconds"/> when a
    /// suspension names no end, so a caller never retries at once.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is <c>null</c>.</exception>
    public static int RetryAfterSeconds(SourceSuspension status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return RetryAfterSeconds(status.GetRemainingTime());
    }

    /// <summary>
    /// How many whole seconds are left of a wait, rounded up.
    /// </summary>
    /// <param name="remaining">The time left, or <c>null</c> when it has no end.</param>
    /// <returns>
    /// The seconds left, or <see cref="DefaultRetryAfterSeconds"/> without an
    /// end, so a caller never retries at once.
    /// </returns>
    public static int RetryAfterSeconds(TimeSpan? remaining)
        => remaining is { } left ? (int)Math.Ceiling(left.TotalSeconds) : DefaultRetryAfterSeconds;

    /// <summary>
    /// Sets the <c>Retry-After</c> header of an answer.
    /// </summary>
    /// <param name="response">The answer.</param>
    /// <param name="seconds">The seconds to wait.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <c>null</c>.</exception>
    public static void SetRetryAfter(HttpResponse response, int seconds)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Headers.RetryAfter = Math.Max(0, seconds).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A <c>503 Service Unavailable</c> problem with a <c>Retry-After</c>
    /// header, for a request refused while its source is suspended.
    /// </summary>
    /// <param name="response">The answer the header is set on.</param>
    /// <param name="source">The suspended source.</param>
    /// <param name="status">What holds the source back.</param>
    /// <returns>The answer, saying why the source is suspended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/>, <paramref name="source"/> or <paramref name="status"/> is <c>null</c>.</exception>
    public static ObjectResult Paused(HttpResponse response, MetadataSource source, SourceSuspension status)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(status);

        SetRetryAfter(response, RetryAfterSeconds(status));
        var title = $"{source.Name} is suspended.";
        var kinds = string.Join(", ", status.Suspensions.Select(suspension => suspension.Kind).Distinct());
        return Problem(title, status.Reason ?? (kinds.Length > 0 ? $"{source.Name} is suspended ({kinds})." : title), source, null);
    }

    /// <summary>
    /// A <c>503 Service Unavailable</c> problem without a <c>Retry-After</c>
    /// header, for a request refused while the provider that would answer it
    /// is not configured, as waiting does not configure it.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The answer, naming the provider and what it is missing when it says.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public static ObjectResult NotConfigured(MetadataProviderInfo provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var title = $"{provider.Name} is not configured.";
        return Problem(title, provider.Provider.NotConfiguredReason is { Length: > 0 } reason ? reason : title, provider.Source, provider.ID);
    }

    /// <summary>
    /// Refuses a request the provider that would answer it cannot take now:
    /// while it is not configured, then while its source is suspended.
    /// </summary>
    /// <param name="response">The answer a <c>Retry-After</c> header is set on while suspended.</param>
    /// <param name="source">The source.</param>
    /// <param name="provider">The provider that would answer, or <c>null</c> when none would.</param>
    /// <param name="status">What holds the source back.</param>
    /// <returns>The refusal, or <c>null</c> to go ahead.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/>, <paramref name="source"/> or <paramref name="status"/> is <c>null</c>.</exception>
    public static ObjectResult? Refuse(HttpResponse response, MetadataSource source, MetadataProviderInfo? provider, SourceSuspension status)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(status);

        if (provider is { Provider.IsConfigured: false })
            return NotConfigured(provider);

        return status.IsSuspended ? Paused(response, source, status) : null;
    }

    /// <summary>
    /// Whether every enabled provider of a source is configured, and what the
    /// first one that is not is missing.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="providers">Every registered provider.</param>
    /// <returns>
    /// Whether the source is configured, and the reason the first provider
    /// that is not gives, if any.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="providers"/> is <c>null</c>.</exception>
    public static (bool IsConfigured, string? Reason) GetConfiguration(MetadataSource source, IEnumerable<MetadataProviderInfo> providers)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(providers);

        return providers.FirstOrDefault(info => info.Enabled && info.Source == source && !info.Provider.IsConfigured) is { } unconfigured
            ? (false, unconfigured.Provider.NotConfiguredReason is { Length: > 0 } reason ? reason : null)
            : (true, null);
    }

    /// <summary>
    /// A <c>503 Service Unavailable</c> problem.
    /// </summary>
    /// <param name="title">The short summary.</param>
    /// <param name="detail">What went wrong.</param>
    /// <param name="source">The source, sent as the <c>source</c> extension.</param>
    /// <param name="providerID">The provider, sent as the <c>providerID</c> extension, if one is named.</param>
    /// <returns>The answer.</returns>
    internal static ObjectResult Problem(string title, string detail, MetadataSource source, Guid? providerID)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = title, Detail = detail };
        problem.Extensions["source"] = source.Value;
        if (providerID is { } id)
            problem.Extensions["providerID"] = id;
        return new(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }
}
