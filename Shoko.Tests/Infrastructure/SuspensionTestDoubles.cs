using System;
using System.Collections.Generic;
using Moq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
///   Doubles for the suspension service, for code that only reads whether a
///   source is held back.
/// </summary>
public static class SuspensionTestDoubles
{
    /// <summary>
    ///   A suspension service holding nothing back.
    /// </summary>
    /// <returns>The mock.</returns>
    public static Mock<ISuspensionService> Service()
    {
        var service = new Mock<ISuspensionService>();
        service.Setup(s => s.GetForSource(It.IsAny<MetadataSource>())).Returns([]);
        service.Setup(s => s.GetAll()).Returns([]);
        return service;
    }

    /// <summary>
    ///   Makes the service hold a source back with one suspension.
    /// </summary>
    /// <param name="service">The mock.</param>
    /// <param name="source">The source.</param>
    /// <param name="resumesAt">When it ends, or <c>null</c>.</param>
    /// <param name="reason">The detail, or <c>null</c>.</param>
    /// <param name="kind">The kind.</param>
    public static void Suspend(
        this Mock<ISuspensionService> service,
        MetadataSource source,
        DateTime? resumesAt = null,
        string? reason = null,
        SuspensionKind kind = SuspensionKind.RateLimited
    )
        => service.Setup(s => s.GetForSource(source)).Returns([Status(resumesAt, reason, kind)]);

    /// <summary>
    ///   A suspended status of a provider with one suspension.
    /// </summary>
    /// <param name="resumesAt">When it ends, or <c>null</c>.</param>
    /// <param name="reason">The detail, or <c>null</c>.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="heldProviderTypes">The provider types it holds back, or none.</param>
    /// <returns>The status.</returns>
    public static SuspensionStatus Status(
        DateTime? resumesAt = null,
        string? reason = null,
        SuspensionKind kind = SuspensionKind.RateLimited,
        IReadOnlyList<Type>? heldProviderTypes = null
    )
    {
        IReadOnlyList<Suspension> suspensions =
        [
            new()
            {
                Kind = kind,
                Reason = reason,
                RaisedAt = DateTime.UtcNow,
                ResumesAt = resumesAt,
            },
        ];
        return new()
        {
            Provider = new()
            {
                ID = Guid.NewGuid(),
                Provider = Mock.Of<ISuspensionProvider>(),
                PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(SuspensionTestDoubles), Guid.NewGuid()),
                Name = "Fake",
                HeldProviderTypes = heldProviderTypes ?? [],
            },
            Suspensions = suspensions,
            IsSuspended = true,
            ResumesAt = resumesAt,
        };
    }
}
