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
    /// Creates <typeparamref name="T"/> with every writable property set to a non-null value that
    /// is distinct from its siblings.
    /// </summary>
    /// <remarks>
    /// Values are seeded from the property name rather than its type. Giving every <c>int</c> the
    /// same sample would make any test that reads one property indistinguishable from one that
    /// reads another of the same type — a selector could return the wrong field and still satisfy
    /// its assertion.
    /// </remarks>
    public static T CreatePopulated<T>() where T : new()
        => (T)Populate(new T(), string.Empty);

    private static object Populate(object instance, string prefix)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
                continue;

            if (SampleFor(property.PropertyType, prefix + property.Name) is { } value)
                property.SetValue(instance, value);
        }

        return instance;
    }

    /// <summary>A small stable seed derived from the property name, so runs are reproducible.</summary>
    private static int SeedFor(string name)
    {
        var seed = 0;
        foreach (var character in name)
            seed = ((seed * 31) + character) & 0x7FFFFF;

        return seed;
    }

    private static object? SampleFor(Type type, string name)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return SampleFor(underlying, name);

        var seed = SeedFor(name);

        if (type == typeof(string)) return $"sample-{name}";
        if (type == typeof(bool)) return true;
        if (type == typeof(DateTime)) return s_date.AddSeconds(seed % 100000);
        if (type == typeof(DateOnly)) return DateOnly.FromDateTime(s_date.AddDays(seed % 1000));
        if (type == typeof(TimeSpan)) return TimeSpan.FromSeconds(1 + (seed % 10000));
        // A default PartialDateOnly has Year 0, which cannot be converted to a DateOnly, so give it
        // a real date rather than letting the struct default through.
        if (type == typeof(PartialDateOnly)) return new PartialDateOnly(1990 + (seed % 30), 1 + (seed % 12), 1 + (seed % 28));
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.Length == 0 ? null : values.GetValue(seed % values.Length);
        }
        if (type == typeof(bool)) return true;
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(1 + (seed % 9973), type);

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(IReadOnlySet<>) || definition == typeof(ISet<>) || definition == typeof(HashSet<>))
                return BuildSet(arguments[0], name);

            if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>))
                return BuildDictionary(arguments[0], arguments[1], name);

            if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>) || definition == typeof(List<>) || definition == typeof(IEnumerable<>))
                return BuildList(arguments[0], name);
        }

        // Value types (including tuples) always have a default; reference types need a constructor.
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        if (type.GetConstructor(Type.EmptyTypes) is null)
            return null;

        // Populate one level down as well, so two properties of the same nested type do not come
        // back identical either.
        return Populate(Activator.CreateInstance(type)!, name + ".");
    }

    private static object BuildSet(Type elementType, string name)
    {
        var set = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        if (SampleFor(elementType, name) is { } element)
            set.Add(element);

        return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType), set)!;
    }

    private static object BuildList(Type elementType, string name)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        if (SampleFor(elementType, name) is { } element)
            list.Add(element);

        return list;
    }

    private static object BuildDictionary(Type keyType, Type valueType, string name)
    {
        var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
        if (SampleFor(keyType, name + ".key") is { } key && SampleFor(valueType, name + ".value") is { } value)
            dictionary[key] = value;

        return dictionary;
    }
}
