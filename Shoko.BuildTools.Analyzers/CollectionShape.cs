using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// How the configuration UI schema generator will describe a type.
/// </summary>
internal enum CollectionKind
{
    /// <summary>
    /// Not a collection; rendered as a scalar or as a section.
    /// </summary>
    None = 0,

    /// <summary>
    /// Rendered as a JSON array, and keyed with a <c>+List</c> suffix.
    /// </summary>
    List = 1,

    /// <summary>
    /// Rendered as a JSON object with <c>additionalProperties</c>, and keyed with a <c>+Dict</c> suffix.
    /// </summary>
    Dictionary = 2,
}

/// <summary>
/// The collection shape of a type, as the configuration UI schema generator sees it.
/// </summary>
/// <param name="kind">The kind of collection, if any.</param>
/// <param name="element">
/// The element type for a list, or the value type for a dictionary. Only meaningful when
/// <paramref name="kind"/> is not <see cref="CollectionKind.None"/>.
/// </param>
/// <param name="key">The key type, for a dictionary.</param>
internal readonly struct CollectionShape(CollectionKind kind, ITypeSymbol? element, ITypeSymbol? key)
{
    /// <summary>
    /// A type that is not a collection.
    /// </summary>
    public static readonly CollectionShape None = new(CollectionKind.None, null, null);

    /// <summary>
    /// The kind of collection, if any.
    /// </summary>
    public CollectionKind Kind { get; } = kind;

    /// <summary>
    /// The element type for a list, or the value type for a dictionary.
    /// </summary>
    public ITypeSymbol? Element { get; } = element;

    /// <summary>
    /// The key type, for a dictionary.
    /// </summary>
    public ITypeSymbol? Key { get; } = key;

    /// <summary>
    /// The word to use for this collection kind in a diagnostic message.
    /// </summary>
    public string Noun => Kind switch
    {
        CollectionKind.List => "list",
        CollectionKind.Dictionary => "dictionary",
        _ => "value",
    };

    /// <summary>
    /// Classifies a type the same way the configuration UI schema generator does: dictionaries
    /// first, then anything else enumerable, with the types the generator maps to a JSON scalar
    /// excluded.
    /// </summary>
    /// <param name="type">The type to classify.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    /// <returns>The collection shape of <paramref name="type"/>.</returns>
    public static CollectionShape Classify(ITypeSymbol? type, KnownSymbols known)
    {
        if (Unwrap(type) is not { } unwrapped)
            return None;

        // string is IEnumerable<char>, and byte[] is emitted as a base64 string, so neither is an
        // array in the generated schema.
        if (unwrapped.SpecialType is SpecialType.System_String)
            return None;
        if (unwrapped is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return None;

        // Newtonsoft and System.Text.Json DOM types implement collection interfaces but are not
        // configuration shapes at all. Never claim to know what the generator does with them.
        foreach (var nonCollection in known.NonCollectionBaseTypes)
        {
            for (var current = unwrapped; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, nonCollection))
                    return None;
            }
        }

        if (unwrapped is IArrayTypeSymbol array)
            return new CollectionShape(CollectionKind.List, array.ElementType, null);

        if (FindConstructed(unwrapped, known.GenericDictionary) is { } dictionary)
            return new CollectionShape(CollectionKind.Dictionary, dictionary.TypeArguments[1], dictionary.TypeArguments[0]);
        if (FindConstructed(unwrapped, known.GenericReadOnlyDictionary) is { } readOnlyDictionary)
            return new CollectionShape(CollectionKind.Dictionary, readOnlyDictionary.TypeArguments[1], readOnlyDictionary.TypeArguments[0]);

        if (FindEnumerable(unwrapped) is { } enumerable)
            return new CollectionShape(CollectionKind.List, enumerable.TypeArguments[0], null);

        // A non-generic dictionary still becomes a JSON object with additionalProperties, but the
        // generator cannot read a key or value type off it. Reported by SHOKO0005 on its own.
        if (known.NonGenericDictionary is not null && Implements(unwrapped, known.NonGenericDictionary))
            return new CollectionShape(CollectionKind.Dictionary, null, null);

        return None;
    }

    /// <summary>
    /// Strips the <see cref="System.Nullable{T}"/> wrapper, so a nullable value type is classified
    /// as the type it wraps.
    /// </summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The unwrapped type, or <see langword="null"/> when there is nothing to classify.</returns>
    public static ITypeSymbol? Unwrap(ITypeSymbol? type)
        => type switch
        {
            null => null,
            IErrorTypeSymbol => null,
            ITypeParameterSymbol => null,
            IDynamicTypeSymbol => null,
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable => nullable.TypeArguments[0],
            _ => type,
        };

    /// <summary>
    /// Whether the type is, or implements, the given non-generic interface.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the interface is implemented.</returns>
    public static bool Implements(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
            return true;

        foreach (var candidate in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, interfaceType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the type is a generic dictionary, which is what
    /// <c>ShokoJsonSchemaGenerator.GetTKeyAndTValue</c> requires of anything it renders as a record.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    /// <returns><see langword="true"/> when the type is a generic dictionary.</returns>
    public static bool IsGenericDictionary(ITypeSymbol type, KnownSymbols known)
        => FindConstructed(type, known.GenericDictionary) is not null || FindConstructed(type, known.GenericReadOnlyDictionary) is not null;

    private static INamedTypeSymbol? FindConstructed(ITypeSymbol type, INamedTypeSymbol? definition)
    {
        if (definition is null)
            return null;

        if (type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, definition))
            return named;

        foreach (var candidate in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, definition))
                return candidate;
        }

        return null;
    }

    private static INamedTypeSymbol? FindEnumerable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } named)
            return named;

        foreach (var candidate in type.AllInterfaces)
        {
            if (candidate.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T)
                return candidate;
        }

        return null;
    }
}
