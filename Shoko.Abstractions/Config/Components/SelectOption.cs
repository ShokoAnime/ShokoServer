using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Config.Components;

/// <summary>
///   A select option for the UI.
/// </summary>
public class SelectOption<TValue> : IEquatable<SelectOption<TValue>> where TValue : IEquatable<TValue>
{
    /// <summary>
    ///   The label for the option.
    /// </summary>
    [JsonProperty("label")]
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    ///   The unique identifier for the group this option belongs to, or
    ///   <c>null</c> if it should be rendered outside of a group.
    /// </summary>
    [JsonProperty("groupId", NullValueHandling = NullValueHandling.Ignore)]
    [JsonPropertyName("groupId")]
    public uint? GroupID { get; set; }

    /// <summary>
    ///   The value of the option.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    [Required(AllowEmptyStrings = true)]
    public TValue Value { get; set; } = default!;

    /// <summary>
    ///   Whether the option is selected.
    /// </summary>
    [DefaultValue(false)]
    [JsonProperty("selected")]
    [JsonPropertyName("selected")]
    public bool IsSelected { get; set; } = false;

    /// <summary>
    ///   Whether the option is the default.
    /// </summary>
    [DefaultValue(false)]
    [JsonProperty("default")]
    [JsonPropertyName("default")]
    public bool IsDefault { get; set; }

    /// <summary>
    ///   Whether the option is disabled.
    /// </summary>
    [DefaultValue(false)]
    [JsonProperty("disabled")]
    [JsonPropertyName("disabled")]
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectOption{TValue}"/>
    ///   class.
    /// </summary>
    public SelectOption() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectOption{TValue}"/>
    ///   class.
    /// </summary>
    /// <param name="value">
    ///   The value of the option.
    /// </param>
    /// <param name="label">
    ///   The label for the option.
    /// </param>
    /// <param name="isSelected">
    ///   Whether the option is selected.
    /// </param>
    /// <param name="isDefault">
    ///   Whether the option is the default.
    /// </param>
    /// <param name="isDisabled">
    ///   Whether the option is disabled.
    /// </param>
    public SelectOption(TValue value, string? label = null, bool isSelected = false, bool isDefault = false, bool isDisabled = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        Label = label;
        Value = value;
        IsSelected = isSelected;
        IsDefault = isDefault;
        IsDisabled = isDisabled;
    }

    /// <inheritdoc/>
    public bool Equals(SelectOption<TValue>? other)
        => other is not null && Value.Equals(other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as SelectOption<TValue>);

    /// <inheritdoc/>
    public override int GetHashCode()
        => Value.GetHashCode();
}
