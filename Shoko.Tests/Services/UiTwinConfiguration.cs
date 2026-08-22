using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Tests.Services;

/// <summary>
///   The Newtonsoft-serialised twin.
/// </summary>
/// <remarks>
///   Both twins hold the same <see cref="TwinBody"/>, so any difference between
///   the two produced definitions comes from the serialiser and nothing else.
///   The body is held rather than inherited on purpose: the generator keys its
///   property bags on <c>MemberInfo.ReflectedType</c> but its class bags on the
///   contextual type, so inherited properties silently lose their bag.
/// </remarks>
[Section(DisplaySectionType.Tab, DefaultSectionName = "General", AppendFloatingSectionsAtEnd = true, ShowSaveAction = true)]
public class NewtonsoftTwinConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The shared body.</summary>
    public TwinBody Body { get; set; } = new();
}

/// <summary>
///   The System.Text.Json-serialised twin.
/// </summary>
[Section(DisplaySectionType.Tab, DefaultSectionName = "General", AppendFloatingSectionsAtEnd = true, ShowSaveAction = true)]
public class SystemTextJsonTwinConfiguration : IConfiguration
{
    /// <summary>The shared body.</summary>
    public TwinBody Body { get; set; } = new();
}

/// <summary>
///   Serialiser-agnostic shape carrying one of every element the generator can
///   emit.
/// </summary>
[Display(Name = "Twin Body")]
[Section(DisplaySectionType.FieldSet, DefaultSectionName = "General", AppendFloatingSectionsAtEnd = true)]
public class TwinBody
{
    /// <summary>The name of the thing.</summary>
    [Display(Name = "Display Name", Order = 1)]
    [Badge("New", Theme = DisplayColorTheme.Primary)]
    [DefaultValue("shoko")]
    public string Name { get; set; } = "shoko";

    /// <summary>Whether the thing is on.</summary>
    [Display(Order = 2)]
    [RequiresRestart]
    [EnvironmentVariable("TWIN_ENABLED", AllowOverride = false)]
    public bool Enabled { get; set; } = true;

    /// <summary>How many things.</summary>
    [Display(Order = 3)]
    [Range(1, 100)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true, ToggleWhenMemberIsSet = nameof(Enabled), ToggleWhenSetTo = false, ToggleVisibilityTo = DisplayVisibility.ReadOnly)]
    public int Count { get; set; } = 4;

    /// <summary>How much of a thing.</summary>
    [Display(Order = 4, GroupName = "Tuning")]
    [DeniedValues(0.0, 1.0)]
    public double Ratio { get; set; } = 0.5;

    /// <summary>The mode to run in.</summary>
    [Display(Order = 5)]
    [SectionName("Behaviour")]
    public TwinMode Mode { get; set; } = TwinMode.Balanced;

    /// <summary>The modes to offer.</summary>
    [Display(Order = 6)]
    [SectionName("Behaviour")]
    [List(ListType = DisplayListType.EnumCheckbox)]
    public List<TwinMode> Modes { get; set; } = [];

    /// <summary>A secret.</summary>
    [Display(Order = 7)]
    [PasswordPropertyText]
    public string Secret { get; set; } = string.Empty;

    /// <summary>A longer note.</summary>
    [Display(Order = 8)]
    [TextArea]
    public string Note { get; set; } = string.Empty;

    /// <summary>Some code.</summary>
    [Display(Order = 9)]
    [CodeEditor(CodeEditorLanguage.Json, AutoFormatOnLoad = true)]
    public string Script { get; set; } = "{}";

    /// <summary>The endpoints to talk to.</summary>
    [Display(Order = 10)]
    [List(ListType = DisplayListType.ComplexTab)]
    public List<TwinEndpoint> Endpoints { get; set; } = [];

    /// <summary>Per-mode weights, keyed by an enum.</summary>
    /// <remarks>
    ///   The key type here is the reason the record key element cannot be
    ///   hardcoded to a string element.
    /// </remarks>
    [Display(Order = 11)]
    [Record(HideRemoveAction = true)]
    public Dictionary<TwinMode, int> Weights { get; set; } = [];

    /// <summary>Per-name toggles, keyed by a string.</summary>
    [Display(Order = 12)]
    public Dictionary<string, bool> Toggles { get; set; } = [];

    /// <summary>A server-populated selection.</summary>
    [Display(Order = 13)]
    [Select(SelectType = DisplaySelectType.CheckboxList, MultipleItems = true)]
    public SelectComponent<string> Picked { get; set; } = new();

    /// <summary>Does a thing.</summary>
    [Display(Name = "Do The Thing")]
    [CustomAction(Icon = "Play", Theme = DisplayColorTheme.Secondary, Position = DisplayButtonPosition.Start, Size = DisplayElementSize.Small, DisableIfNoChanges = true)]
    public void DoTheThingAction() { }

    /// <summary>Does another thing.</summary>
    [CustomAction(Position = DisplayButtonPosition.End, SectionName = "Behaviour", ToggleWhenMemberIsSet = nameof(Enabled), ToggleWhenSetTo = true)]
    public void DoAnotherThingAction() { }

    /// <summary>Validates the configuration.</summary>
    [ConfigurationAction(ConfigurationActionType.Validate)]
    public void ValidateHandler() { }
}

