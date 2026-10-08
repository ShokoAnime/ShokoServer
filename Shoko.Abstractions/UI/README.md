# Form UI

A configuration class, or an executable action's parameters, is rendered as a
form by whatever client the user happens to be using. This folder holds both
halves of that: the attributes you author with, and `UiDefinition`, the
render-ready description the server derives from them and serves to clients.

The JSON schema is still generated, and it remains the authority for
*validation*. `UiDefinition` is the authority for *rendering*: `$ref`s are
resolved, labels are worked out, constraints sit on the element that needs them,
and every element carries a concrete `Kind`. A client never has to interpret the
schema to draw a form.

```
GET  /api/v3/Configuration/{configID}/UiDefinition
GET  /api/v3/Action/{actionID}/UiDefinition
POST /api/v3/Configuration/{configID}/Options?path=…
POST /api/v3/Action/{actionID}/Options?path=…
```

---

## What comes back

```jsonc
{
  "ID": "19c7a5e2-…",
  "Name": "My Plugin",
  "Root": {
    "Kind": "section-container",
    "SectionType": "field-set",
    "ShowSaveAction": true,
    "Items":    { "ApiToken": { "Kind": "password", … }, "Library": { "Kind": "section-container", … } },
    "Actions":  { "TestToken": { "Name": "TestToken", "Title": "Test", … } },
    "FloatingSections": {
      "Login": { "Title": "Login", "StartActions": ["TestToken"], "EndActions": [],
                 "Structure": [ { "Kind": "item", "Name": "ApiToken" } ] }
    },
    "StartActions": [], "EndActions": [],
    "Structure": [
      { "Kind": "floating-section", "Name": "Login" },
      { "Kind": "item", "Name": "Library" }
    ]
  },
  "Definitions": {}
}
```

A container files its members in three maps and puts them back in order with
`Structure`. Each entry names the map to look it up in, so a client indexes
rather than searches. `Definitions` is empty unless a configuration recurses, in
which case the cycle is hoisted there and pointed at by a `reference` element.

---

## Layout

Members render in the order you declared them. Nothing is reordered behind your
back, so a class can go field, field, nested class, field, and that is what the
user sees.

Two things change that, and both are yours to ask for:

- **`[SectionName("Login")]`** gathers every member carrying that name into one
  titled group, a *floating section*, entered at the position of the first
  member in it. A later member with the same name joins that group rather than
  opening a second one.
- **`[Section(..., AppendFloatingSectionsAtEnd = true)]`** moves every gathered
  group past the members that were left in place.

A member that is a class of its own stays an ordinary item and renders its own
heading from its own label. It is never swept into a group, which is why a
tabbed configuration gets one tab per nested class without you naming anything.

```csharp
[Section(DisplaySectionType.Tab, DefaultSectionName = "Misc.", AppendFloatingSectionsAtEnd = true)]
public class MyConfiguration : IConfiguration
{
    public LibraryConfiguration Library { get; set; } = new();   // its own tab
    public NetworkConfiguration Network { get; set; } = new();   // its own tab

    [SectionName("Login")] public string? Username { get; set; } // "Login" tab
    [SectionName("Login")] public string? Password { get; set; }

    public bool Debug { get; set; }                              // "Misc." tab
}
```

A gathered section has no type of its own, so the class is where it is
described:

```csharp
[Section(DisplaySectionType.Tab, DefaultSectionName = "Misc.")]
[FloatingSection("Login", Description = "Credentials for the service.")]
public class MyConfiguration : IConfiguration { … }
```

The member still names the section it belongs to; this only describes the
section it names. A name no member uses leaves nothing behind, and a section no
`[FloatingSection]` describes still renders, without a description.

`DisplaySectionType` picks how a group is drawn: `Tab`, `FieldSet`, `Minimal`,
`Checkbox`. Laid out as tabs, a member with no section name has nowhere to live,
so those members are gathered into the default section; give it a name with
`DefaultSectionName`. In every other layout they simply stay where they are.

### Buttons

A `[CustomAction]` method becomes a button. Where it lands follows the same
rules as a field:

