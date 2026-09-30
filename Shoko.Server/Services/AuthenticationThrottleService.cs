using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <inheritdoc cref="IAuthenticationThrottleService"/>
public class AuthenticationThrottleService : IAuthenticationThrottleService, IDisposable
{
    private const int _sweepThreshold = 4096;

    /// <summary>
    /// The key of the username <see cref="ThrottleAuthentication"/> was given, kept in the request's
    /// items so a failure registered later in the same request can name it.
    /// </summary>
    private static readonly object _usernameItemKey = new();

    private readonly ILogger<AuthenticationThrottleService> _logger;

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.InvariantCultureIgnoreCase);

    // Swapped as a whole on every settings save, so a single call never mixes old and new values.
    private volatile Policy _policy;

    public AuthenticationThrottleService(ILogger<AuthenticationThrottleService> logger, ConfigurationProvider<ServerSettings> settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _policy = Policy.From(settingsProvider.Load().Web.AuthenticationThrottle);
        _settingsProvider.Saved += OnSettingsSaved;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _settingsProvider.Saved -= OnSettingsSaved;
        GC.SuppressFinalize(this);
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
        => _policy = Policy.From(eventArgs.Configuration.Web.AuthenticationThrottle);

    /// <inheritdoc/>
    public event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;

    /// <inheritdoc/>
    public int MaxFailedAttempts => _policy.MaxFailedAttempts;

    /// <inheritdoc/>
    public TimeSpan AttemptWindow => _policy.AttemptWindow;

    /// <inheritdoc/>
    public TimeSpan InitialLockout => _policy.InitialLockout;

    /// <inheritdoc/>
    public TimeSpan MaxLockout => _policy.MaxLockout;

    /// <inheritdoc/>
    public TimeSpan? GetRemainingLockout(HttpContext context)
        => GetRemainingLockoutForKey(ClientKey(context));

    /// <inheritdoc/>
    public TimeSpan? GetRemainingLockout(HubCallerContext context)
        => GetRemainingLockoutForKey(ClientKey(context));

    /// <inheritdoc/>
    public TimeSpan? GetRemainingLockout(IUser user)
        => GetRemainingLockout(user.Username);

    /// <inheritdoc/>
    public void RegisterFailure(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (RegisterFailureForKey(ClientKey(address)) is { } outcome)
        {
            var username = context.Items.TryGetValue(_usernameItemKey, out var value) ? value as string : null;
            OnAuthenticationFailed(address, username, null, context.Request.Path.Value, outcome);
        }
    }

    /// <inheritdoc/>
    public void RegisterFailure(HubCallerContext context)
    {
        var address = ClientAddress(context);
        if (RegisterFailureForKey(ClientKey(address)) is { } outcome)
            OnAuthenticationFailed(address, null, null, context.GetHttpContext()?.Request.Path.Value, outcome);
    }

    /// <inheritdoc/>
    public void RegisterFailure(IUser user)
    {
        if (RegisterFailureForKey(UserKey(user.Username)) is { } outcome)
            OnAuthenticationFailed(null, user.Username, user, null, outcome);
    }

    /// <inheritdoc/>
    public void Reset(HttpContext context)
        => ResetForKey(ClientKey(context));

    /// <inheritdoc/>
    public void Reset(HubCallerContext context)
        => ResetForKey(ClientKey(context));

    /// <inheritdoc/>
    public void Reset(IUser user)
        => Reset(user.Username);

    /// <inheritdoc/>
    public StatusCodeResult? ThrottleAuthentication(HttpContext context, string username)
    {
        context.Items[_usernameItemKey] = username;
        var usernameRemaining = GetRemainingLockout(username);
        var clientRemaining = GetRemainingLockout(context);
        var remaining = usernameRemaining is null
            ? clientRemaining
            : clientRemaining is null || usernameRemaining > clientRemaining
                ? usernameRemaining
                : clientRemaining;

        if (remaining is null)
            return null;

        var retryAfter = (int)Math.Ceiling(remaining.Value.TotalSeconds);
        context.Response.Headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);
        _logger.LogWarning("Authentication attempts from '{ClientAddress}' for user '{Username}' are throttled. Retry after {RetryAfter}.",
            context.Connection.RemoteIpAddress, username, remaining.Value);
        return new StatusCodeResult(StatusCodes.Status429TooManyRequests);
    }

    /// <summary>
    /// Gets the remaining lockout for the username, if any. Unlike <see cref="GetRemainingLockout(IUser)"/>,
    /// the username need not belong to an existing user.
    /// </summary>
    /// <param name="username">The username to check.</param>
    /// <returns>The remaining lockout duration; otherwise <see langword="null"/> if not locked out.</returns>
    internal TimeSpan? GetRemainingLockout(string? username)
        => GetRemainingLockoutForKey(UserKey(username));

    /// <summary>
    /// Registers a failed authentication attempt for the username, which need not belong to an existing user.
    /// </summary>
    /// <param name="username">The username to register the failure for.</param>
    internal void RegisterFailure(string? username)
        => RegisterFailureForKey(UserKey(username));

    /// <summary>
    /// Raises <see cref="AuthenticationFailed"/> for a failure just charged, logging any handler that throws.
    /// </summary>
    /// <param name="address">The client's address, if the failure was charged to a client.</param>
    /// <param name="username">The username the attempt was for, if known.</param>
    /// <param name="user">The user, if the failure was charged to a user.</param>
    /// <param name="path">The request path, if there was a request.</param>
    /// <param name="outcome">What the failure did to the lockout.</param>
    private void OnAuthenticationFailed(IPAddress? address, string? username, IUser? user, string? path, FailureOutcome outcome)
    {
        if (AuthenticationFailed is not { } handlers)
            return;

        var eventArgs = new AuthenticationFailedEventArgs
        {
            RemoteAddress = address,
            Username = username,
            User = user,
            Path = path,
            StartedLockout = outcome.StartedLockout,
            LockedOutUntil = outcome.LockedOutUntil,
            OccurredAt = outcome.OccurredAt,
        };
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<AuthenticationFailedEventArgs>>())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A handler of the authentication failure event failed.");
            }
        }
    }

    /// <summary>
    /// Clears all tracked failures for the username.
    /// </summary>
    /// <param name="username">The username to reset.</param>
    internal void Reset(string? username)
        => ResetForKey(UserKey(username));

    private TimeSpan? GetRemainingLockoutForKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_entries.TryGetValue(key, out var entry))
            return null;

        var policy = _policy;
        lock (entry)
        {
            var remaining = policy.BlockedUntil(entry) - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>
    /// Charges a failed attempt to the key, starting or extending its lockout if needed.
    /// </summary>
    /// <param name="key">The client or user key, or <see langword="null"/> for nothing to charge.</param>
    /// <returns>What the failure did to the lockout, or <see langword="null"/> when nothing was charged.</returns>
    private FailureOutcome? RegisterFailureForKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var policy = _policy;
        var now = DateTime.UtcNow;
        var entry = _entries.GetOrAdd(key, static _ => new());
        var startedLockout = false;
        DateTime blockedUntil;
        lock (entry)
        {
            if (policy.BlockedUntil(entry) > now)
            {
                // Attempts made while locked out escalate the lockout applied from now on.
                entry.FailedAttempts++;
                entry.LastFailure = now;
                entry.BlockedUntil = now + policy.LockoutDuration(entry.FailedAttempts);
            }
            else
            {
                if (entry.FailedAttempts > 0 && now - entry.LastFailure > policy.AttemptWindow)
                    entry.FailedAttempts = 0;

                entry.FailedAttempts++;
                entry.LastFailure = now;
                if (entry.FailedAttempts >= policy.MaxFailedAttempts)
                {
                    entry.BlockedUntil = now + policy.LockoutDuration(entry.FailedAttempts);
                    startedLockout = true;
                }
            }

            blockedUntil = policy.BlockedUntil(entry);
        }

        if (_entries.Count > _sweepThreshold)
            Sweep(policy, now);

        return new(startedLockout, blockedUntil > now ? blockedUntil : null, now);
    }

    private void ResetForKey(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _entries.TryRemove(key, out _);
    }

    private static string ClientKey(HttpContext context)
        => ClientKey(context.Connection.RemoteIpAddress);

    private static string ClientKey(HubCallerContext context)
        => ClientKey(ClientAddress(context));

    private static IPAddress? ClientAddress(HubCallerContext context)
        => context.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress ?? context.GetHttpContext()?.Connection.RemoteIpAddress;

    // Both kinds of keys are prefixed, so a username can never collide with a client key.
    private static string? UserKey(string? username)
        => string.IsNullOrWhiteSpace(username) ? null : $"user:{username}";

    private static string ClientKey(IPAddress? address)
        => $"ip:{address?.ToString() ?? "unknown"}";

    private void Sweep(Policy policy, DateTime now)
    {
        var maxAge = policy.AttemptWindow + policy.MaxLockout;
        foreach (var (key, entry) in _entries)
        {
            lock (entry)
            {
                var blockedUntil = policy.BlockedUntil(entry);
                var lastActivity = blockedUntil > entry.LastFailure ? blockedUntil : entry.LastFailure;
                if (now - lastActivity > maxAge)
                    _entries.TryRemove(key, out _);
            }
        }
    }

    private sealed record Policy(int MaxFailedAttempts, TimeSpan AttemptWindow, TimeSpan InitialLockout, TimeSpan MaxLockout)
    {
        // The ranges and the initial-not-above-max rule are enforced by the settings validation, so
        // the values are taken as they are.
        public static Policy From(AuthenticationThrottleSettings settings)
            => new(
                settings.MaxFailedAttempts,
                TimeSpan.FromMinutes(settings.AttemptWindowMinutes),
                TimeSpan.FromMinutes(settings.InitialLockoutMinutes),
                TimeSpan.FromMinutes(settings.MaxLockoutMinutes)
            );

        public TimeSpan LockoutDuration(int failedAttempts)
        {
            // Past 20 doublings even the smallest initial lockout is far above the largest max
            // lockout, and stopping there keeps the multiplication from overflowing.
            var exponent = Math.Max(failedAttempts - MaxFailedAttempts, 0);
            if (exponent > 20)
                return MaxLockout;

            var duration = InitialLockout * (1 << exponent);
            return duration > MaxLockout ? MaxLockout : duration;
        }

        // Lockouts are stored as they were applied, and capped by the current max lockout when read,
        // so lowering the max lockout also shortens existing lockouts.
        public DateTime BlockedUntil(Entry entry)
        {
            var cap = entry.LastFailure + MaxLockout;
            return entry.BlockedUntil < cap ? entry.BlockedUntil : cap;
        }
    }

    /// <summary>
    /// What one failed attempt did to the lockout of the key it was charged to.
    /// </summary>
    /// <param name="StartedLockout">Whether it started a lockout.</param>
    /// <param name="LockedOutUntil">When the lockout it left running ends, if one is running.</param>
    /// <param name="OccurredAt">When the attempt was charged.</param>
    private readonly record struct FailureOutcome(bool StartedLockout, DateTime? LockedOutUntil, DateTime OccurredAt);

    private sealed class Entry
    {
        public int FailedAttempts;

        public DateTime LastFailure;

        public DateTime BlockedUntil;
    }
}
