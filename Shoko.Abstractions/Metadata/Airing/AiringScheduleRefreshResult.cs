using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What a refresh did, per provider, and the entity's schedules afterwards.
/// </summary>
/// <param name="Providers">
///   What each provider did during the refresh.
/// </param>
/// <param name="Schedules">
///   The entity's schedules after the refresh.
/// </param>
public sealed record AiringScheduleRefreshResult(
    IReadOnlyList<AiringScheduleProviderRefresh> Providers,
    IReadOnlyList<IAiringSchedule> Schedules
);
