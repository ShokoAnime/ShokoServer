using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// The suspensions holding back a metadata source, merged from every
/// suspension provider holding one of its providers.
/// </summary>
public sealed record SourceSuspension
{
    /// <summary>
    /// A source nothing holds back.
    /// </summary>
    public static SourceSuspension None { get; } = new() { Suspensions = [] };

    /// <summary>
    /// The suspensions on now, from every provider holding the source.
    /// </summary>
    public required IReadOnlyList<Suspension> Suspensions { get; init; }

    /// <summary>
    /// Whether any suspension holds the source back.
    /// </summary>
    public bool IsSuspended => Suspensions.Count > 0;

    /// <summary>
    /// The longest-blocking suspension: the one ending last, one without an
    /// end before all, or <c>null</c> when there is none.
    /// </summary>
    public Suspension? Longest => Suspensions
        .OrderByDescending(suspension => suspension.ResumesAt ?? DateTime.MaxValue)
        .FirstOrDefault();

    /// <summary>
    /// The reason of the longest-blocking suspension, which may be
    /// <c>null</c> even while suspended.
    /// </summary>
    public string? Reason => Longest?.Reason;

    /// <summary>
    /// When the last suspension ends, in UTC, or <c>null</c> when one has no
    /// end or there are none.
    /// </summary>
    public DateTime? ResumesAt => Longest?.ResumesAt;

    /// <summary>
    /// How long until <see cref="ResumesAt"/>, never below zero.
    /// </summary>
    /// <returns>The time left, or <c>null</c> without a <see cref="ResumesAt"/>.</returns>
    public TimeSpan? GetRemainingTime()
        => Longest?.GetRemainingTime();

    /// <summary>
    /// Merges the statuses of the providers holding a source.
    /// </summary>
    /// <param name="statuses">The statuses.</param>
    /// <returns>The merged suspension.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="statuses"/> is <c>null</c>.</exception>
    public static SourceSuspension From(IEnumerable<SuspensionStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var suspensions = statuses.SelectMany(status => status.Suspensions).ToList();
        return suspensions.Count is 0 ? None : new() { Suspensions = suspensions };
    }

    /// <summary>
    /// Reads what holds a source back now.
    /// </summary>
    /// <param name="service">The suspension service.</param>
    /// <param name="source">The source.</param>
    /// <returns>The merged suspension.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> or <paramref name="source"/> is <c>null</c>.</exception>
    public static SourceSuspension For(ISuspensionService service, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(service);
        return From(service.GetForSource(source));
    }
}
