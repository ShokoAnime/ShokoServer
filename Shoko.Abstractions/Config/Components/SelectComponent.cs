using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Shoko.Abstractions.Config.Components;

/// <summary>
///   A select component for the UI.
/// </summary>
public class SelectComponent<TValue> where TValue : IEquatable<TValue>
{
    /// <summary>
    ///   Whether there are any currently selected values.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(SelectedValue))]
    public bool HasSelectedValue => SelectedValues is { Length: > 0 };

    /// <summary>
    ///   The first available currently selected value, or <c>null</c> if
    ///   nothing is currently selected.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public TValue? SelectedValue
    {
        get => SelectedValues.FirstOrDefault();
        set => SelectedValues = value is TValue nonNullValue ? [nonNullValue] : [];
    }

    /// <summary>
    ///   All currently selected values, or an empty array if nothing is
    ///   currently selected.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public TValue[] SelectedValues
    {
        get => Options.Where(o => o.IsSelected).Select(o => o.Value).ToArray();
        set
        {
            var values = value is null ? [] : value;
            foreach (var option in Options)
                option.IsSelected = values.Contains(option.Value);
        }
    }

    /// <summary>
    ///   Whether there are any available default values.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(DefaultValue))]
    public bool HasDefaultValue => DefaultValues is { Length: > 0 };

    /// <summary>
    ///   The first available default value, or <c>null</c> if nothing is set as
    ///   default.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public TValue? DefaultValue
    {
        get => DefaultValues.FirstOrDefault();
        set => DefaultValues = value is TValue nonNullValue ? [nonNullValue] : [];
    }

    /// <summary>
    ///  All available default values, or an empty array if nothing is set as
    ///  default.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public TValue[] DefaultValues
    {
        get => Options.Where(o => o.IsDefault).Select(o => o.Value).ToArray();
        set
        {
            var values = value is null ? [] : value;
            foreach (var option in Options)
                option.IsDefault = values.Contains(option.Value);
        }
    }

    private SelectOption<TValue>[] _options = [];

    /// <summary>
    ///   The options for the select component in the UI.
    /// </summary>
    [Required, MinLength(0), DefaultValue(new object[] { })]
    [Newtonsoft.Json.JsonProperty("options")]
    [System.Text.Json.Serialization.JsonPropertyName("options")]
    public IReadOnlyList<SelectOption<TValue>> Options { get => _options; set => _options = value.Distinct().ToArray(); }

    private SelectGroup[] _groups = [];

    /// <summary>
    ///   The groups for the select component in the UI.
    /// </summary>
    [Required, MinLength(0), DefaultValue(new object[] { })]
    [Newtonsoft.Json.JsonProperty("groups")]
    [System.Text.Json.Serialization.JsonPropertyName("groups")]
    public IReadOnlyList<SelectGroup> Groups { get => _groups; set => _groups = value.Distinct().ToArray(); }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectComponent{TValue}"/> class.
    /// </summary>
    public SelectComponent() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectComponent{TValue}"/> class.
    /// </summary>
    /// <param name="options">
    ///   The options for the select component in the UI.
    /// </param>
    /// <param name="groups">
    ///   The groups for the select component in the UI.
    /// </param>
    public SelectComponent(IEnumerable<SelectOption<TValue>> options, IEnumerable<SelectGroup>? groups = null)
    {
        Options = options.ToArray();
        if (groups is not null)
            Groups = groups.ToArray();
    }
}
