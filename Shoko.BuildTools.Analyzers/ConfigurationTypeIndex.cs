using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// The set of source types the configuration UI schema generator will walk into, computed once per
/// compilation.
/// </summary>
/// <remarks>
/// <para>
/// The generator starts at a type implementing <c>Shoko.Abstractions.Config.IConfiguration</c> or
/// <c>Shoko.Abstractions.Actions.IExecutableAction</c> — an action's invocation parameters are its
/// own settable, serialized properties, walked exactly as a configuration's are — and recurses
/// through the property graph, so a plain class used as a section of a configuration or as a
/// parameter of an action is analysed too. Types that only become reachable through a root in
/// another assembly are not analysed, because that assembly runs its own copy of this analyzer.
/// </para>
/// <para>
/// An action's metadata surface (<c>Name</c>, <c>Description</c>, <c>Category</c>,
/// <c>Permission</c>, <c>RequiresConfirmation</c>, <c>Scope</c>) is not filtered out here, because
/// every one of those is a string, a bool or an enum from a referenced assembly: none can trip any
/// of the rules, and none embeds a source type for the walk to descend into.
/// </para>
/// </remarks>
internal sealed class ConfigurationTypeIndex
{
    private readonly Lazy<ImmutableHashSet<INamedTypeSymbol>> _reachable;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationTypeIndex"/> class.
    /// </summary>
    /// <param name="compilation">The compilation to index.</param>
    /// <param name="known">The symbols for the current compilation.</param>
    public ConfigurationTypeIndex(Compilation compilation, KnownSymbols known)
        => _reachable = new Lazy<ImmutableHashSet<INamedTypeSymbol>>(() => Compute(compilation, known), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Whether the schema generator reaches the given type.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is reachable.</returns>
    public bool Contains(INamedTypeSymbol type)
        => _reachable.Value.Contains(type.OriginalDefinition);

    private static ImmutableHashSet<INamedTypeSymbol> Compute(Compilation compilation, KnownSymbols known)
    {
        var reachable = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<INamedTypeSymbol>();
        foreach (var type in EnumerateSourceTypes(compilation.Assembly.GlobalNamespace))
        {
            if (IsRoot(type, known.Configuration) || IsRoot(type, known.ExecutableAction))
                pending.Push(type);
        }

        while (pending.Count > 0)
        {
            // Indexed by original definition, so an open generic base class and every constructed
            // form of it resolve to the same entry.
            var type = pending.Pop().OriginalDefinition;
            if (!reachable.Add(type))
                continue;

            if (type.BaseType is { } baseType && IsInSource(baseType))
                pending.Push(baseType);

            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol property || !ConfigurationMembers.ReachesSchemaGenerator(property, known))
                    continue;

                foreach (var embedded in EnumerateEmbeddedTypes(property.Type))
                {
                    if (IsInSource(embedded))
                        pending.Push(embedded);
                }
            }
        }

        return reachable.ToImmutable();
    }

    /// <summary>
    /// Whether the type is one the schema generator starts a walk at.
    /// </summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="contract">The contract to test against, or <see langword="null"/> when the
    /// compilation does not reference it.</param>
    /// <returns><see langword="true"/> when the type implements the contract.</returns>
    private static bool IsRoot(INamedTypeSymbol type, INamedTypeSymbol? contract)
        => contract is not null && type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default);

    private static bool IsInSource(INamedTypeSymbol type)
    {
        if (type.SpecialType is not SpecialType.None || type.TypeKind is TypeKind.Enum or TypeKind.Delegate or TypeKind.Error)
            return false;

        foreach (var location in type.Locations)
        {
            if (location.IsInSource)
                return true;
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateSourceTypes(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol nested:
                    foreach (var type in EnumerateSourceTypes(nested))
                        yield return type;
                    break;
                case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } type:
                    yield return type;
                    foreach (var nestedType in EnumerateSourceTypes(type))
                        yield return nestedType;
                    break;
            }
        }
    }

    /// <summary>
    /// Yields every named type mentioned by a type reference, so <c>List&lt;Section&gt;</c> yields
    /// both <c>List&lt;Section&gt;</c> and <c>Section</c>.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateEmbeddedTypes(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var embedded in EnumerateEmbeddedTypes(array.ElementType))
                    yield return embedded;
                break;
            case INamedTypeSymbol named:
                yield return named;
                foreach (var argument in named.TypeArguments)
                {
                    foreach (var embedded in EnumerateEmbeddedTypes(argument))
                        yield return embedded;
                }
                break;
        }
    }
}
