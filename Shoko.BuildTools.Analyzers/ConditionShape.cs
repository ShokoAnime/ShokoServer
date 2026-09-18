using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// How a condition compares the value it points at. Mirrors
/// <c>Shoko.Abstractions.UI.Enums.UiConditionOperator</c>, which the analyzer cannot reference
/// directly because it is compiled against the consumer's own copy of the abstractions.
/// </summary>
internal enum ConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    IsEmpty = 2,
    IsNotEmpty = 3,
    In = 4,
    NotIn = 5,
    GreaterThan = 6,
    LessThan = 7,
    Contains = 8,
}

/// <summary>
/// One authored condition, read off a <c>[Visibility]</c> or <c>[CustomAction]</c> attribute.
/// </summary>
internal readonly struct ConditionShape(
    string path,
    ConditionOperator conditionOperator,
    bool hasValue,
    TypedConstant value,
    bool hasValues,
    Location location
)
{
    /// <summary>The member the condition compares.</summary>
    public string Path { get; } = path;

    /// <summary>How it compares.</summary>
    public ConditionOperator Operator { get; } = conditionOperator;

    /// <summary>Whether a single value was authored. <c>null</c> is a value.</summary>
    public bool HasValue { get; } = hasValue;

    /// <summary>The single value, when one was authored.</summary>
    public TypedConstant Value { get; } = value;

    /// <summary>Whether a value set was authored.</summary>
    public bool HasValues { get; } = hasValues;

    /// <summary>Where to report a fault.</summary>
    public Location Location { get; } = location;

    /// <summary>
    /// Reads the toggle or disable condition off an attribute, or returns <see langword="null"/>
    /// when the attribute names no member for it.
    /// </summary>
    /// <param name="attribute">The attribute to read.</param>
    /// <param name="prefix"><c>Toggle</c> or <c>Disable</c>.</param>
    /// <param name="fallback">Where to report, when the attribute has no syntax of its own.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The condition, or <see langword="null"/>.</returns>
    public static ConditionShape? Read(AttributeData attribute, string prefix, Location fallback, System.Threading.CancellationToken cancellationToken)
    {
        var arguments = attribute.NamedArguments.ToDictionary(x => x.Key, x => x.Value, System.StringComparer.Ordinal);
        if (!arguments.TryGetValue($"{prefix}WhenMemberIsSet", out var member) || member.Value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        var conditionOperator = arguments.TryGetValue($"{prefix}Operator", out var op) && op.Value is int value
            ? (ConditionOperator)value
            : ConditionOperator.Equals;
        var hasValue = arguments.TryGetValue($"{prefix}WhenSetTo", out var single);
        var hasValues = arguments.TryGetValue($"{prefix}WhenSetToAny", out var many) && !many.IsNull && many.Values.Length > 0;
        var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? fallback;
        return new(path, conditionOperator, hasValue, single, hasValues, location);
    }

    /// <summary>
    /// Says why the condition could never hold, or <see langword="null"/> when it can.
    /// </summary>
    /// <remarks>
    /// Kept in step with <c>UiConditionValidator</c> on the server, which is what a plugin built
    /// without this package runs into instead, at startup.
    /// </remarks>
    /// <param name="owner">The type the condition is declared on.</param>
    /// <param name="known">The resolved symbols.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    public string? Fault(INamedTypeSymbol owner, KnownSymbols known)
    {
        switch (Operator)
        {
            case ConditionOperator.IsEmpty or ConditionOperator.IsNotEmpty when HasValue || HasValues:
                return $"uses '{Operator}', which compares nothing, so it takes no value";
            case ConditionOperator.In or ConditionOperator.NotIn when !HasValues:
                return $"uses '{Operator}', which matches against a set, so it needs one or more values";
            case ConditionOperator.In or ConditionOperator.NotIn when HasValue:
                return $"uses '{Operator}', which matches against a set, so it takes values rather than a single value";
            case not (ConditionOperator.IsEmpty or ConditionOperator.IsNotEmpty or ConditionOperator.In or ConditionOperator.NotIn) when HasValues:
                return $"uses '{Operator}', which compares one value, so it takes a value rather than a set";
            case ConditionOperator.GreaterThan or ConditionOperator.LessThan when !IsNumber(Value):
                return $"uses '{Operator}', which compares numbers, so it takes a numeric value";
        }

        var target = Resolve(owner, known, out var failure);
        if (target is null)
            return failure;

        return Operator switch
        {
            ConditionOperator.GreaterThan or ConditionOperator.LessThan when !IsNumericType(target) =>
                $"uses '{Operator}', which compares numbers, but '{Path}' is a {target.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}",
            ConditionOperator.Contains when target.SpecialType is not SpecialType.System_String && CollectionShape.Classify(target, known).Kind is CollectionKind.None =>
                $"uses '{Operator}', which needs a string or a collection, but '{Path}' is a {target.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}",
            _ => null,
        };
    }

    /// <summary>
    /// Walks the dotted path from the declaring type to the member it lands on.
    /// </summary>
    private ITypeSymbol? Resolve(INamedTypeSymbol owner, KnownSymbols known, out string? failure)
    {
        failure = null;
        ITypeSymbol current = owner;
        var segments = Path.Split('.');
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (FindMember(current, segment) is not { } memberType)
            {
                // A type the analyzer cannot see into is left alone rather than guessed at.
                if (current.TypeKind is TypeKind.Error)
                    return null;

                failure = $"names '{segment}', which {current.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)} does not have";
                return null;
            }

            if (index == segments.Length - 1)
                return memberType;

            if (CollectionShape.Classify(memberType, known).Kind is not CollectionKind.None)
            {
                failure = $"points through '{segment}', which is a collection, and a condition has no index to say which entry it meant";
                return null;
            }

            current = memberType;
        }

        return null;
    }

    private static ITypeSymbol? FindMember(ITypeSymbol owner, string name)
    {
        for (var current = owner; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { GetMethod: not null } property)
                    return Unwrap(property.Type);
                if (member is IFieldSymbol field)
                    return Unwrap(field.Type);
            }
        }

        return null;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    private static bool IsNumericType(ITypeSymbol type)
        => type.TypeKind is not TypeKind.Enum && type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal;

    private static bool IsNumber(TypedConstant value)
        => !value.IsNull && value.Type is { } type && IsNumericType(type);
}
