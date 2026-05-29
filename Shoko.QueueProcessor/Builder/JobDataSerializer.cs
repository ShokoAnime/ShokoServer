using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Serialises and deserialises job property data as JSON.
/// Replaces Quartz's <c>JobDataMapBuilder</c> and property-injection via <c>JobDataMap</c>.
/// </summary>
public static class JobDataSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propCache = new();

    private static readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include
    };

    /// <summary>
    /// Serialises the values set by <paramref name="configure"/> relative to a freshly-constructed
    /// default instance. Only changed (non-default) properties are included in the returned map.
    /// This is used by <see cref="JobKeyBuilder{T}.UsingJobData"/> for key generation.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="RuntimeHelpers.GetUninitializedObject"/> to bypass constructors entirely,
    /// so jobs are not required to have a parameterless constructor. Key-building only reads
    /// and writes public settable properties (job data), which never depend on injected services.
    /// </remarks>
    public static Dictionary<string, object?> DiffFromDefault<T>(Action<T> configure)
        where T : class, IQueueJob
    {
        var baseline = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        var modified = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        configure(modified);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in GetProperties(typeof(T)))
        {
            var original = prop.GetValue(baseline);
            var changed = prop.GetValue(modified);
            if (!Equals(original, changed))
                result[prop.Name] = changed;
        }
        return result;
    }

    /// <summary>
    /// Serialises the public settable (readable + writable, not <see cref="JsonIgnoreAttribute"/>)
    /// properties of <paramref name="job"/> to a JSON string.
    /// This is the canonical storage format in <c>QueuedJob.JobDataJson</c>.
    /// Read-only computed properties are excluded to avoid wasting DB space.
    /// </summary>
    public static string Serialize(IQueueJob job)
    {
        var props = GetProperties(job.GetType());
        var dict = new Dictionary<string, object?>(props.Length);
        foreach (var prop in props)
            dict[prop.Name] = prop.GetValue(job);
        return JsonConvert.SerializeObject(dict, _settings);
    }

    /// <summary>
    /// Deserialises <paramref name="json"/> and applies it to the properties of
    /// <paramref name="job"/> in-place. Called by workers before <see cref="IQueueJob.Process"/>.
    /// </summary>
    public static void Apply(IQueueJob job, string? json)
    {
        if (string.IsNullOrEmpty(json)) return;
        JsonConvert.PopulateObject(json, job, _settings);
    }

    private static PropertyInfo[] GetProperties(Type type) =>
        _propCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite &&
                        p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .ToArray());
}
