namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   A suspension that ended, and how.
/// </summary>
/// <param name="Suspension">The suspension as it was.</param>
/// <param name="Cause">How it ended.</param>
public sealed record SuspensionRemoval(Suspension Suspension, SuspensionRemovalCause Cause);
