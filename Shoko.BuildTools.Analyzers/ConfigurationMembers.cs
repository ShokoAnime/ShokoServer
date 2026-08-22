using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// Shared member-level predicates, kept in one place so the reachability walk and the rules agree
/// on which members the configuration UI schema generator actually sees.
/// </summary>
internal static class ConfigurationMembers
{
    /// <summary>
    /// Whether the property ends up in the generated JSON schema at all.
    /// </summary>
    /// <remarks>
    /// Deliberately conservative. Non-public members, write-only members and members either JSON
    /// serializer would ignore are skipped, so an uncertain case never becomes a build error.
    /// </remarks>
    /// <param name="property">The property to test.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    /// <returns><see langword="true"/> when the property reaches the schema generator.</returns>
    public static bool ReachesSchemaGenerator(IPropertySymbol property, KnownSymbols known)
    {
        if (property.IsStatic || property.IsIndexer || property.IsImplicitlyDeclared)
            return false;
        if (property.DeclaredAccessibility is not Accessibility.Public)
            return false;
        if (property.GetMethod is null || property.ExplicitInterfaceImplementations.Length > 0)
            return false;
        if (known.NewtonsoftJsonIgnoreAttribute is not null && HasAttribute(property, known.NewtonsoftJsonIgnoreAttribute))
            return false;
        if (known.SystemTextJsonIgnoreAttribute is not null && HasAttribute(property, known.SystemTextJsonIgnoreAttribute))
            return false;
        if (known.JsonSchemaIgnoreAttribute is not null && HasAttribute(property, known.JsonSchemaIgnoreAttribute))
            return false;

        return true;
    }

    /// <summary>
    /// Whether the schema generator will render the type as a section container, which is the
    /// condition the complex list display types check through <c>listElementType</c>.
    /// </summary>
    /// <remarks>
    /// Mirrors the registration condition in <c>ShokoJsonSchemaGenerator</c>: the type's schema has
    /// at least one property, and its full name is under neither <c>System.</c> nor
    /// <c>Shoko.Abstractions.UI.Components.</c>.
    /// </remarks>
    /// <param name="type">The type to test.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    /// <returns><see langword="true"/> when the type renders as a section container.</returns>
    public static bool IsSectionContainer(ITypeSymbol? type, KnownSymbols known)
    {
        if (CollectionShape.Unwrap(type) is not INamedTypeSymbol named)
            return false;
        if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
            return false;
        if (named.SpecialType is not SpecialType.None)
            return false;

        var fullName = named.ToDisplayString();
        if (fullName.StartsWith("System.", StringComparison.Ordinal) || fullName.StartsWith("Shoko.Abstractions.UI.Components.", StringComparison.Ordinal))
            return false;

        // Inherited properties are flattened into the derived type's schema, so the whole chain counts.
        foreach (var current in SelfAndBases(named))
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol property && ReachesSchemaGenerator(property, known))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the type declares a property the generator will pick up as the primary key.
    /// </summary>
    /// <remarks>
    /// Only properties declared by the type itself count. The generator files a property's UI
    /// metadata under <c>MemberInfo.ReflectedType</c>, which for an inherited property is the base
    /// type, so an inherited <c>[Key]</c> never reaches the derived type's primary key scan. This
    /// mirrors that rather than the intent.
    /// </remarks>
    /// <param name="type">The type to test.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    /// <returns><see langword="true"/> when the type declares a primary key.</returns>
    public static bool DeclaresPrimaryKey(ITypeSymbol? type, KnownSymbols known)
    {
        if (known.KeyAttribute is null || CollectionShape.Unwrap(type) is not INamedTypeSymbol named)
            return false;

        // The base chain counts: the schema flattens inheritance, and the
        // generator resolves an inherited key through the flattened property
        // set. Reporting one here would be an error on code that builds a
        // perfectly good schema.
        foreach (var current in SelfAndBases(named))
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol property && ReachesSchemaGenerator(property, known) && HasAttribute(property, known.KeyAttribute))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The type and every base type up to, but excluding, <see cref="object"/>.
    /// </summary>
    /// <param name="type">The type to walk from.</param>
    /// <returns>The type and its base types.</returns>
    public static IEnumerable<INamedTypeSymbol> SelfAndBases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
            yield return current;
    }

    /// <summary>
    /// Whether the symbol carries the given attribute.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="attributeType">The attribute type to look for.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        => FindAttribute(symbol, attributeType) is not null;

    /// <summary>
    /// Finds the given attribute on the symbol.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="attributeType">The attribute type to look for.</param>
    /// <returns>The attribute, or <see langword="null"/> when it is not present.</returns>
    public static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                return attribute;
        }

        return null;
    }
}