| Authored as | Renders |
|---|---|
| `Position = Auto` | Inline, in the order it was declared, among the fields |
| `Position = Start` / `End` | Pinned to the top or bottom of whatever holds it |

---

## Elements

Every element carries `Kind`, a label, a description, its size, its visibility
and whatever constraints apply to it.

`[DeniedValues]` is filed on the element that can act on it. On a list or a
dictionary it describes an entry, so it lands on the item rather than on the
collection, and an element with no value of its own to match, a nested class or a
select whose options live in the configuration value, carries none.

| `Kind` | Authored as |
|---|---|
| `boolean`, `integer`, `float`, `string` | The property's own type |
| `password` | `[PasswordPropertyText]` or `[DataType(DataType.Password)]` |
| `text-area`, `code-editor` | `[TextArea]`, `[CodeEditor(CodeEditorLanguage.Json)]` |
| `enum` | An `enum`, with its members and aliases resolved |
| `select` | `SelectComponent<T>` |
| `list`, `record` | A collection or a dictionary, shaped by `[List]` / `[Record]` |
| `section-container` | A nested class |
| `reference` | A type that recursed, pointing into `Definitions` |

---

## Showing and hiding

`[Visibility]` states the element's resting visibility and, optionally, one
condition that switches it:

```csharp
[Visibility(
    DisplayVisibility.Hidden,
    ToggleWhenMemberIsSet = nameof(Mode),
    ToggleWhenSetTo = LibraryMode.Remote,
    ToggleVisibilityTo = DisplayVisibility.Visible
)]
public string? RemoteUrl { get; set; }
```

A condition names a member relative to the class it is declared in, and
`DisableWhenMemberIsSet` does the same for the enabled state instead of the
visible one. `ToggleOperator` and `DisableOperator` pick how the comparison is
made; it is equality by default, and `null` is a legal value to compare against.

| Operator | Takes | For |
|---|---|---|
| `Equals`, `NotEquals` | `…WhenSetTo` | One value, the everyday case |
| `IsEmpty`, `IsNotEmpty` | nothing | A cleared field, which holds `""` rather than `null` |
| `In`, `NotIn` | `…WhenSetToAny` | A set, instead of one property per value |
| `GreaterThan`, `LessThan` | a numeric `…WhenSetTo` | Numbers |
| `Contains` | `…WhenSetTo` | A substring, or an entry of a collection |

```csharp
[Visibility(DisableWhenMemberIsSet = nameof(FfmpegPath), DisableOperator = UiConditionOperator.IsEmpty)]
public string Encoder { get; set; } = string.Empty;

[Visibility(DisplayVisibility.Hidden,
    ToggleWhenMemberIsSet = nameof(Acceleration),
    ToggleOperator = UiConditionOperator.In,
    ToggleWhenSetToAny = new object?[] { HardwareAcceleration.Vaapi, HardwareAcceleration.Qsv },
    ToggleVisibilityTo = DisplayVisibility.Visible)]
public string RenderDevice { get; set; } = string.Empty;
```

A path may descend into a nested class, which `nameof` spells with an
interpolated string. That is still a constant, so it survives a rename:

```csharp
[Visibility(
    DisableWhenMemberIsSet = $"{nameof(Nested)}.{nameof(NestedConfiguration.Mode)}",
    DisableOperator = UiConditionOperator.NotEquals,
    DisableWhenSetTo = NestedMode.Local
)]
public string LocalPath { get; set; } = string.Empty;
```

A condition that could never hold fails when the configuration is described,
rather than rendering an element stuck in the state its author did not intend:
an operator handed the wrong sort of value, a numeric comparison against text, a
path naming a member the class does not have, or a path pointing through a list
or dictionary, which has no index to say which item it meant.

Conditions cannot reach outside the object they are declared in. Inside a list
item or a dictionary value the condition resolves against that one item, which
is what makes a rule like "disable this row's button while this row's id is
unset" work. The definition describes a type, not an instance, so it cannot
know where any given instance sits in the document.

For the same reason nothing in the definition carries a path to itself. A client
tracks the position of the container it descended into, and sends that path back
when it invokes an action.

---

## Reacting while the user edits

