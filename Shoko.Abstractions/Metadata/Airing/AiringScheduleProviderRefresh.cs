using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What one provider did during a refresh.
/// </summary>
/// <param name="ProviderID">
///   The ID of the provider.
/// </param>
/// <param name="ProviderName">
///   The name of the provider.
/// </param>
/// <param name="State">
///   What the provider did.
/// </param>
/// <param name="ErrorMessage">
///   Why the provider failed, or <c>null</c> when it did not.
/// </param>
public sealed record AiringScheduleProviderRefresh(
    Guid ProviderID,
    string ProviderName,
    AiringScheduleRefreshState State,
    string? ErrorMessage
);
