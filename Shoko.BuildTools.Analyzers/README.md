# Shoko Analyzers

Compile-time checks for the classes Shoko describes as forms: configurations
(`IConfiguration`) and the parameters of executable actions
(`IExecutableAction`). Each rule reports a shape the server's schema generator
would refuse at startup, leaving the whole type without a schema and without a
UI, so the mistake is caught where it is written instead.

Every rule is an error in the `Shoko.Configuration` category. The server keeps
its own checks, so a plugin built without the analyzer still fails at startup
with the message quoted under each rule.

Only members the generator sees are checked: public instance properties that
no JSON serializer ignores, and methods carrying `[CustomAction]`,
`[ConfigurationAction]` or `[OptionsProvider]`. Any other method is the class's
own business, whatever its name or shape.

## SHOKO0001

**Property nests a collection inside a collection.**

A list of lists, a dictionary of dictionaries, a list of dictionaries or a
jagged array. The UI has no way to render one, and the generator cannot tell
the two levels apart. A dictionary of lists is fine. A `[Flags]` enum is a list
of its members, so a list of one is reported too.

```csharp
public List<List<string>> Groups { get; set; } = [];           // reported

public class Group { public List<string> Names { get; set; } = []; }
public List<Group> Groups { get; set; } = [];                  // fixed
```

Startup: `Configuration property "MyConfig.Groups" is a List<List<String>>. A
collection cannot hold another collection directly, …`

## SHOKO0002

**Dictionary key is not serializable to text.**

JSON object keys are text, so a dictionary key must be a string, an enum, a type
marked `[Serializable]` or implementing `ISerializable`. A `[Flags]` enum is
written as a list of its members, so it is no key.

```csharp
public Dictionary<Point, int> Weights { get; set; } = [];      // reported
public Dictionary<string, int> Weights { get; set; } = [];     // fixed
```

Startup: `Type "Point" is not serializable to text and therefore cannot be used
as a key in a dictionary inside a configuration.`

## SHOKO0003

**List display type is not supported for these list items.**

The complex list types (`ComplexTab`, `ComplexDropdown`, `ComplexInline`) need
entries that are classes, and `EnumCheckbox` needs entries that are enums.

```csharp
[List(ListType = DisplayListType.ComplexTab)]
public List<string> Names { get; set; } = [];                  // reported

[List(ListType = DisplayListType.ComplexTab)]
public List<NamedEntry> Names { get; set; } = [];              // fixed, with a [Key]
```

Startup: `Tab lists are not supported for non-class list items.` (and the same
for dropdown, inline and checkbox lists).

## SHOKO0004

**Complex list display type has no primary key.**

A complex list labels each entry with a `[Key]` property, declared on the entry
type itself or on the list member. An inherited `[Key]` does not count.

```csharp
public class Entry { public string Name { get; set; } = ""; }  // reported on the list

public class Entry { [Key] public string Name { get; set; } = ""; }  // fixed
```

Startup: `Tab lists must have a primary key set.` (and the same for dropdown
and inline lists).

## SHOKO0005

**Record-shaped property is not a generic dictionary.**

A non-generic dictionary such as `Hashtable` has no key or value type to read.

```csharp
public Hashtable Values { get; set; } = new();                         // reported
public Dictionary<string, string> Values { get; set; } = [];           // fixed
```

Startup: `Type Hashtable does not implement IReadOnlyDictionary<,> or
IDictionary<,>.`

## SHOKO0006

**Condition can never hold.**

A `[Visibility]` or `[CustomAction]` condition names a member the class does not
have, points through a collection, or gives its operator the wrong sort of
value: the emptiness operators take none, `In` and `NotIn` take a set, ordering
needs numbers and `Contains` needs a string or a collection.

```csharp
[Visibility(ToggleWhenMemberIsSet = nameof(Name), ToggleOperator = UiConditionOperator.GreaterThan, ToggleWhenSetTo = 3)]
public string Note { get; set; } = "";                         // reported: Name is a string

[Visibility(ToggleWhenMemberIsSet = nameof(Count), ToggleOperator = UiConditionOperator.GreaterThan, ToggleWhenSetTo = 3)]
public string Note { get; set; } = "";                         // fixed
```

Startup: `The condition on MyConfig.Note with operator 'GreaterThan' …`

## SHOKO0007

**Handler cannot react to what it names.**

A `[ConfigurationAction]` handler narrows itself with `Events` or
`ReactiveMembers` but is not a live-edit handler, or watches a member the class
does not have or one behind a collection.

```csharp
[ConfigurationAction(ConfigurationActionType.Save, ReactiveMembers = [nameof(Path)])]
public void OnSave() { }                                       // reported

[ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveMembers = [nameof(Path)])]
public void OnEdit() { }                                       // fixed
```

Startup: `MyConfig.OnSave handles Save, which is not raised by an event, so it
cannot narrow what it reacts to.`

## SHOKO0008

**Options provider method cannot be used.**