Besides `[CustomAction]`, a class can declare lifecycle hooks with
`[ConfigurationAction(ConfigurationActionType...)]`:

| Hook | Runs |
|---|---|
| `New` | When a fresh instance is created |
| `Load` | When the configuration is fetched for editing |
| `Save` | When it is saved |
| `Validate` | When it is validated |
| `LiveEdit` | While the user is editing, before anything is saved |

A hook is declared on the class it belongs to, so a nested class handles its own
edits. Only `LiveEdit` is raised by an event, and it can name the ones it wants
with `Events`; a handler that names none takes them all.

Every live-edit handler the edited path passes through runs, innermost first,
and only those whose declared events and `ReactiveMembers` cover the edit. Each
is handed what the one before it returned, on the context and as an injectable
parameter, so a handler can build on it; returning something else costs nothing,
since the messages and validation errors of every handler are collected either
way and they all hold the same configuration instance. A path that does not
resolve, which is what a row still being composed client-side looks like, is not
an error for a live edit: nothing reacts. An explicitly invoked action still
fails, because somebody pressed a button on something that is not there.

A hook owns its side of the job, which is the point of declaring one:

- **`Load`** is handed a fresh instance, not the stored document. Build on the
  saved one by loading it yourself and returning what you want the user to edit.
- **`Save`** is handed what the user submitted, and nothing is written unless it
  writes it. This is the place to drop anything that should not reach disk.

So that a client knows when it is worth posting the document, every container
says whether it reacts:

- **`HasLiveEdit`**: this class handles live edits.
- **`HasNestedLiveEdit`**: something below it does.

With both unset, nothing under that branch reacts, and a client editing there
posts nothing. A live edit returns JSON Patch operations against the document
that was posted, not a whole configuration.

A handler can narrow that to the events and the members it actually cares about,
which reaches the element as `ReactsToLiveEdit`: the events an edit to it is
worth sending on, empty when nothing watches it:

```csharp
[ConfigurationAction(ConfigurationActionType.LiveEdit,
    Events = [ReactiveEventType.Unfocused, ReactiveEventType.Edited],
    ReactiveMembers = [nameof(FfmpegPath), $"{nameof(Nested)}.{nameof(NestedConfiguration.Mode)}"])]
public ConfigurationActionResult OnEdit(ConfigurationActionContext<MyConfiguration> context) { … }
```

A request carries one event and a handler is interested in as many as it likes,
so `Events` is the plural side of the same enum: name none and the handler takes
them all, which reaches the element as `All`. Dispatch prefers the handler that
named the raised event, and falls back to one that named none.

`ReactiveMembers` may descend into a nested class. Leave it off and the handler
watches everything in its own class, and everything below it that has no handler
of its own. A member a handler names but the class does not have,
a name pointing through a list or dictionary, or either property on a hook that
no event raises, fails when the configuration is described.

---

## Options the server lists

`[OptionsProvider(nameof(A), nameof(B))]` on a method turns the members it names
into choices from values the server lists when asked. A method without the
attribute is never checked, whatever its name or shape, so a model keeps any
other methods it likes.

```csharp
public int LibraryId { get; set; }

public List<int> ExtraLibraryIds { get; set; } = [];

public Dictionary<string, TestMode> ModeByFolder { get; set; } = [];

[OptionsProvider(nameof(LibraryId), nameof(ExtraLibraryIds))]
public async Task<IReadOnlyList<SelectOption<int>>> ListLibraries(ILibraryService libraries)
    => (await libraries.GetAll()).Select(x => new SelectOption<int>(x.ID, x.Name)).ToList();

[OptionsProvider(nameof(ModeByFolder), Target = OptionsTarget.Keys)]
public string[] ListFolders(IFolderService folders)
    => folders.GetNames();
```

### The method

It is public, static or not, and not generic. It returns an array or any
`IEnumerable<T>` of the option type, or of `SelectOption<T>` of it when each
option wants a label, directly or through a `Task` or a `ValueTask`. A nullable
`T?` is fine too; its nulls are left out.

