using System;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.API.Authentication;

/// <summary>
/// Tracks failed login attempts per username and per client, and blocks further attempts with an
/// escalating lockout once the allowed number of attempts within the attempt window is exceeded.
/// </summary>
public class LoginThrottler
{
    /// <summary>
    /// The number of failed attempts within <see cref="AttemptWindow"/> that are allowed before the
    /// username or client is locked out.
    /// </summary>
    public const int MaxFailedAttempts = 10;

    /// <summary>
    /// The sliding window in which failed attempts are counted.
    /// </summary>
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The lockout duration applied on the first excess attempt, doubling for every attempt after that.
    /// </summary>
    public static readonly TimeSpan InitialLockout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The maximum lockout duration, regardless of how many failed attempts have been made.
    /// </summary>
    public static readonly TimeSpan MaxLockout = TimeSpan.FromDays(1);

    private const int SweepThreshold = 4096;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Gets the remaining lockout for the key, if any.
    /// </summary>
    /// <param name="key">The username or client key to check.</param>
    /// <returns>The remaining lockout duration; otherwise <see langword="null"/> if not locked out.</returns>
    public TimeSpan? GetRemainingLockout(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_entries.TryGetValue(key, out var entry))
            return null;

        lock (entry)
        {
            var remaining = entry.BlockedUntil - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>
    /// Registers a failed login attempt for the key, starting or extending the lockout if needed.
    /// </summary>
    /// <param name="key">The username or client key to register the failure for.</param>
    public void RegisterFailure(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var now = DateTime.UtcNow;
        var entry = _entries.GetOrAdd(key, static _ => new());
        lock (entry)
        {
            if (entry.BlockedUntil > now)
            {
                // Attempts made while locked out escalate the lockout applied from now on.
                entry.FailedAttempts++;
                entry.LastFailure = now;
                entry.BlockedUntil = now + LockoutDuration(entry.FailedAttempts);
                return;
            }

            if (entry.FailedAttempts > 0 && now - entry.LastFailure > AttemptWindow)
                entry.FailedAttempts = 0;

            entry.FailedAttempts++;
            entry.LastFailure = now;
            if (entry.FailedAttempts >= MaxFailedAttempts)
                entry.BlockedUntil = now + LockoutDuration(entry.FailedAttempts);
        }

        if (_entries.Count > SweepThreshold)
            Sweep(now);
    }

    /// <summary>
    /// Clears all tracked failures for the key, as is appropriate after a successful login.
    /// </summary>
    /// <param name="key">The username or client key to reset.</param>
    public void Reset(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _entries.TryRemove(key, out _);
    }

    private static TimeSpan LockoutDuration(int failedAttempts)
    {
        var exponent = Math.Max(failedAttempts - MaxFailedAttempts, 0);
        if (exponent > 10)
            return MaxLockout;

        var duration = InitialLockout * (1 << exponent);
        return duration > MaxLockout ? MaxLockout : duration;
    }

    private void Sweep(DateTime now)
    {
        var maxAge = AttemptWindow + MaxLockout;
        foreach (var (key, entry) in _entries)
        {
            lock (entry)
            {
                var lastActivity = entry.BlockedUntil > entry.LastFailure ? entry.BlockedUntil : entry.LastFailure;
                if (now - lastActivity > maxAge)
                    _entries.TryRemove(key, out _);
            }
        }
    }

    private class Entry
    {
        public int FailedAttempts;

        public DateTime LastFailure;

        public DateTime BlockedUntil;
    }
}

/// <summary>
/// Helpers to apply <see cref="LoginThrottler"/> to login endpoints.
/// </summary>
public static class LoginThrottlerExtensions
{
    /// <summary>
    /// The throttling key for the requesting client.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    public static string ClientKey(this HttpContext context)
        => $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>
    /// Checks whether the login attempt for the username from the requesting client should be
    /// throttled, setting the <c>Retry-After</c> response header if it should.
    /// </summary>
    /// <param name="throttler">The throttler.</param>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="username">The username the login attempt is for.</param>
    /// <param name="logger">Logger used to report the throttled attempt.</param>
    /// <returns>A <c>429 Too Many Requests</c> result if the attempt should be blocked; otherwise <see langword="null"/>.</returns>
    public static StatusCodeResult? ThrottleLogin(this LoginThrottler throttler, HttpContext context, string username, ILogger logger)
    {
        var usernameRemaining = throttler.GetRemainingLockout(username);
        var clientRemaining = throttler.GetRemainingLockout(context.ClientKey());
        var remaining = usernameRemaining is null
            ? clientRemaining
            : clientRemaining is null || usernameRemaining > clientRemaining
                ? usernameRemaining
                : clientRemaining;

        if (remaining is null)
            return null;

        var retryAfter = (int)Math.Ceiling(remaining.Value.TotalSeconds);
        context.Response.Headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);
        logger.LogWarning("Login attempts from '{ClientAddress}' for user '{Username}' are throttled. Retry after {RetryAfter}.",
            context.Connection.RemoteIpAddress, username, remaining.Value);
        return new StatusCodeResult(StatusCodes.Status429TooManyRequests);
    }
}