/// <summary>
///   An endpoint entry, used as a complex list item so the list gets a primary
///   key and a class definition of its own.
/// </summary>
[Section(DisplaySectionType.FieldSet)]
public class TwinEndpoint
{
    /// <summary>The endpoint's id.</summary>
    [Key]
    [Display(Order = 1)]
    public string ID { get; set; } = string.Empty;

    /// <summary>The endpoint's url.</summary>
    [Display(Order = 2)]
    [Url]
    public string Url { get; set; } = string.Empty;

    /// <summary>The endpoint's mode.</summary>
    [Display(Order = 3)]
    public TwinMode Mode { get; set; } = TwinMode.Balanced;
}

/// <summary>
///   An enum that carries both serialisers' naming attributes, in agreement.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TwinMode
{
    /// <summary>Go slow.</summary>
    [EnumMember(Value = "slow-and-steady")]
    [JsonStringEnumMemberName("slow-and-steady")]
    [Description("Takes its time.")]
    Slow = 0,

    /// <summary>Go at a sensible pace.</summary>
    [EnumMember(Value = "balanced")]
    [JsonStringEnumMemberName("balanced")]
    Balanced = 1,

    /// <summary>Go fast.</summary>
    [EnumMember(Value = "fast")]
    [JsonStringEnumMemberName("fast")]
    [Display(Name = "Very Fast")]
    Fast = 2,
}

/// <summary>
///   A base class whose members a configuration inherits rather than holds.
/// </summary>
/// <remarks>
///   NJsonSchema hands a schema processor an inherited property through the
///   type that declares it, so this is the shape that used to lose its
///   <c>x-uiDefinition</c> entirely.
/// </remarks>
[Section(DisplaySectionType.Minimal, DefaultSectionName = "Base")]
public class InheritedConfigurationBase
{
    /// <summary>The name of the thing.</summary>
    [Display(Name = "Inherited Name", Order = 1)]
    [RequiresRestart]
    [EnvironmentVariable("INHERITED_NAME")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The mode to run in.</summary>
    [Display(Order = 2)]
    [SectionName("Inherited")]
    public TwinMode Mode { get; set; } = TwinMode.Balanced;

    /// <summary>The endpoints to talk to.</summary>
    [Display(Order = 3)]
    [List(ListType = DisplayListType.ComplexTab)]
    public List<TwinEndpoint> Endpoints { get; set; } = [];

    /// <summary>Does an inherited thing.</summary>
    [Display(Name = "Do The Inherited Thing")]
    [CustomAction(Icon = "Play")]
    public void DoTheInheritedThingAction() { }
}

/// <summary>
///   A configuration that inherits most of its members.
/// </summary>
[Display(Name = "Inheriting")]
[Section(DisplaySectionType.Tab, DefaultSectionName = "Derived", ShowSaveAction = true)]
public class InheritingConfiguration : InheritedConfigurationBase, INewtonsoftJsonConfiguration
{
    /// <summary>Something only the derived class has.</summary>
    [Display(Name = "Derived Count", Order = 4)]
    [Badge("New", Theme = DisplayColorTheme.Primary)]
    public int Count { get; set; } = 1;
}