Its parameters are filled in the way a custom action's are: the configuration
being edited, unsaved changes included, the user and any registered service. On
an executable action the provider runs on an instance prepared the way invoking
it prepares one, scoped to its entity, given its caller and populated with the
parameters entered so far, and may take the entity, the caller and services too.

### Which part of a member

`Target` picks the part the options are for:

| Member | `OptionsTarget.Values` (default) | `OptionsTarget.Keys` |
|---|---|---|
| A scalar, `T` or `T?` | the value itself | refused |
| A list, `List<T>` or `T[]` | each entry | refused |
| A dictionary, `Dictionary<K, V>` | each value, or each entry of a list value | each key |

A select component carries its own options and takes none, and so does a
dictionary of dictionaries. Each part of a member has one provider at most, so
a dictionary's keys and its values may each have their own.

### The option type

Every member one provider names takes the same option type for its target, and
the method lists exactly that type. There is no bridging: a provider listing
`long` for an `int` member, or strings for a member of a type that converts from
a string, is refused.

An option type is one a client can tell apart and show as text:

- a primitive (`bool`, `char`, the integer and floating types), `string`,
  `decimal` or an enum;
- `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` or
  `Uri`;
- any type of any origin, a plugin's own included, that implements
  `IParsable<TSelf>` of itself, or carries `[TypeConverter]` naming a
  converter that converts it to and from a string or one of the primitives.

A class, record or struct with neither, or a collection, is refused. A member of
a type parsable from text or converted by a `[TypeConverter]` is described as
text in the form, so give it a JSON converter that writes it as text too.

### What comes back

The element that renders the choice carries an `OptionsRoute`: a scalar member
itself, a list's `Item`, a dictionary's `Item` (or the item of a list value) for
values, and its `KeyItem` for keys, whose route ends in `/Keys`. A client POSTs
the edited document to it with the member's path as `path`, the same path a
custom action is invoked with. The path is always the member's own, so an entry
or a key needs no index of its own. A scoped action's route holds the entity's
placeholder, such as `{seriesID}`, for the client to fill in.

```
POST /api/v3/Configuration/{configID}/Options[/Keys]?path=…
POST /api/v3/Action/{actionID}/Options[/Keys]?path=…
POST /api/v3/{Group|Series|Episode|File}/{id}/Action/{actionID}/Options[/Keys]?path=…
```

The answer is a list of `{ "Value": …, "Label": "…" }`:

- `Value` is serialised the way the member itself is.
- `Label` is always set: the provider's own when it gave one through
  `SelectOption<T>`, otherwise the value as text, using the type converter's
  text when it has one and invariant formatting otherwise. An enum is labelled
  with its name.
- The order is the provider's, duplicates included; nulls are left out.
- An empty list means there is nothing to choose from right now.

A provider may refuse the draft by throwing `GenericValidationException`, for
example when the credentials it needs are wrong. The route then answers with a
validation problem, its errors keyed by member path as thrown. Any other
exception is a server error.

### Checks

A provider that does not fit fails startup, and the analyzer reports the same
mistakes at compile time as SHOKO0008 to SHOKO0013; see
[the analyzer rules](../../Shoko.BuildTools.Analyzers/README.md).

## Pattern: a choice only the server can enumerate

When the stored value can simply be the chosen option, `[OptionsProvider]` above
is all it takes. The pattern below is for a choice that needs more than that.

A dropdown whose options come from the machine (folders on disk, users in the
database, devices a probe found) should not persist those options. Store the
*choice* in a property of its own, and treat the selector as scratch space that
exists only while a choice is being made.

