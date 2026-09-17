using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Checks that an authored condition can actually be evaluated, before it
///   reaches a client that would silently never match it.
/// </summary>
/// <remarks>
///   <para>
///     A condition that cannot hold is a defect in the configuration, not a
///     condition to recover from: the element it guards renders in whichever
///     state the author did not intend, and nothing says why. The same shapes
///     are reported at the authoring site by the SHOKO0006 analyzer rule; this
///     is the check for a plugin built without the analyzer package.
///   </para>
/// </remarks>
internal static class UiConditionValidator
{
    /// <summary>
    ///   Validates one condition.
    /// </summary>
    /// <param name="owner">The type the condition is declared on.</param>
    /// <param name="declaredBy">What declared it, for the message.</param>
    /// <param name="path">The member the condition compares.</param>
    /// <param name="conditionOperator">How it compares.</param>
    /// <param name="hasValue">Whether a single value was authored.</param>
    /// <param name="values">The value set, if one was authored.</param>
    /// <param name="value">The single value, if one was authored.</param>
    /// <exception cref="NotSupportedException">
    ///   Thrown when the operator disagrees with what it was given, or the path
    ///   does not lead to a member that can be compared.
    /// </exception>
    public static void Validate(
        Type owner,
        string declaredBy,
        string path,
        UiConditionOperator conditionOperator,
        bool hasValue,
        object? value,
        IReadOnlyList<object?>? values
    )
    {
        var hasValues = values is { Count: > 0 };
        switch (conditionOperator)
        {
            case UiConditionOperator.IsEmpty or UiConditionOperator.IsNotEmpty when hasValue || hasValues:
                throw Invalid(owner, declaredBy, conditionOperator, "compares nothing, so it takes no value");
            case UiConditionOperator.In or UiConditionOperator.NotIn when !hasValues:
                throw Invalid(owner, declaredBy, conditionOperator, "matches against a set, so it needs one or more values");
            case UiConditionOperator.In or UiConditionOperator.NotIn when hasValue:
                throw Invalid(owner, declaredBy, conditionOperator, "matches against a set, so it takes values rather than a single value");
            case not (UiConditionOperator.IsEmpty or UiConditionOperator.IsNotEmpty or UiConditionOperator.In or UiConditionOperator.NotIn) when hasValues:
                throw Invalid(owner, declaredBy, conditionOperator, "compares one value, so it takes a value rather than a set");
            case (UiConditionOperator.GreaterThan or UiConditionOperator.LessThan) when !IsNumber(value):
                throw Invalid(owner, declaredBy, conditionOperator, "compares numbers, so it takes a numeric value");
        }

        // A condition names a member of the object it is declared in, and may
        // descend into a nested one. It cannot point through a collection: the
        // path has no index to say which item it meant.
        var target = ResolvePath(owner, path, out var failure);
        if (target is null)
            throw Invalid(owner, declaredBy, conditionOperator, failure!);

        switch (conditionOperator)
        {
            case UiConditionOperator.GreaterThan or UiConditionOperator.LessThan when !IsNumericType(target):
                throw Invalid(owner, declaredBy, conditionOperator, $"compares numbers, but \"{path}\" is a {target.Name}");
            case UiConditionOperator.Contains when target != typeof(string) && GetElementType(target) is null:
                throw Invalid(owner, declaredBy, conditionOperator, $"needs a string or a collection, but \"{path}\" is a {target.Name}");
        }
    }

    /// <summary>
    ///   Walks a dotted path from the type that declared the condition, and
    ///   returns the type of the member it lands on.
    /// </summary>
    /// <param name="owner">The type to start from.</param>
    /// <param name="path">The dotted path.</param>
    /// <param name="failure">Why the walk stopped, when it did.</param>
    /// <returns>
    ///   The member's type, or <see langword="null"/> when the path does not
    ///   resolve.
    /// </returns>
    public static Type? ResolvePath(Type owner, string path, out string? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            failure = "names no member";
            return null;
        }

        var current = owner;
        var segments = path.Split('.');
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (GetMemberType(current, segment) is not { } memberType)
            {
                failure = $"names \"{segment}\", which {current.Name} does not have";
                return null;
            }

            if (index == segments.Length - 1)
                return memberType;

            if (GetElementType(memberType) is not null)
            {
                failure = $"points through \"{segment}\", which is a collection, and a condition has no index to say which item it meant";
                return null;
            }

            current = memberType;
        }

        return null;
    }

    private static Type? GetMemberType(Type owner, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (owner.GetProperty(name, flags) is { CanRead: true } property)
            return Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        return owner.GetField(name, flags) is { } field
            ? Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType
            : null;
    }

    private static Type? GetElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        if (type.IsArray)
            return type.GetElementType();

        return type.GetInterfaces().Prepend(type)
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0] ?? (typeof(IEnumerable).IsAssignableFrom(type) ? typeof(object) : null);
    }

    private static bool IsNumericType(Type type)
        => Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or
            TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static bool IsNumber(object? value)
        => value is not null && IsNumericType(value.GetType());

    private static NotSupportedException Invalid(Type owner, string declaredBy, UiConditionOperator conditionOperator, string reason)
        => new($"The condition on {owner.Name}.{declaredBy} with operator '{conditionOperator}' {reason}.");
}
