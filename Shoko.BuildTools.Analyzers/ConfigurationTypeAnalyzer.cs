using System.Collections.Concurrent;
using System.Collections.Immutable;
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
        Diagnostics.NotAGenericDictionary,
        Diagnostics.UnusableCondition,
        Diagnostics.UnusableReactiveHandler,
        Diagnostics.UnusableOptionsProviderMethod,
        Diagnostics.UnknownOptionsMember,
        Diagnostics.MemberTakesNoOptions,
        Diagnostics.UnusableOptionType,
        Diagnostics.OptionTypeMismatch,
        Diagnostics.OptionsClaimedTwice,
        Diagnostics.FlagEnumWithoutMembers);

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
        AnalyzeOptionsProviders(context, type, known, reported);

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                // A condition sits on a member the schema generator may well skip, and is
                // checked all the same: the generator reads the attribute before it decides
                // what to do with the member.
                if (member is IMethodSymbol method)
                {
                    AnalyzeConditions(context, method, method.Name, type, known.CustomActionAttribute, known, reported);
                    AnalyzeReactiveHandler(context, method, type, known, reported);
                    continue;
                }

                if (member is not IPropertySymbol property || !seenNames.Add(property.Name))
                    continue;

                AnalyzeConditions(context, property, property.Name, type, known.VisibilityAttribute, known, reported);
                if (!ConfigurationMembers.ReachesSchemaGenerator(property, known))
                    continue;

                AnalyzeProperty(context, property, type, known, reported);
            }
        }
    }

    /// <summary>
    /// Checks what a lifecycle hook says it reacts to.
    /// </summary>
    private static void AnalyzeReactiveHandler(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        INamedTypeSymbol owner,
        KnownSymbols known,
        ConcurrentDictionary<string, byte> reported
    )
    {
        const int liveEdit = 4;
        if (known.ConfigurationActionAttribute is null)
            return;
        if (ConfigurationMembers.FindAttribute(method, known.ConfigurationActionAttribute) is not { } attribute)
            return;

        var arguments = attribute.NamedArguments.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var members = arguments.TryGetValue("ReactiveMembers", out var named) && !named.IsNull ? named.Values : default;
        var events = arguments.TryGetValue("Events", out var raised) && !raised.IsNull ? raised.Values : default;
        if (members.IsDefaultOrEmpty && events.IsDefaultOrEmpty)
            return;

        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? method.Locations.FirstOrDefault()
            ?? Location.None;
        // Only a live edit is raised by an event; the type is the attribute's one positional
        // argument, and an unreadable one is left alone rather than guessed at.
        if (attribute.ConstructorArguments.FirstOrDefault().Value is int actionType && actionType != liveEdit)
        {
            Report(context, reported, Diagnostic.Create(
                Diagnostics.UnusableReactiveHandler,
                location,
                $"{owner.Name}.{method.Name}",
                "handles a hook that no event raises, so it cannot narrow what it reacts to"));
            return;
        }

        // Only events may have been named, which leaves the members unset.
        foreach (var member in members.IsDefault ? [] : members)
        {
            if (member.Value is not string path || string.IsNullOrWhiteSpace(path))
                continue;
            if (ConditionShape.ResolvePath(owner, path, known, out var failure) is not null || failure is null)
                continue;

            Report(context, reported, Diagnostic.Create(
                Diagnostics.UnusableReactiveHandler,
                location,
                $"{owner.Name}.{method.Name}",
                $"watches a member that {failure}"));
        }
    }

    /// <summary>
    /// Checks every <c>[OptionsProvider]</c> a type declares, the way
    /// <c>UiOptionsProvider.GetProviders</c> does at startup.
    /// </summary>
    private static void AnalyzeOptionsProviders(
        SymbolAnalysisContext context,
        INamedTypeSymbol owner,
        KnownSymbols known,
        ConcurrentDictionary<string, byte> reported
    )
    {
        if (known.OptionsProviderAttribute is null)
            return;

        // Keyed by member and part, since a dictionary's keys and values may
        // each have a provider of their own.
        var claimed = new Dictionary<(string Member, bool Keys), IMethodSymbol>();
        for (var current = owner; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind is not MethodKind.Ordinary || ConfigurationMembers.FindAttribute(method, known.OptionsProviderAttribute) is not { } attribute)
                    continue;
                if (GetOptionsProviderFault(method, attribute, owner, claimed, known) is not var (rule, fault))
                    continue;

                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? method.Locations.FirstOrDefault()
                    ?? Location.None;
                Report(context, reported, Diagnostic.Create(
                    rule,
                    location,
                    $"{owner.Name}.{method.Name}",
                    fault));
            }
        }
    }

    /// <summary>
    /// The first thing wrong with a provider, and the rule it breaks.
    /// </summary>
    private static (DiagnosticDescriptor Rule, string Fault)? GetOptionsProviderFault(
        IMethodSymbol method,
        AttributeData attribute,
        INamedTypeSymbol owner,
        Dictionary<(string Member, bool Keys), IMethodSymbol> claimed,
        KnownSymbols known
    )
    {
        if (method.DeclaredAccessibility is not Accessibility.Public)
            return (Diagnostics.UnusableOptionsProviderMethod, "is not public");
        if (method.IsGenericMethod)
            return (Diagnostics.UnusableOptionsProviderMethod, "is generic");

        // `params string[]` arrives as one array argument, however it was written.
        var argument = attribute.ConstructorArguments.FirstOrDefault();
        var members = argument.Kind is TypedConstantKind.Array && !argument.IsNull
            ? argument.Values.Select(x => x.Value as string).ToList()
            : [];
        if (members.Count is 0)
            return (Diagnostics.UnknownOptionsMember, "names no members");

        // `Target` is `OptionsTarget`, where `Keys` is 1 and the default `Values` is 0.
        var forKeys = attribute.NamedArguments.Any(x => x.Key is "Target" && x.Value.Value is 1);

        ITypeSymbol? optionType = null;
        string? firstMember = null;
        foreach (var member in members)
        {
            if (member is null)
                return null;
            if (FindProperty(owner, member) is not { } property)
                return (Diagnostics.UnknownOptionsMember, $"names \"{member}\", which {owner.Name} does not have");
            if (GetOptionType(property.Type, forKeys, known, out var shapeFault) is not { } memberOptionType)
                return shapeFault is null ? null : (Diagnostics.MemberTakesNoOptions, $"names \"{member}\", which {shapeFault}");
            if (!IsAllowedOptionType(memberOptionType, known))
            {
                var typeName = memberOptionType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                return (Diagnostics.UnusableOptionType, $"names \"{member}\", whose options would be {typeName}, which is neither a primitive nor convertible to and from one");
            }
            if (claimed.TryGetValue((member, forKeys), out var other))
                return SymbolEqualityComparer.Default.Equals(other, method)
                    ? (Diagnostics.UnknownOptionsMember, $"names \"{member}\" twice")
                    : (Diagnostics.OptionsClaimedTwice, $"names \"{member}\", whose {(forKeys ? "keys" : "values")} {other.Name} already provides for");

            claimed[(member, forKeys)] = method;
            if (optionType is null)
            {
                (optionType, firstMember) = (memberOptionType, member);
            }
            else if (!SymbolEqualityComparer.Default.Equals(optionType, memberOptionType))
            {
                var firstName = optionType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                var otherName = memberOptionType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                return (Diagnostics.OptionTypeMismatch, $"names \"{firstMember}\" and \"{member}\", whose options are {firstName} and {otherName}");
            }
        }

        var returnType = method.ReturnType;
        // A return type that cannot be resolved is left alone rather than guessed at.
        if (returnType is IErrorTypeSymbol || optionType is null)
            return null;

        var returned = GetReturnedOptionType(returnType, known);
        if (SymbolEqualityComparer.Default.Equals(returned, optionType))
            return null;
        if (returned is INamedTypeSymbol named && known.SelectOption is not null &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.SelectOption) &&
            SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], optionType))
            return null;

        var returnName = returnType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        var optionName = optionType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        return returned is null
            ? (Diagnostics.UnusableOptionsProviderMethod, $"returns {returnName}, which is not a collection of options")
            : (Diagnostics.OptionTypeMismatch, $"returns {returnName} rather than a collection of {optionName}");
    }

    /// <summary>
    /// The public instance property of that name, on the type or a base type.
    /// </summary>
    private static IPropertySymbol? FindProperty(INamedTypeSymbol owner, string name)
    {
        for (var current = owner; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property)
                    return property;
            }
        }

        return null;
    }

    /// <summary>
    /// The type of a single option for a part of a member, the way
    /// <c>UiOptionsProvider.GetOptionType</c> works it out: a list takes options for its entries, a
    /// dictionary for its values or keys, and a nullable value for the type it wraps.
    /// </summary>
    private static ITypeSymbol? GetOptionType(ITypeSymbol memberType, bool forKeys, KnownSymbols known, out string? fault)
    {
        fault = null;
        if (CollectionShape.Unwrap(memberType) is not { } type)
            return null;

        var shape = CollectionShape.Classify(type, known);
        if (shape.Kind is not CollectionKind.Dictionary)
        {
            if (forKeys)
            {
                fault = "is not a dictionary, so it has no keys";
                return null;
            }

            return GetValueOptionType(type, known, out fault);
        }

        if (shape.Element is null)
        {
            fault = "is a dictionary without key and value types";
            return null;
        }

        if (forKeys)
            return CollectionShape.Unwrap(shape.Key);
        if (CollectionShape.Unwrap(shape.Element) is not { } valueType)
            return null;
        if (CollectionShape.Classify(valueType, known).Kind is CollectionKind.Dictionary)
        {
            fault = "holds dictionaries, which have no single value to offer options for";
            return null;
        }

        return GetValueOptionType(valueType, known, out fault);
    }

    /// <summary>
    /// Mirrors <c>UiOptionsProvider.IsAllowedOptionType</c>: a primitive, a string, a decimal, an
    /// enum, one of the common value types that round-trip through text, or a type implementing
    /// <c>IParsable</c> of itself or carrying <c>[TypeConverter]</c>. Whether that converter really
    /// handles text or a primitive is left to the startup check, which can run it.
    /// </summary>
    private static bool IsAllowedOptionType(ITypeSymbol type, KnownSymbols known)
    {
        if (type.TypeKind is TypeKind.Enum)
            return true;
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String or SpecialType.System_Decimal or
                SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
                SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_Single or SpecialType.System_Double or
                SpecialType.System_DateTime:
                return true;
        }

        if (type.ToDisplayString() is "System.Guid" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan" or "System.Uri")
            return true;

        if (known.Parsable is not null && type.AllInterfaces.Any(x =>
            SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, known.Parsable) &&
            SymbolEqualityComparer.Default.Equals(x.TypeArguments[0], type)))
            return true;

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (known.TypeConverterAttribute is not null && ConfigurationMembers.HasAttribute(current, known.TypeConverterAttribute))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The option type of a value that is not a dictionary: itself, or a list's entries.
    /// </summary>
    private static ITypeSymbol? GetValueOptionType(ITypeSymbol type, KnownSymbols known, out string? fault)
    {
        fault = null;
        if (type is INamedTypeSymbol named && known.SelectComponent is not null &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.SelectComponent))
        {
            fault = "is a select component, which carries its own options";
            return null;
        }

        if (type.SpecialType is SpecialType.System_String)
            return type;
        if (type is IArrayTypeSymbol array)
            return CollectionShape.Unwrap(array.ElementType);
        if (CollectionShape.FindEnumerable(type) is { } enumerable)
            return CollectionShape.Unwrap(enumerable.TypeArguments[0]);

        return type;
    }

    /// <summary>
    /// The type of a single option a method lists, looking through a task.
    /// </summary>
    private static ITypeSymbol? GetReturnedOptionType(ITypeSymbol returnType, KnownSymbols known)
    {
        if (returnType is INamedTypeSymbol { IsGenericType: true } named &&
            (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.GenericTask) ||
                SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.GenericValueTask)))
            returnType = named.TypeArguments[0];
        if (returnType.SpecialType is SpecialType.System_String)
            return null;

        // A nullable entry is listed for the type it wraps, its nulls skipped.
        var element = returnType is IArrayTypeSymbol array ? array.ElementType : CollectionShape.FindEnumerable(returnType)?.TypeArguments[0];
        return element is null ? null : CollectionShape.Unwrap(element) ?? element;
    }

    /// <summary>
    /// Checks the toggle and disable conditions one attribute carries.
    /// </summary>
    private static void AnalyzeConditions(
        SymbolAnalysisContext context,
        ISymbol member,
        string memberName,
        INamedTypeSymbol owner,
        INamedTypeSymbol? attributeType,
        KnownSymbols known,
        ConcurrentDictionary<string, byte> reported
    )
    {
        if (attributeType is null)
            return;
        if (ConfigurationMembers.FindAttribute(member, attributeType) is not { } attribute)
            return;

        var fallback = member.Locations.FirstOrDefault() ?? Location.None;
        foreach (var prefix in new[] { "Toggle", "Disable" })
        {
            if (ConditionShape.Read(attribute, prefix, fallback, context.CancellationToken) is not { } condition)
                continue;
            if (condition.Fault(owner, known) is not { } fault)
                continue;

            Report(context, reported, Diagnostic.Create(
                Diagnostics.UnusableCondition,
                condition.Location,
                $"{owner.Name}.{memberName}",
                fault));
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol owner, KnownSymbols known, ConcurrentDictionary<string, byte> reported)
    {
        var shape = CollectionShape.Classify(property.Type, known);
        if (shape.Kind is CollectionKind.None)
            return;

        AnalyzeFlagEnum(context, property, owner, shape, known, reported);

        // A flags enum is a list of its own members, which hold nothing further.
        var inner = shape.IsFlagEnum ? CollectionShape.None : CollectionShape.Classify(shape.Element, known);
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
    /// Reports a <c>[Flags]</c> enum, held by the property or as a dictionary's values, with no
    /// single-bit member for its list to hold.
    /// </summary>
    private static void AnalyzeFlagEnum(
        SymbolAnalysisContext context,
        IPropertySymbol property,
        INamedTypeSymbol owner,
        CollectionShape shape,
        KnownSymbols known,
        ConcurrentDictionary<string, byte> reported
    )
    {
        var flagEnum = shape.IsFlagEnum
            ? shape.Element
            : shape.Kind is CollectionKind.Dictionary && CollectionShape.IsFlagEnumType(shape.Element, known) ? CollectionShape.Unwrap(shape.Element) : null;
        if (flagEnum is null || CollectionShape.HasSingleBitMember(flagEnum))
            return;

        Report(context, reported, Diagnostic.Create(
            Diagnostics.FlagEnumWithoutMembers,
            GetTypeLocation(property, owner, context.CancellationToken),
            property.Name,
            flagEnum.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
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

        // Auto and Flat are the display types the generator never rejects.
        if (listType is DisplayListType.Auto or DisplayListType.Flat)
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
    /// Mirrors <c>ShokoJsonSchemaGenerator.AssertKeyUsable</c>, which throws for anything else. A
    /// <c>[Flags]</c> enum is written as a list of its members, which no key can be.
    /// </summary>
    private static bool IsUsableDictionaryKey(ITypeSymbol? key, KnownSymbols known)
    {
        if (CollectionShape.Unwrap(key) is not { } unwrapped)
            return true;
        if (CollectionShape.IsFlagEnumType(unwrapped, known))
            return false;
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
        Flat = 5,
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
