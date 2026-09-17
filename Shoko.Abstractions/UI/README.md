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
GET /api/v3/Configuration/{configID}/UiDefinition
GET /api/v3/Action/{actionID}/UiDefinition
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
  titled group — a *floating section* — entered at the position of the first
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
`[FloatingSection]` describes still renders — without a description.

`DisplaySectionType` picks how a group is drawn — `Tab`, `FieldSet`, `Minimal`,
`Checkbox`. Laid out as tabs, a member with no section name has nowhere to live,
so those members are gathered into the default section; give it a name with
`DefaultSectionName`. In every other layout they simply stay where they are.

### Buttons

A `[CustomAction]` method becomes a button. Where it lands follows the same
rules as a field, with one addition:

| Authored as | Renders |
|---|---|
| `Position = Auto` | Inline, in the order it was declared, among the fields |
| `Position = Start` / `End` | Pinned to the top or bottom of whatever holds it |
| `AttachToMember = nameof(Path)` | On that member's row, with `Position` choosing the edge |

An attached action leaves the container's own order entirely: it appears in the
element's `AttachedStartActions` or `AttachedEndActions` and nowhere else, so it
is described once and drawn once.

---

## Elements

Every element carries `Kind`, a label, a description, its size, its visibility
and whatever constraints apply to it.

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

A condition names a member relative to the class it is declared in, and compares
for equality. `null` is a legal value to compare against, and
`InverseToggleCondition` flips the outcome. `DisableWhenMemberIsSet` does the
same for the enabled state instead of the visible one.

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
edits. Only `LiveEdit` is raised by an event, and it can name which one it wants
with `ReactiveEventType`; a handler that names none takes them all.

So that a client knows when it is worth posting the document, every container
says whether it reacts:

- **`HasLiveEdit`** — this class handles live edits.
- **`HasNestedLiveEdit`** — something below it does.

With both unset, nothing under that branch reacts, and a client editing there
posts nothing. A live edit returns JSON Patch operations against the document
that was posted, not a whole configuration.

---

## Pattern: a choice only the server can enumerate

A dropdown whose options come from the machine — folders on disk, users in the
database, devices a probe found — should not persist those options. Store the
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
    ToggleWhenSetTo = null,
    ToggleVisibilityTo = DisplayVisibility.Hidden,
    InverseToggleCondition = true
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
  form is ever drawn, and no live edit is needed.

Guard the handler on state rather than on which member changed, as above. It is
then safe to run for any event.

## Pattern: refreshing a field in place

An action attached to a member renders on that member's row, which suits
anything that acts on the field next to it.

```csharp
[Display(Name = "Refresh")]
[CustomAction(Icon = "Magnify", AttachToMember = nameof(Path), Position = DisplayButtonPosition.End)]
public async Task<ConfigurationActionResult> RefreshPaths(ConfigurationActionContext<MyConfiguration> context, IBrowser browser)
{
    PathSelector ??= new();
    PathSelector.Options = (await browser.List(Path)).Select(x => new SelectOption<string>(x)).ToList();
    return new(context.Configuration);
}
```

## Pattern: rows that identify themselves

A list of classes renders as a list of rows, and each row needs a label. Mark
the member that identifies a row with `[Key]`: it becomes the list's
`ItemTitlePath`, and the client labels the row from that value instead of
guessing which member to show. Pick something the user can read — the value is
what they will see in the tab, the header or the picker.

```csharp
[List(ListType = DisplayListType.ComplexTab)]
public List<EndpointConfiguration> Endpoints { get; set; } = [];

public class EndpointConfiguration
{
    [Key]
    [Display(Order = 1)]
    public string Name { get; set; } = string.Empty;

    [Display(Order = 2), Url]
    public string Url { get; set; } = string.Empty;
}
```

`DisplayListType` decides the shape: `ComplexInline` stacks the rows,
`ComplexTab` gives each a tab, `ComplexDropdown` folds them behind a picker, and
`EnumCheckbox` renders a list of enum values as checkboxes.
