using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Checks shared by the attributes that author a condition.
/// </summary>
internal static class ConditionAuthoring
{
    /// <summary>
    /// Whether a condition has whatever its operator needs to compare with.
    /// </summary>
    /// <param name="conditionOperator">How the member is compared.</param>
    /// <param name="hasValue">Whether a single value was authored.</param>
    /// <param name="values">The value set, if one was authored.</param>
    /// <returns>
    /// <see langword="true"/> when the operator compares nothing, or has the
    /// value or the non-empty set it compares with.
    /// </returns>
    public static bool HasComparand(UiConditionOperator conditionOperator, bool hasValue, object?[]? values)
        => conditionOperator switch
        {
            UiConditionOperator.IsEmpty or UiConditionOperator.IsNotEmpty => true,
            UiConditionOperator.In or UiConditionOperator.NotIn => values is { Length: > 0 },
            _ => hasValue,
        };
}
