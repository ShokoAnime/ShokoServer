using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Shoko.Server.Scheduling.GenericJobBuilder.Utils;

/// <summary>
/// A static FieldInfo cache for generic classes, which have static members per generic parameter
/// </summary>
public static class TypeFieldCache
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> fieldsCache = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo?> fieldCache = new();

    public static FieldInfo[] Get(Type type)
    {
        return fieldsCache.GetOrAdd(type, t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    public static bool ContainsKey(Type type)
    {
        return fieldsCache.ContainsKey(type);
    }

    public static FieldInfo[] GetOrAdd(Type type, Func<Type, FieldInfo[]> getter)
    {
        return fieldsCache.GetOrAdd(type, getter);
    }

    public static FieldInfo? Get(Type type, string name)
    {
        return fieldCache.GetOrAdd((type, name),
            t => t.Type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }

    public static bool ContainsKey(Type type, string name)
    {
        return fieldCache.ContainsKey((type, name));
    }

    public static FieldInfo? GetOrAdd(Type type, string name, Func<(Type Type, string Name), FieldInfo?> getter)
    {
        return fieldCache.GetOrAdd((type, name), getter);
    }

    public static T? Get<T>(string name, object arg) where T : class
    {
        return fieldCache.GetOrAdd((arg.GetType(), name),
                _ => GetField<T>(arg, name))?.GetValue(arg) as T;
    }

    private static FieldInfo GetField<T>(object obj, string fieldName)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var field = obj.GetType().GetField(fieldName, BindingFlags.Public |
                                                      BindingFlags.NonPublic |
                                                      BindingFlags.Instance);

        if (field == null) return null;

        if (!typeof(T).IsAssignableFrom(field.FieldType))
            throw new InvalidOperationException($"Field type and requested type are not compatible: {typeof(T).Name}, {field.FieldType.Name}");

        return field;
    }
}
