using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Shoko.Server.Scheduling.GenericJobBuilder.Utils;

/// <summary>
/// A static FieldInfo cache for generic classes, which have static members per generic parameter
/// </summary>
public static class TypeFieldCache
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo> fieldCache = new();

    public static FieldInfo Get(Type type, string name)
    {
        return fieldCache.GetOrAdd((type, name),
            t => t.Type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
    }
}
