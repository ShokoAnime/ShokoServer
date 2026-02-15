using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Abstractions.Config.Components;

/// <summary>
///   A select group for the UI.
/// </summary>
public class SelectGroup : IEquatable<SelectGroup>
{
    /// <summary>
    ///   The unique identifier for the group.
    /// </summary>
    [Newtonsoft.Json.JsonProperty("id")]
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public uint ID { get; set; }

    /// <summary>
    ///   The label for the group.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Newtonsoft.Json.JsonProperty("label")]
    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    ///   Whether the group is disabled.
    /// </summary>
    [Required, DefaultValue(false)]
    [Newtonsoft.Json.JsonProperty("disabled")]
    [System.Text.Json.Serialization.JsonPropertyName("disabled")]
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectGroup"/> class.
    /// </summary>
    public SelectGroup() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectGroup"/> class.
    /// </summary>
    /// <param name="id">
    ///   The unique identifier for the group.
    /// </param>
    /// <param name="label">
    ///   The label for the group.
    /// </param>
    /// <param name="isDisabled">
    ///   Whether the group is disabled.
    /// </param>
    public SelectGroup(uint id, string label, bool isDisabled = false)
    {
        ID = id;
        Label = label;
        IsDisabled = isDisabled;
    }

    /// <inheritdoc/>
    public bool Equals(SelectGroup? other)
        => other is not null && ID == other.ID;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as SelectGroup);

    /// <inheritdoc/>
    public override int GetHashCode()
        => ID.GetHashCode();
}
