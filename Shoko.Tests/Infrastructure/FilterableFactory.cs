using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Abstractions.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Builds fully populated filterable test doubles by reflection.
/// </summary>
/// <remarks>
/// The filterable interfaces carry well over a hundred members, and a hand-written initializer
/// would silently stop being complete the moment one is added — leaving anything that reads the new
/// member dereferencing null in tests that look like they cover it. Populating by reflection keeps
/// every member non-null as the interface grows.
/// </remarks>
public static class FilterableFactory
{
    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    /// <summary>
    /// Creates <typeparamref name="T"/> with every writable property set to a non-null value.
    /// </summary>
    public static T CreatePopulated<T>() where T : new()
    {
        var instance = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
                continue;

            if (SampleFor(property.PropertyType) is { } value)
                property.SetValue(instance, value);
        }

        return instance;
    }

    private static object? SampleFor(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return SampleFor(underlying);

        if (type == typeof(string)) return "sample";
        if (type == typeof(bool)) return true;
        if (type == typeof(DateTime)) return s_date;
        if (type == typeof(DateOnly)) return DateOnly.FromDateTime(s_date);
        if (type == typeof(TimeSpan)) return TimeSpan.FromMinutes(1);
        // A default PartialDateOnly has Year 0, which cannot be converted to a DateOnly, so give it
        // a real date rather than letting the struct default through.
        if (type == typeof(PartialDateOnly)) return new PartialDateOnly(s_date.Year, s_date.Month, s_date.Day);
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(1, type);

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(IReadOnlySet<>) || definition == typeof(ISet<>) || definition == typeof(HashSet<>))
                return BuildSet(arguments[0]);

            if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>))
                return BuildDictionary(arguments[0], arguments[1]);

            if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>) || definition == typeof(List<>) || definition == typeof(IEnumerable<>))
                return BuildList(arguments[0]);
        }

        // Value types (including tuples) always have a default; reference types need a constructor.
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return type.GetConstructor(Type.EmptyTypes) is null ? null : Activator.CreateInstance(type);
    }

    private static object BuildSet(Type elementType)
    {
        var set = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        if (SampleFor(elementType) is { } element)
            set.Add(element);

        return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType), set)!;
    }

    private static object BuildList(Type elementType)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        if (SampleFor(elementType) is { } element)
            list.Add(element);

        return list;
    }

    private static object BuildDictionary(Type keyType, Type valueType)
    {
        var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
        if (SampleFor(keyType) is { } key && SampleFor(valueType) is { } value)
            dictionary[key] = value;

        return dictionary;
    }
}
