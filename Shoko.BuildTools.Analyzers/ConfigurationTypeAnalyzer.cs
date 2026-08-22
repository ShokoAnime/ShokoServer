using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// Reports property shapes that compile fine but that the UI schema generator cannot describe,
/// either because it silently drops the metadata or because it throws while building the schema.
/// </summary>
/// <remarks>
/// <para>
/// Both the properties of a configuration and the invocation parameters of an executable action are
/// walked by the same generator, and the same shapes break both, so both are analysed. See
/// <see cref="ConfigurationTypeIndex"/> for how a root is picked.
/// </para>
/// <para>
/// This is defence in depth. The generator keeps its own runtime validation, because a plugin can
/// be built without ever referencing this package.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigurationTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Diagnostics.NestedCollection,
        Diagnostics.UnusableDictionaryKey,
        Diagnostics.IncompatibleListType,
        Diagnostics.MissingPrimaryKey,
        Diagnostics.NotAGenericDictionary);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            if (KnownSymbols.TryCreate(start.Compilation) is not { } known)
                return;

            var index = new ConfigurationTypeIndex(start.Compilation, known);
            var reported = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            start.RegisterSymbolAction(context => AnalyzeType(context, known, index, reported), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, KnownSymbols known, ConfigurationTypeIndex index, ConcurrentDictionary<string, byte> reported)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!index.Contains(type))
            return;

        // Walking the base chain from the derived type substitutes the type arguments, so a
        // 'Base<T> { List<T> Items }' inherited as 'Base<List<string>>' is seen as
        // 'List<List<string>>' here even though the declaration itself is fine.
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol property || !seenNames.Add(property.Name))
                    continue;
                if (!ConfigurationMembers.ReachesSchemaGenerator(property, known))
                    continue;

                AnalyzeProperty(context, property, type, known, reported);
            }
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol owner, KnownSymbols known, ConcurrentDictionary<string, byte> reported)
    {
        var shape = CollectionShape.Classify(property.Type, known);
        if (shape.Kind is CollectionKind.None)
            return;

        var inner = CollectionShape.Classify(shape.Element, known);
        // A dictionary of collections is fine: the two levels get distinct keys
        // (`+Dict` and `+List`) and the generator produces a usable schema. Only
        // same-kind nesting collides on one key, and only a dictionary inside a
        // list makes the key resolver read the wrong type and throw.
        if (inner.Kind is not CollectionKind.None && !(shape.Kind is CollectionKind.Dictionary && inner.Kind is CollectionKind.List))
        {
            Report(context, reported, Diagnostic.Create(
                Diagnostics.NestedCollection,
                GetTypeLocation(property, owner, context.CancellationToken),
                property.Name,
                property.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                shape.Noun));
            // The outer shape is already wrong, so the remaining rules would only add noise.
            return;
        }

        if (shape.Kind is CollectionKind.Dictionary)
        {
            // GetTKeyAndTValue runs before AssertKeyUsable, so a non-generic dictionary fails there
            // first and never reaches the key check.
            if (!CollectionShape.IsGenericDictionary(property.Type, known))
            {
                Report(context, reported, Diagnostic.Create(
                    Diagnostics.NotAGenericDictionary,
                    GetTypeLocation(property, owner, context.CancellationToken),
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }
            else if (!IsUsableDictionaryKey(shape.Key, known))
            {
                Report(context, reported, Diagnostic.Create(
                    Diagnostics.UnusableDictionaryKey,
                    GetTypeLocation(property, owner, context.CancellationToken),
                    property.Name,
                    shape.Key!.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }
        }

        if (shape.Kind is CollectionKind.List)
            AnalyzeListType(context, property, owner, shape.Element, known, reported);
    }

    /// <summary>
    /// Reports a diagnostic, dropping the repeats that come from several configurations inheriting
    /// the same member.
    /// </summary>
    private static void Report(SymbolAnalysisContext context, ConcurrentDictionary<string, byte> reported, Diagnostic diagnostic)
    {
        if (reported.TryAdd(diagnostic.ToString(), 0))
            context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeListType(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol owner, ITypeSymbol? element, KnownSymbols known, ConcurrentDictionary<string, byte> reported)
    {
        if (known.ListAttribute is null || element is null)
            return;
        if (ConfigurationMembers.FindAttribute(property, known.ListAttribute) is not { } attribute)
            return;
        if (GetListType(attribute) is not { } listType)
            return;

        // Auto is the only display type the generator never rejects.
        if (listType is DisplayListType.Auto)
            return;

        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? GetTypeLocation(property, owner, context.CancellationToken);
        var noun = GetListTypeNoun(listType);
        if (listType is DisplayListType.EnumCheckbox)
        {
            if (CollectionShape.Unwrap(element) is not { TypeKind: TypeKind.Enum })
            {
                Report(context, reported, Diagnostic.Create(
                    Diagnostics.IncompatibleListType,
                    location,
                    property.Name,
                    noun,
                    "enum",
                    listType.ToString(),
                    element.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }

            return;
        }

        // The generator checks the element type first and only then the primary key, so report at
        // most one of the two for a given property.
        if (!ConfigurationMembers.IsSectionContainer(element, known))
        {
            // An element type the analyzer cannot resolve is left alone rather than guessed at.
            if (CollectionShape.Unwrap(element) is not null)
            {
                Report(context, reported, Diagnostic.Create(
                    Diagnostics.IncompatibleListType,
                    location,
                    property.Name,
                    noun,
                    "class",
                    listType.ToString(),
                    element.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }

            return;
        }

        if (known.KeyAttribute is null)
            return;
        if (ConfigurationMembers.HasAttribute(property, known.KeyAttribute) || ConfigurationMembers.DeclaresPrimaryKey(element, known))
            return;

        Report(context, reported, Diagnostic.Create(
            Diagnostics.MissingPrimaryKey,
            location,
            property.Name,
            noun,
            element.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
    }

    /// <summary>
    /// The word the generator uses for the display type in its own messages.
    /// </summary>
    private static string GetListTypeNoun(DisplayListType listType)
        => listType switch
        {
            DisplayListType.EnumCheckbox => "Checkbox",
            DisplayListType.ComplexDropdown => "Dropdown",
            DisplayListType.ComplexTab => "Tab",
            DisplayListType.ComplexInline => "Inline",
            _ => listType.ToString(),
        };

    /// <summary>
    /// Mirrors <c>ShokoJsonSchemaGenerator.AssertKeyUsable</c>, which throws for anything else.
    /// </summary>
    private static bool IsUsableDictionaryKey(ITypeSymbol? key, KnownSymbols known)
    {
        if (CollectionShape.Unwrap(key) is not { } unwrapped)
            return true;
        if (unwrapped.SpecialType is SpecialType.System_String || unwrapped.TypeKind is TypeKind.Enum)
            return true;
        // [Serializable] is a metadata flag, not a stored custom attribute. The runtime synthesises
        // the attribute back from the flag, which is what the generator reads, but the .NET
        // targeting packs drop the flag when they emit their reference assemblies, so a type coming
        // from one cannot be judged here. Assume such a type is fine rather than risk a false error.
        if (unwrapped is INamedTypeSymbol { IsSerializable: true })
            return true;
        if (unwrapped.ContainingAssembly is { } assembly && IsReferenceAssembly(assembly))
            return true;
        if (known.JsonSerializableAttribute is not null && ConfigurationMembers.HasAttribute(unwrapped, known.JsonSerializableAttribute))
            return true;
        if (known.SerializableInterface is not null && unwrapped.AllInterfaces.Contains(known.SerializableInterface, SymbolEqualityComparer.Default))
            return true;

        return false;
    }

    private static bool IsReferenceAssembly(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() is "System.Runtime.CompilerServices.ReferenceAssemblyAttribute")
                return true;
        }

        return false;
    }

    /// <summary>
    /// The property's declared type syntax, falling back to the configuration type that pulls the
    /// property in when the property itself is not declared in source.
    /// </summary>
    private static Location GetTypeLocation(IPropertySymbol property, INamedTypeSymbol owner, CancellationToken cancellationToken)
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is PropertyDeclarationSyntax { Type: { } type })
                return type.GetLocation();
        }

        foreach (var location in property.Locations)
        {
            if (location.IsInSource)
                return location;
        }

        foreach (var location in owner.Locations)
        {
            if (location.IsInSource)
                return location;
        }

        return Location.None;
    }

    /// <summary>
    /// The <c>Shoko.Abstractions.UI.Enums.DisplayListType</c> values, by their underlying value.
    /// </summary>
    private enum DisplayListType
    {
        Auto = 0,
        EnumCheckbox = 1,
        ComplexDropdown = 2,
        ComplexTab = 3,
        ComplexInline = 4,
    }

    private static DisplayListType? GetListType(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key is "ListType" && argument.Value.Value is int value && Enum.IsDefined(typeof(DisplayListType), value))
                return (DisplayListType)value;
        }

        return null;
    }
}
