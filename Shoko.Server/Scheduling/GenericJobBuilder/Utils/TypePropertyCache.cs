﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

#nullable enable
namespace Shoko.Server.Scheduling.GenericJobBuilder.Utils;

/// <summary>
/// A static PropertyInfo cache for generic classes, which have static members per generic parameter
/// </summary>
public static class TypePropertyCache
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache = new();

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> _propertyCache = new();

    public static PropertyInfo[] Get(Type type)
    {
        return _propertiesCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(a =>
        {
            if (!a.CanRead) return false;
            var setMethod = a.SetMethod;
            return setMethod != null && setMethod.IsPublic;
        }).ToArray());
    }

    public static bool ContainsKey(Type type)
    {
        return _propertiesCache.ContainsKey(type);
    }

    public static PropertyInfo[] GetOrAdd(Type type, Func<Type, PropertyInfo[]> getter)
    {
        return _propertiesCache.GetOrAdd(type, getter);
    }

    public static PropertyInfo? Get(Type type, string name)
    {
        return _propertyCache.GetOrAdd((type, name),
            t => t.Type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(a => a.Name == name));
    }

    public static bool ContainsKey(Type type, string name)
    {
        return _propertyCache.ContainsKey((type, name));
    }

    public static PropertyInfo? GetOrAdd(Type type, string name, Func<(Type Type, string Name), PropertyInfo?> getter)
    {
        return _propertyCache.GetOrAdd((type, name), getter);
    }

    public static T? Get<T>(string name, object arg) where T : class
    {
        return _propertyCache.GetOrAdd((arg.GetType(), name),
            _ => GetProperty<T>(arg, name))?.GetValue(arg) as T;
    }

    private static PropertyInfo? GetProperty<T>(object obj, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));

        var property = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(a => a.Name.Split('.').LastOrDefault() == propertyName);

        if (property == null) return null;

        if (!typeof(T).IsAssignableFrom(property.PropertyType))
            throw new InvalidOperationException($"Property type and requested type are not compatible: {typeof(T).Name}, {property.PropertyType.Name}");

        return property;
    }
}
