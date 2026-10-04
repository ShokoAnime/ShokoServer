using System;
using System.Linq;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.User;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling;

/// <summary>
///   Carries the actor across the queue: the user ID and device name of the
///   current <see cref="ActorContext"/> are stored with a job, and the token is
///   looked up again when it runs. The queue holds such jobs while the
///   database is blocked, as the repositories cannot be read before then.
/// </summary>
/// <remarks>
///   The repositories are taken lazily: the scheduler takes this accessor,
///   and resolving a cached repository builds every one of them, some of
///   which take the scheduler.
/// </remarks>
public sealed class JobActorAccessor : IJobActorAccessor
{
    private readonly ISystemService _systemService;

    private readonly Lazy<JMMUserRepository> _userRepository;

    private readonly Lazy<AuthTokensRepository> _authTokensRepository;

    /// <summary>
    ///   Creates the accessor.
    /// </summary>
    /// <param name="systemService">Tells whether the database can be read yet.</param>
    /// <param name="userRepository">Where the user is looked up again.</param>
    /// <param name="authTokensRepository">Where the token is looked up again.</param>
    public JobActorAccessor(ISystemService systemService, Lazy<JMMUserRepository> userRepository, Lazy<AuthTokensRepository> authTokensRepository)
    {
        _systemService = systemService;
        _userRepository = userRepository;
        _authTokensRepository = authTokensRepository;
        _systemService.DatabaseBlockedChanged += (_, _) => CanRestoreChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public bool CanRestore => !_systemService.IsDatabaseBlocked;

    /// <inheritdoc />
    public event EventHandler? CanRestoreChanged;

    /// <inheritdoc />
    public JobActor? Capture()
        => ActorContext.CurrentActor is { } token ? new JobActor(token.User.LocalID, token.Device) : null;

    /// <inheritdoc />
    public IDisposable Restore(JobActor? actor)
        => ActorContext.Begin(actor is { } value ? Find(value) : null);

    /// <summary>
    ///   Looks the stored actor up again, so a token revoked or expired since
    ///   the job was queued, or a user removed since, runs it for the system.
    /// </summary>
    /// <param name="actor">The stored actor.</param>
    /// <returns>The token, or <c>null</c> when it is gone.</returns>
    internal ApiToken? Find(JobActor actor)
    {
        if (_userRepository.Value.GetByID(actor.UserID) is not { } user)
            return null;

        var now = DateTime.Now;
        var token = _authTokensRepository.Value.GetByUserID(actor.UserID)
            .Where(t => !string.IsNullOrEmpty(t.Token) && t.DeviceName.Trim().Equals(actor.DeviceName.Trim(), StringComparison.InvariantCultureIgnoreCase))
            .Where(t => t.ExpiresAt is not { } expiresAt || expiresAt > now)
            .OrderBy(t => t.ExpiresAt.HasValue)
            .FirstOrDefault();
        return token is null ? null : new ApiToken(user, token.DeviceName, token.Token, token.ExpiresAt);
    }
}
