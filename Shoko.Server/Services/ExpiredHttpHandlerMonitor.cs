using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;

#nullable enable
namespace Shoko.Server.Services;

/// <summary>
/// Names the HTTP clients whose expired handlers the client factory cannot
/// dispose, because something still holds a client created from them, most
/// often a typed client kept by a singleton.
/// </summary>
/// <remarks>
/// No public API lists the expired handlers, so this reads the factory's
/// internal queue through reflection. When that queue is not found, or reading
/// it fails, it logs once at Debug and stops.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="httpClientFactory">The client factory to watch.</param>
/// <param name="pluginManager">The plugins, to name the one a typed client belongs to.</param>
internal sealed class ExpiredHttpHandlerMonitor(
    ILogger<ExpiredHttpHandlerMonitor> logger,
    IHttpClientFactory httpClientFactory,
    IPluginManager pluginManager
) : BackgroundService
{
    #region Constants

    /// <summary>
    /// How often the expired handlers are checked.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long an expired handler must stay alive before it is reported.
    /// </summary>
    private static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long before an unchanged warning is logged again.
    /// </summary>
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromHours(6);

    #endregion

    #region Fields

    /// <summary>
    /// When each expired handler entry was first seen, keyed weakly by the entry.
    /// </summary>
    private readonly ConditionalWeakTable<object, Sighting> _sightings = new();

    /// <summary>
    /// The last warning logged for each client name.
    /// </summary>
    private readonly Dictionary<string, Warning> _warnings = new(StringComparer.Ordinal);

    /// <summary>
    /// The plugins found for each client name, or <c>null</c> when none was.
    /// </summary>
    private readonly Dictionary<string, string?> _owners = new(StringComparer.Ordinal);

    #endregion

    #region Checking

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = FindQueue();
        if (queue is null)
            return;

        using var timer = new PeriodicTimer(CheckInterval);
        while (await WaitForNextTick(timer, stoppingToken).ConfigureAwait(false))
        {
            if (!Check(queue))
                return;
        }
    }

    /// <summary>
    /// Finds the factory's queue of expired handlers and the members read off
    /// each entry.
    /// </summary>
    /// <returns>The queue, or <c>null</c> when its shape is not the one expected.</returns>
    private ExpiredHandlerQueue? FindQueue()
    {
        try
        {
            var factoryType = httpClientFactory.GetType();
            var field = factoryType.GetField("_expiredHandlers", BindingFlags.Instance | BindingFlags.NonPublic);
            var entryType = field?.FieldType.GetInterfaces()
                .Append(field.FieldType)
                .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
            var name = entryType?.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var canDispose = entryType?.GetProperty("CanDispose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(httpClientFactory) is IEnumerable entries
                && name?.PropertyType == typeof(string)
                && canDispose?.PropertyType == typeof(bool))
                return new(entries, name, canDispose);

            logger.LogDebug(
                "The HTTP client factory {FactoryType} does not keep its expired handlers where expected; not watching them.",
                factoryType.FullName
            );
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not find the expired handlers of the HTTP client factory; not watching them.");
            return null;
        }
    }

    /// <summary>
    /// Finds the expired handlers still alive past the minimum age and through
    /// a full garbage collection, and reports them by client name.
    /// </summary>
    /// <param name="queue">The factory's queue of expired handlers.</param>
    /// <returns><c>false</c> when reading the queue failed and checking should stop.</returns>
    private bool Check(ExpiredHandlerQueue queue)
    {
        try
        {
            var now = DateTime.UtcNow;
            var fullCollections = GC.CollectionCount(GC.MaxGeneration);
            var stuck = new Dictionary<string, Stuck>(StringComparer.Ordinal);
            // The queue enumerates a snapshot, so the factory's cleanup is never blocked.
            foreach (var entry in queue.Entries)
            {
                if (entry is null || queue.CanDispose.GetValue(entry) is not false)
                    continue;

                var sighting = _sightings.GetValue(entry, _ => new(now, fullCollections));
                if (now - sighting.FirstSeen < MinimumAge || fullCollections == sighting.FullCollections)
                    continue;

                var name = queue.Name.GetValue(entry) as string ?? string.Empty;
                stuck[name] = stuck.TryGetValue(name, out var previous)
                    ? new(previous.Count + 1, previous.Oldest.FirstSeen <= sighting.FirstSeen ? previous.Oldest : sighting)
                    : new(1, sighting);
            }

            Report(stuck, now, fullCollections);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read the expired handlers of the HTTP client factory; no longer watching them.");
            return false;
        }
    }

    /// <summary>
    /// Warns about each client whose stuck handlers are new or changed, or were
    /// last reported long ago, and notes the clients whose handlers were released.
    /// </summary>
    /// <param name="stuck">The stuck handlers by client name.</param>
    /// <param name="now">When the check started.</param>
    /// <param name="fullCollections">The full garbage collections run so far.</param>
    private void Report(Dictionary<string, Stuck> stuck, DateTime now, int fullCollections)
    {
        foreach (var (name, (count, oldest)) in stuck)
        {
            if (_warnings.TryGetValue(name, out var last) && last.Count == count && now - last.LoggedAt < RepeatInterval)
                continue;

            _warnings[name] = new(count, now);
            var displayName = name.Length > 0 ? name : "(default)";
            var minutes = (int)(now - oldest.FirstSeen).TotalMinutes;
            var collections = fullCollections - oldest.FullCollections;
            if (FindOwner(name) is { } owner)
                logger.LogWarning(
                    "HTTP client {ClientName} (a type in plugin {PluginName}) has {Count} expired handler(s) the factory cannot dispose, " +
                    "the oldest expired at least {Minutes} minutes and {Collections} full GCs ago. " +
                    "Something still holds an HttpClient created from it, such as a singleton keeping a typed client.",
                    displayName, owner, count, minutes, collections
                );
            else
                logger.LogWarning(
                    "HTTP client {ClientName} has {Count} expired handler(s) the factory cannot dispose, " +
                    "the oldest expired at least {Minutes} minutes and {Collections} full GCs ago. " +
                    "Something still holds an HttpClient created from it, such as a singleton keeping a typed client.",
                    displayName, count, minutes, collections
                );
        }

        foreach (var name in _warnings.Keys.Where(name => !stuck.ContainsKey(name)).ToList())
        {
            _warnings.Remove(name);
            logger.LogInformation("HTTP client {ClientName} no longer has expired handlers the factory cannot dispose.", name.Length > 0 ? name : "(default)");
        }
    }

    /// <summary>
    /// Finds the plugins with a type named like the client, as the factory
    /// names a typed client after its type.
    /// </summary>
    /// <param name="name">The client name.</param>
    /// <returns>The plugin names, or <c>null</c> when no plugin has such a type.</returns>
    private string? FindOwner(string name)
    {
        if (_owners.TryGetValue(name, out var owner))
            return owner;

        try
        {
            var typeName = name.Split('<')[0];
            var owners = pluginManager.GetPluginInfos()
                .Where(plugin => plugin.IsActive && plugin.PluginType is not null)
                .Where(plugin => GetLoadableTypes(plugin.PluginType!.Assembly).Any(type => type.Name.Split('`')[0] == typeName))
                .Select(plugin => plugin.Name)
                .Distinct()
                .ToList();
            owner = owners.Count > 0 ? string.Join(", ", owners) : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not look up the plugin of HTTP client {ClientName}.", name);
            owner = null;
        }

        _owners[name] = owner;
        return owner;
    }

    /// <summary>
    /// Gets the types of an assembly that could be loaded.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The loaded types.</returns>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>
    /// Waits for the next tick.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="stoppingToken">Cancelled when the host stops.</param>
    /// <returns><c>false</c> once the host is stopping.</returns>
    private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion

    #region Types

    /// <summary>
    /// The factory's queue of expired handlers, and the members read off each entry.
    /// </summary>
    /// <param name="Entries">The queue.</param>
    /// <param name="Name">The client name of an entry.</param>
    /// <param name="CanDispose">Whether an entry's outer handler was collected.</param>
    private sealed record ExpiredHandlerQueue(IEnumerable Entries, PropertyInfo Name, PropertyInfo CanDispose);

    /// <summary>
    /// When an expired handler was first seen.
    /// </summary>
    /// <param name="FirstSeen">The time it was first seen.</param>
    /// <param name="FullCollections">The full garbage collections run by then.</param>
    private sealed record Sighting(DateTime FirstSeen, int FullCollections);

    /// <summary>
    /// The stuck handlers of one client.
    /// </summary>
    /// <param name="Count">How many there are.</param>
    /// <param name="Oldest">The sighting of the oldest one.</param>
    private sealed record Stuck(int Count, Sighting Oldest);

    /// <summary>
    /// The last warning logged for a client.
    /// </summary>
    /// <param name="Count">How many stuck handlers it reported.</param>
    /// <param name="LoggedAt">When it was logged.</param>
    private sealed record Warning(int Count, DateTime LoggedAt);

    #endregion
}
