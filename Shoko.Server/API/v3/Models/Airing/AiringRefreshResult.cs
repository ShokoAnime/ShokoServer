using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// What every enabled provider did for one refresh request, and the entity's
/// schedules once the work was done.
/// </summary>
public class AiringRefreshResult
{
    /// <summary>
    /// What each provider did. A provider that failed is reported here rather
    /// than sinking the others.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringRefreshProvider> Providers { get; init; }

    /// <summary>
    /// The entity's schedules after the refresh.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringSchedule> Schedules { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringRefreshResult"/>
    /// class.
    /// </summary>
    /// <param name="result">The refresh result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    public AiringRefreshResult(AiringScheduleRefreshResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Providers = [.. result.Providers.Select(provider => new AiringRefreshProvider(provider))];
        Schedules = [.. result.Schedules.Select(schedule => new AiringSchedule(schedule))];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringRefreshResult"/>
    /// class from the pieces, for a wait that ran out of time before the
    /// providers were done.
    /// </summary>
    /// <param name="providers">What each provider did.</param>
    /// <param name="schedules">The entity's schedules as they are now.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> or <paramref name="schedules"/> is <c>null</c>.</exception>
    public AiringRefreshResult(IEnumerable<AiringScheduleProviderRefresh> providers, IEnumerable<IAiringSchedule> schedules)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(schedules);

        Providers = [.. providers.Select(provider => new AiringRefreshProvider(provider))];
        Schedules = [.. schedules.Select(schedule => new AiringSchedule(schedule))];
    }
}

/// <summary>
/// What one provider did for a refresh request.
/// </summary>
/// <param name="refresh">The provider's part of the refresh result.</param>
/// <exception cref="ArgumentNullException"><paramref name="refresh"/> is <c>null</c>.</exception>
public class AiringRefreshProvider(AiringScheduleProviderRefresh refresh)
{
    /// <summary>
    /// The ID of the provider.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = refresh.ProviderID;

    /// <summary>
    /// The name of the provider.
    /// </summary>
    [Required]
    public string Name { get; init; } = refresh.ProviderName;

    /// <summary>
    /// What the provider did.
    /// </summary>
    [Required]
    public AiringScheduleRefreshState State { get; init; } = refresh.State;

    /// <summary>
    /// Why the provider failed, when it did.
    /// </summary>
    public string? ErrorMessage { get; init; } = refresh.ErrorMessage;
}
