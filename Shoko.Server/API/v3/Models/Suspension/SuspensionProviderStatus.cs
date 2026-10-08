using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Suspension;

/// <summary>
/// A suspension provider and the suspensions it holds now.
/// </summary>
public class SuspensionProviderStatus
{
    /// <summary>
    /// Describes a provider's status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is <c>null</c>.</exception>
    public SuspensionProviderStatus(SuspensionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        ID = status.Provider.ID;
        Plugin = new(status.Provider.PluginInfo);
        Name = status.Provider.Name;
        Description = status.Provider.Description;
        IsSuspended = status.IsSuspended;
        ResumesAt = status.ResumesAt;
        RetryAfterSeconds = status.IsSuspended ? MetadataPauseResponses.RetryAfterSeconds(status.ResumesAt is { } end ? Remaining(end) : null) : null;
        Suspensions = [.. status.Suspensions.Select(suspension => new SuspensionDetails(suspension))];
    }

    /// <summary>
    /// The provider's unique ID.
    /// </summary>
    [Required]
    public Guid ID { get; init; }

    /// <summary>
    /// The plugin the provider belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; }

    /// <summary>
    /// The display name of the service or channel.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// What is suspended, if the provider says.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether any suspension is on.
    /// </summary>
    [Required]
    public bool IsSuspended { get; init; }

    /// <summary>
    /// When the last suspension ends, in UTC, when every one has an end.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    /// The seconds to wait, as a <c>Retry-After</c> header would send them,
    /// when suspended.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// The suspensions on now, at most one per kind. Empty while running.
    /// </summary>
    [Required]
    public IReadOnlyList<SuspensionDetails> Suspensions { get; init; }

    /// <summary>
    /// How long until a time, never below zero.
    /// </summary>
    /// <param name="end">The time, in UTC.</param>
    /// <returns>The time left.</returns>
    internal static TimeSpan Remaining(DateTime end)
        => end - DateTime.UtcNow is { Ticks: > 0 } remaining ? remaining : TimeSpan.Zero;
}

/// <summary>
/// One reason a service is suspended.
/// </summary>
public class SuspensionDetails
{
    /// <summary>
    /// Describes a suspension.
    /// </summary>
    /// <param name="suspension">The suspension.</param>
    /// <exception cref="ArgumentNullException"><paramref name="suspension"/> is <c>null</c>.</exception>
    public SuspensionDetails(Abstractions.Connectivity.Suspensions.Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);

        Kind = suspension.Kind;
        Reason = suspension.Reason;
        RaisedAt = suspension.RaisedAt;
        ResumesAt = suspension.ResumesAt;
        RetryAfterSeconds = MetadataPauseResponses.RetryAfterSeconds(suspension.GetRemainingTime());
        IsLiftable = suspension.IsLiftable;
    }

    /// <summary>
    /// Why the service is suspended.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public SuspensionKind Kind { get; init; }

    /// <summary>
    /// A detail only the service knows. When absent, word it from
    /// <see cref="Kind"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the suspension was first reported, in UTC.
    /// </summary>
    [Required]
    public DateTime RaisedAt { get; init; }

    /// <summary>
    /// When the suspension ends, in UTC, if it has an end.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    /// The seconds to wait, as a <c>Retry-After</c> header would send them.
    /// </summary>
    [Required]
    public int RetryAfterSeconds { get; init; }

    /// <summary>
    /// Whether an admin may lift the suspension through
    /// <c>POST /api/v3/Suspension/{providerID}/{kind}/Lift</c>.
    /// </summary>
    [Required]
    public bool IsLiftable { get; init; }
}