An `[OptionsProvider]` method is not public, is generic, or returns no
collection of options.

```csharp
[OptionsProvider(nameof(Port))]
private int[] ListPorts() => [80, 443];                        // reported
public int[] ListPorts() => [80, 443];                         // fixed
```

Startup: `The options provider 'MyConfig.ListPorts' is not public.`

## SHOKO0009

**Options provider names no usable member.**

The attribute names no member, a member that is not a public instance property
of the class, or one member twice.

```csharp
[OptionsProvider("Prot")]                                      // reported
[OptionsProvider(nameof(Port))]                                // fixed
```

Startup: `The options provider 'MyConfig.ListPorts' names "Prot", which MyConfig
does not have.`

## SHOKO0010

**Member cannot take options.**

The named part of a member has nothing to choose: `Keys` on a member that is
not a dictionary, a select component, a dictionary of dictionaries, or a
dictionary without key and value types.

```csharp
[OptionsProvider(nameof(Name), Target = OptionsTarget.Keys)]   // reported: Name is a string
[OptionsProvider(nameof(Name))]                                // fixed
```

Startup: `The options provider 'MyConfig.ListNames' names "Name", which is not a
dictionary, so it has no keys.`

## SHOKO0011

**Option type is not allowed.**

Options must be told apart and shown as text: a primitive, a string, a decimal,
an enum, a `Guid`, a date, time or time span, a `Uri`, or any type implementing
`IParsable<TSelf>` of itself or carrying `[TypeConverter]`. The startup check
also makes sure that converter converts to and from a string or a primitive.

```csharp
public Library Library { get; set; } = new();
[OptionsProvider(nameof(Library))]
public Library[] ListLibraries() => [];                        // reported

public int LibraryId { get; set; }
[OptionsProvider(nameof(LibraryId))]
public SelectOption<int>[] ListLibraries() => [];              // fixed
```

Startup: `The options provider 'MyConfig.ListLibraries' names "Library", whose
options would be Library, which is neither a primitive nor convertible to and
from one.`

## SHOKO0012

**Options do not match their members.**

The members one provider names take different option types, or the method lists
another type than they take. No conversion bridges the two: `long` options for
an `int` member are refused.

```csharp
public int Port { get; set; }
[OptionsProvider(nameof(Port))]
public long[] ListPorts() => [80];                             // reported
public int[] ListPorts() => [80];                              // fixed
```

Startup: `The options provider 'MyConfig.ListPorts' returns Int64[] rather than
a collection of Int32.`

## SHOKO0013

**Member's options are provided twice.**

Two providers name the same part of a member. A dictionary's keys and values
are separate parts, so each may have one.

```csharp
[OptionsProvider(nameof(Port))] public int[] ListPorts() => [80];
[OptionsProvider(nameof(Port))] public int[] ListMore() => [443];   // reported

[OptionsProvider(nameof(Port))] public int[] ListPorts() => [80, 443];  // fixed
```

Startup: `The options provider 'MyConfig.ListMore' names "Port", whose values
ListPorts already provides for.`

## SHOKO0014

**Flags enum has no single-bit members.**

A `[Flags]` enum is rendered as a list of its members with a single bit set.
Zero and combined members are only accepted on read, so an enum with none of
the first kind has nothing to list.

```csharp
[Flags] public enum Mode { None = 0, All = 3 }
public Mode Mode { get; set; }                                 // reported

[Flags] public enum Mode { None = 0, Read = 1, Write = 2, All = Read | Write }  // fixed
```

Startup: `Configuration property "MyConfig.Mode" holds the flags enum Mode,
which has no member with a single bit set. …`

## SHOKO0015

**Options key parameter does not fit.**

A parameter marked `[OptionsKey]` receives the key of the dictionary entry the
options are asked for. Only a provider of a dictionary's values has one to
hand, the parameter's type is the dictionary's key type exactly, and a provider
takes one key at most.

```csharp
public Dictionary<string, Mode> ModeByPlugin { get; set; } = [];
[OptionsProvider(nameof(ModeByPlugin))]
public Mode[] ListModes([OptionsKey] Guid plugin) => [];       // reported
public Mode[] ListModes([OptionsKey] string plugin) => [];     // fixed
```

Startup: `The options provider 'MyConfig.ListModes' takes a key of Guid, but
"ModeByPlugin" is keyed by String.`

## SHOKO0016

**Section toggle does not fit.**

A section drawn as a checkbox names the `bool` that turns it on with
`ToggleMember`, a public, serialised member of the class itself. The client draws
that member as the section's own checkbox. No other section type names one.

```csharp
[Section(DisplaySectionType.Checkbox)]                         // reported
public class Experimental { public bool Enabled { get; set; } }

[Section(DisplaySectionType.Checkbox, ToggleMember = nameof(Enabled))]
public class Experimental { public bool Enabled { get; set; } } // fixed
```

Startup: `Section "Experimental" is drawn as a checkbox but names no
ToggleMember, the bool member that turns it on.`
