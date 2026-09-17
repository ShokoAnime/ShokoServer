using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// How a condition compares the value it points at.
/// </summary>
/// <remarks>
/// A client that meets an operator it does not know treats the condition as
/// unmet, so an element hides or stays enabled rather than acting on a
/// comparison it guessed at.
/// </remarks>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum UiConditionOperator
{
    /// <summary>
    /// The value equals the one the condition carries.
    /// </summary>
    [EnumMember(Value = "equals")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("equals")]
    Equals = 0,

    /// <summary>
    /// The value differs from the one the condition carries.
    /// </summary>
    [EnumMember(Value = "not-equals")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("not-equals")]
    NotEquals = 1,

    /// <summary>
    /// The value is absent: <c>null</c>, an empty string, or an empty
    /// collection. Carries no value of its own.
    /// </summary>
    /// <remarks>
    /// A cleared text field holds an empty string rather than <c>null</c>, which
    /// an equality test against <c>null</c> does not catch.
    /// </remarks>
    [EnumMember(Value = "is-empty")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("is-empty")]
    IsEmpty = 2,

    /// <summary>
    /// The value is present. Carries no value of its own.
    /// </summary>
    [EnumMember(Value = "is-not-empty")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("is-not-empty")]
    IsNotEmpty = 3,

    /// <summary>
    /// The value is one of the ones the condition carries.
    /// </summary>
    [EnumMember(Value = "in")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("in")]
    In = 4,

    /// <summary>
    /// The value is none of the ones the condition carries.
    /// </summary>
    [EnumMember(Value = "not-in")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("not-in")]
    NotIn = 5,

    /// <summary>
    /// The value is greater than the one the condition carries. Numbers only.
    /// </summary>
    [EnumMember(Value = "greater-than")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("greater-than")]
    GreaterThan = 6,

    /// <summary>
    /// The value is less than the one the condition carries. Numbers only.
    /// </summary>
    [EnumMember(Value = "less-than")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("less-than")]
    LessThan = 7,

    /// <summary>
    /// The value holds the one the condition carries — a substring of a string,
    /// or an entry of a collection.
    /// </summary>
    [EnumMember(Value = "contains")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("contains")]
    Contains = 8,
}
