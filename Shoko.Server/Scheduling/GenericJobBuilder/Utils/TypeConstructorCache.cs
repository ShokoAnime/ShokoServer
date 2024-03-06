using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Shoko.Server.Scheduling.GenericJobBuilder.Utils;

/// <summary>
/// A static PropertyInfo cache for generic classes, which have static members per generic parameter
/// </summary>
public static class TypeConstructorCache
{
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> ConstructorCache = new();

    public static ConstructorInfo Get(Type type)
    {
        return ConstructorCache.GetOrAdd(type,
            t => t.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null,
                CallingConventions.Any, Type.EmptyTypes, Array.Empty<ParameterModifier>()));
    }

    public static bool ContainsKey(Type type)
    {
        return ConstructorCache.ContainsKey(type);
    }

    public static ConstructorInfo GetOrAdd(Type type, Func<Type, ConstructorInfo> getter)
    {
        return ConstructorCache.GetOrAdd(type, getter);
    }
}
