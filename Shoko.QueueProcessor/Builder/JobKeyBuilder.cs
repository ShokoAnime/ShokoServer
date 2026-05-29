using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Builds a stable, human-readable string key that uniquely identifies a job instance.
/// The key encodes the job type and the values of its <see cref="JobKeyMemberAttribute"/>-marked
/// properties (or all primitive properties when no attributes are present).
/// </summary>
public class JobKeyBuilder<T> where T : class, IQueueJob
{
    private static readonly HashSet<Type> _allowedTypes =
    [
        typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(char), typeof(double), typeof(float), typeof(decimal), typeof(Guid)
    ];

    // Cache property lookups per type to avoid repeated reflection
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    private JobKeyBuilder() { }

    /// <summary>Creates a new builder.</summary>
    public static JobKeyBuilder<T> Create() => new();

    /// <summary>Sets a data value that participates in key generation.</summary>
    public JobKeyBuilder<T> WithData(string propertyName, object? value)
    {
        _data[propertyName] = value;
        return this;
    }

    /// <summary>Populates data from a job configurator delegate.</summary>
    public JobKeyBuilder<T> UsingJobData(Action<T> configure)
    {
        var map = JobDataSerializer.DiffFromDefault(configure);
        foreach (var kv in map)
            _data[kv.Key] = kv.Value;
        return this;
    }

    /// <summary>Builds the unique job key string.</summary>
    public string Build()
    {
        var type = typeof(T);
        var allProperties = GetProperties(type);

        // Prefer explicitly annotated members; fall back to all eligible primitive properties
        var annotated = allProperties
            .Select(p => (Prop: p, Attr: p.GetCustomAttribute<JobKeyMemberAttribute>()))
            .Where(t => t.Attr != null)
            .ToArray();

        (PropertyInfo Prop, JobKeyMemberAttribute? Attr)[] members;
        if (annotated.Length > 0)
        {
            var maxIndex = annotated.Max(t => t.Attr!.Index);
            if (maxIndex < 0) maxIndex = annotated.Length;
            members = annotated
                .Select((t, i) => (t, i))
                .OrderBy(x => x.t.Attr!.Index >= 0 ? x.t.Attr.Index : x.i + maxIndex + 1)
                .Select(x => x.t)
                .ToArray();
        }
        else
        {
            members = allProperties
                .Where(p => IsEligible(p.PropertyType))
                .Select(p => (Prop: p, Attr: (JobKeyMemberAttribute?)null))
                .ToArray();
        }

        var segments = new List<string>();
        // Class-level attribute can override the type name prefix
        var classAttr = type.GetCustomAttribute<JobKeyMemberAttribute>();
        segments.Add(classAttr?.Id ?? type.Name);

        foreach (var (prop, attr) in members)
        {
            var segmentId = attr?.Id ?? prop.Name;
            if (!_data.TryGetValue(prop.Name, out var value)) continue;
            segments.Add(segmentId + ":" + JsonConvert.SerializeObject(value));
        }

        // Optionally prefix with group name
        var group = type.GetCustomAttribute<JobKeyGroupAttribute>()?.GroupName;
        var key = string.Join("_", segments);
        return group != null ? $"{group}/{key}" : key;
    }

    private static PropertyInfo[] GetProperties(Type type) =>
        _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray());

    private static bool IsEligible(Type t)
    {
        if (_allowedTypes.Contains(t)) return true;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return _allowedTypes.Contains(t.GetGenericArguments()[0]);
        return false;
    }
}