```csharp
/// <summary>The chosen library. This is the value that persists.</summary>
[Visibility(DisplayVisibility.Hidden)]
public int LibraryId { get; set; }

/// <summary>Shown once something is chosen.</summary>
[Display(Name = "Library")]
[Visibility(
    DisplayVisibility.ReadOnly,
    ToggleWhenMemberIsSet = nameof(LibraryId),
    ToggleWhenSetTo = 0,
    ToggleVisibilityTo = DisplayVisibility.Hidden
)]
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public string? LibraryName { get; set; }

/// <summary>Shown while nothing is chosen. Never persisted.</summary>
[Display(Name = "Library")]
[Visibility(
    DisplayVisibility.Visible,
    ToggleWhenMemberIsSet = nameof(LibrarySelector),
    ToggleOperator = UiConditionOperator.IsNotEmpty,
    ToggleVisibilityTo = DisplayVisibility.Hidden
)]
public SelectComponent<int>? LibrarySelector { get; set; }

[ConfigurationAction(ConfigurationActionType.LiveEdit)]
public ConfigurationActionResult OnEdit(ConfigurationActionContext<MyConfiguration> context, ILibraryService libraries)
{
    if (LibraryId is 0 && LibrarySelector is null)
        LibrarySelector = new(libraries.GetAll().Select(x => new SelectOption<int>(x.ID, x.Name)).ToList());
    else if (LibraryId is not 0 && LibrarySelector is not null)
        LibrarySelector = null;
    return new(context.Configuration);
}

[CustomAction(Theme = DisplayColorTheme.Primary, ToggleWhenMemberIsSet = nameof(LibraryId), ToggleWhenSetTo = 0)]
public ConfigurationActionResult Choose(ConfigurationActionContext<MyConfiguration> context, ILibraryService libraries)
{
    if (LibrarySelector?.SelectedValue is not { } selected)
        return new();

    LibraryId = selected;
    LibraryName = libraries.GetById(selected)?.Name;
    LibrarySelector = null;
    return new(context.Configuration);
}
```

What each piece buys:

- **The scalar persists, the options do not.** `settings-*.json` holds an id, not
  a snapshot of whatever the machine reported the last time it was asked.
- **The choice survives its option disappearing.** The id is still there when the
  library is gone, and the read-only mirror is what tells the user what is stored.
- **`NullValueHandling.Ignore` keeps the document clean**, so a nulled selector
  leaves no trace rather than persisting a null.
- **Populate on `Load` instead** if the options are cheap and always wanted: the
  same body under `ConfigurationActionType.Load` fills the selector before the
  form is ever drawn, and no live edit is needed. Remember that the handler
  starts from a fresh instance, so load the stored document yourself first.
- **Or strip it on `Save`** if the selector is not nullable, or you would rather
  not think about when it is populated: clear it in a
  `ConfigurationActionType.Save` handler, then write the document. Nothing is
  persisted but what that handler writes.

Guard the handler on state rather than on which member changed, as above. It is
then safe to run for any event.

## Pattern: rows that identify themselves

A list of classes renders as a list of rows, and each row needs a label. The
clearest way to give it one is a `TitleComponent`, which says what the row calls
itself rather than leaving it to be inferred:

```csharp
[List(ListType = DisplayListType.ComplexTab)]
public List<EndpointConfiguration> Endpoints { get; set; } = [];

public class EndpointConfiguration
{
    /// <summary>What this row calls itself.</summary>
    public TitleComponent Name { get; set; } = new();

    /// <summary>What tells two rows apart.</summary>
    [Key, Url]
    public string Url { get; set; } = string.Empty;
}
```

The list points at it, `ItemTitlePath` at the component's `Title` and
`ItemCategoryPath` at its `SubTitle`, and a record's values are labelled the same
way. Failing that the member marked `[Key]` is used, and a record entry with
neither is labelled by the key it is stored under. Pick something the user can
read: the value is what they will see in the tab, the header or the picker.

The title only labels a row. A tabbed, inline or dropdown list still needs a
member marked `[Key]` to tell its rows apart, and fails to describe without one.

`DisplayListType` decides the shape: `Flat` is one row per entry, `ComplexInline`
stacks them, `ComplexTab` gives each a tab, `ComplexDropdown` folds them behind a
picker, and `EnumCheckbox` renders a list of enum values as checkboxes.
`DisplayRecordType` is the same for a dictionary, without the inline and checkbox
variants.

Leave the layout unset and the server settles it before serving: an entry that is
a class of its own is laid out one per entry, anything else is a plain row, and a
select follows whether it takes more than one value. `Auto` never reaches a
client, so a renderer switches on the answer rather than working it out again. A
list of enums stays flat, since rendering those as checkboxes is a choice worth
making with `EnumCheckbox` rather than inferring.
