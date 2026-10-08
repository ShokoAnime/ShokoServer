namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   Why a service is suspended. A provider holds at most one suspension of
///   each kind, so the kind is a suspension's identity.
/// </summary>
public enum SuspensionKind
{
    /// <summary>
    ///   The service asked for fewer requests and a wait.
    /// </summary>
    RateLimited,

    /// <summary>
    ///   The service banned the client for a while.
    /// </summary>
    Banned,

    /// <summary>
    ///   The service answered with server errors.
    /// </summary>
    ServerErrors,

    /// <summary>
    ///   The service is under heavy load and asked clients to back off.
    /// </summary>
    Overloaded,

    /// <summary>
    ///   The service refused the credentials or key it was given.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    ///   The session with the service is no longer valid.
    /// </summary>
    SessionInvalid,

    /// <summary>
    ///   The service is down for maintenance.
    /// </summary>
    Maintenance,

    /// <summary>
    ///   Any other reason, told in <see cref="Suspension.Reason"/>.
    /// </summary>
    Other,
}
