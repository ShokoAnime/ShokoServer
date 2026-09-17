# Configuration

Shoko has **no `appsettings.json`**. Configuration is code-first: you declare a
plain C# class, and the server derives a JSON schema from it, persists it, loads
it, validates it, and renders a settings page for it in the WebUI. A plugin gets
a full settings page without writing any UI.

`IConfigurationService`, in this folder, is the machinery behind that. Most
plugins only use `ConfigurationProvider<TConfig>` (in `Config/`), which wraps
the service for one configuration type.

---

## The shortest thing that works

Declare a class implementing `IConfiguration`, or one of its marker
sub-interfaces:

```csharp
[Display(Name = "My Plugin")]
public class MyConfiguration : IConfiguration
{
    [Display(Name = "API Token", Description = "Your personal API token.")]
    [DataType(DataType.Password)]
    public string? ApiToken { get; set; }

    [Display(Name = "Sweep Interval (Hours)")]
    [Range(1, 24 * 30)]
    [DefaultValue(24)]
    public int SweepIntervalHours { get; set; } = 24;
}
```

Then take a `ConfigurationProvider<MyConfiguration>` in any constructor:

```csharp
public class MyProvider(ConfigurationProvider<MyConfiguration> configurationProvider)
{
    public async Task DoSomething()
    {
        var config = configurationProvider.Load();
        if (string.IsNullOrWhiteSpace(config.ApiToken))
            return;
        // ...
    }
}
```

There is nothing to register:

- **Discovery.** Every exported type implementing `IConfiguration` is handed to
  the configuration service (`GetTypes<T>()`, not `GetExports<T>()`), which
  instantiates configurations itself. The singleton rule in
  [the main README](../../README.md#the-three-branch-registration-rule) does not
  apply, and registering a configuration class in DI does nothing useful.
- **The provider.** `ConfigurationProvider<>` is an open generic singleton, so
  it resolves for any configuration type.

---

## `ConfigurationProvider<TConfig>`

| Member | What it does |
|---|---|
| `Load(copy = false)` | The current in-memory instance, loading it from disk (or creating it from defaults) on first call. Throws `ConfigurationValidationException` when validation fails. |
| `Load(copy: true)` | A deep copy, safe to mutate without touching what everyone else sees. |
| `New()` | A fresh instance from defaults. Not saved, not cached. |
| `Save()` | Persists the current in-memory instance. Returns `false` when nothing changed. |
| `Save(config)` | Persists this instance and makes it the current one. |
| `Validate(config)` | Validation errors keyed by property path, without saving. |
| `ConfigurationInfo` | Metadata: `ID`, `Path` on disk, `Name`, `Description`, `IsHidden`, `IsBase`. |
| `Saved` | Fires when *your* configuration is saved, carrying the reloaded instance. The provider already filters out every other configuration's saves. |
| `PerformCustomAction` / `PerformReactiveAction` | Invokes a `[CustomAction]` or `[ConfigurationAction]` method programmatically. The WebUI is the usual caller. |

**Call `Load()` at the point of use, not in your constructor**, or your plugin
keeps running on the values it had at startup. After the first call it returns
the instance held in memory.

To react to a change, subscribe to `Saved` rather than polling:

```csharp
public MyProvider(ConfigurationProvider<MyConfiguration> configurationProvider)
{
    configurationProvider.Saved += (_, e) => _client.ApplyToken(e.Configuration.ApiToken);
}
```

The saved event args also carry `ChangedPaths`, the JSON paths whose stored
values the save changed, added or removed (`Web.Port`, `Import.Exclude[2]`,
every path on the first save), and `Actor`, the API token of whoever saved it
or `null` for the system. `ChangedPaths` never holds a value, secrets
included, so it is safe to log or store.

`Saved` and `RequiresRestart` are raised on a thread-pool task after the save
returns. Nothing observes that task, so an exception your handler throws is
lost without a log line: catch and log inside the handler. Nor can you rely on
a handler having run by the time `Save` returns.

---

## The `[Required]` trap

> **Do not put `[Required]` on a property the user has to fill in themselves.**

`[Required]` becomes `required` in the generated JSON schema, and schema
validation runs inside `Load()`. An unfilled `[Required]` property therefore
makes **every** `Load()` throw `ConfigurationValidationException`, from install
until the user saves a value, which can keep the plugin from loading at all.
Check the value where it is used instead:

```csharp
// The property itself makes no demands.
[DataType(DataType.Password)]
[Display(Name = "App Token", Description = "Register an app to get one.")]
public string? AppToken { get; set; }
```

```csharp
// The check lives where the value is used.
if (string.IsNullOrWhiteSpace(_configurationProvider.Load().AppToken))
{
    _logger.LogDebug("Skipping refresh for anime {AnimeID}: no app token configured.", anime.AnidbID);
    return false;
}
```

The plugin then loads, shows its settings page, and does nothing until it is
configured.

`[Required]` is still fine on a member with a non-null default, and on the item
type behind a `[List]` or `[Record]` editor, where it applies to each item the
user adds. Constraints the default already satisfies, such as `[Range]` and
`[MinLength]`, are safe for the same reason.

---

## Marker interfaces

Everything derives from `IConfiguration`. The sub-interfaces change how the
configuration is treated:

| Interface | Effect |
|---|---|
| `INewtonsoftJsonConfiguration` | Serialise with Newtonsoft.Json rather than the default `System.Text.Json`. Reach for it when your type relies on Newtonsoft attributes or converters. |
| `IHiddenConfiguration` | Hide the configuration from every UI. |
| `IBaseConfiguration` | A shared base other configurations build on. It has no path, is never loaded or saved on its own, and `Load` on it returns a fresh instance. |
| `IConfigurationWithMigrations` | Add a `static string ApplyMigrations(string config, IApplicationPaths paths)`, run on the raw JSON before it is validated and deserialised. This is how you rename or reshape a property without breaking existing installs. |
| `IConfigurationWithNewFactory<TConfig>` | Add a `static TConfig New(IConfigurationService, IPluginManager)` for defaults that have to be computed rather than declared. |
| `IConfigurationWithCustomValidation<TConfig>` | Add a `static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(...)`, run after schema validation passes. Rules the schema cannot express go here. |

The interfaces that tie a configuration to an extension point all imply
`IHiddenConfiguration`, since the page is rendered next to the provider:
`IHashProviderConfiguration`, `IReleaseInfoProviderConfiguration`,
`IRelocationProviderConfiguration` (also a base configuration),
`IVideoStreamTransformConfiguration` and `IAiringScheduleProviderConfiguration`.
A provider names its configuration type through the generic form of its own
contract, such as `IReleaseInfoProvider<TConfiguration>`.

The static hooks are `static abstract` interface members, declared as
`public static` methods on the configuration class.

---

## Where it is stored

By default, at
`<IApplicationPaths.ConfigurationsPath>/<plugin id>/<display-name>.json`, where
the display name is lowercased with spaces replaced by dashes. `[StorageLocation]`
changes that:

| Property | Effect |
|---|---|
| `FileName` | A different file name inside the plugin's own configuration folder. `.json` is appended if you leave it off. |
| `RelativePath` | A path relative to `IApplicationPaths.DataPath` instead, escaping the plugin folder. This is how `ServerSettings` lands on `settings-server.json`. |
| `InMemoryOnly` | Never persisted. The configuration resets on every restart. |

Uninstalling a plugin with "purge configuration" deletes its folder under
`ConfigurationsPath`, and any file the plugin placed outside it via
`ConfigurationInfo.Path`.

---

## Secrets

A property marked `[DataType(DataType.Password)]` or `[PasswordPropertyText]` is
a secret. The settings page renders it as a password field, and the REST API
never sends its value. A secret that holds something goes out masked, as
`***SECRET-UNCHANGED:<fingerprint>***`, while one that is `null` or empty goes
out as it is, so "nothing set" still looks different from "withheld". The
fingerprint is a keyed hash under a random key kept in the data directory, in
its own file rather than the settings, so it says nothing about the value
beyond whether two secrets are equal. Use `ConfigurationSecrets.IsMasked` to
recognise a masked value rather than comparing strings.

When a document comes back in, the stored value is put back wherever a masked
value arrived or the property was left out. Any other value replaces it, and an
explicit `null` or empty string clears it. `Save(info, json)` does this itself,
so you only need to know about it in three cases:

- **Your own code reads the object graph, not JSON.** `Load()` always returns
  the real values. Masking belongs to the wire, never to the model.
- **You hand a configuration to a client of your own.** Serialize it with
  `IConfigurationService.SerializeWithMasking(config)`, or run an existing
  document through `MaskSecrets(info, json)`, so the credentials are masked
  exactly as the REST API masks them. If a document you masked comes back and
  you do something with it other than saving (validating it, running a custom
  action on it), call `RestoreMaskedSecrets(info, json)` first.
- **A secret sits inside a list.** Nothing on a list element reliably says
  which stored element it came from: the position shifts on every insert,
  delete or reorder, and a label can repeat or be renamed. So inside a list the
  masked value is matched by its fingerprint against every secret stored for
  the same property, wherever that element now sits. Send back what you were
  given unchanged. A masked value whose fingerprint matches nothing stored, or
  the bare `ConfigurationSecrets.Sentinel` inside a list, is rejected with
  `ConfigurationValidationException`.

The same exception is thrown when a masked value arrives for a secret that has
nothing stored behind it.

---

## Shaping the settings page

The WebUI renders from the generated schema, so standard
`System.ComponentModel.DataAnnotations` attributes do most of the work:
`[Display(Name, Description)]`, `[DefaultValue]`, `[Range]`,
`[DataType(DataType.Password)]`, `[PasswordPropertyText]`, `[Description]`.

`UI/Attributes/` adds the Shoko-specific ones, and
[`UI/README.md`](../../UI/README.md) covers how a form is laid out, how it
reacts while the user edits, and the patterns worth copying:

| Attribute | On | What it does |
|---|---|---|
| `[Section(DisplaySectionType)]` | Class | Picks the section style, names the default section, controls whether a save button is shown. |
| `[CustomAction(AttachToMember = ...)]` | Method | Renders the button on that member's row rather than among the class's own members. |
| `[SectionName("Login")]` | Member | Groups members into a named section. |
| `[FloatingSection("Login", Description = ...)]` | Class | Describes a section members are grouped into. |
| `[Visibility]` | Member | Hides a member, marks it advanced, or sets its size. |
| `[Badge("Advanced", Theme = ...)]` | Member | A coloured label next to the field. |
| `[EnvironmentVariable("MY_TOKEN")]` | Member | Seeds the value from an environment variable. `AllowOverride` decides whether the user may still change it. |
| `[RequiresRestart]` | Member | Marks the change as needing a restart. Surfaces through `IConfigurationService.RestartPendingFor`, the `RequiresRestart` event, and the one `Configuration` reason in `ISystemService.RestartReasons`, which stands while any configuration has such a change. |
| `[TextArea]`, `[CodeEditor(CodeEditorLanguage)]` | Member | Multi-line and syntax-highlighted editors. |
| `[Select]`, `[List]`, `[Record]` | Member | Dropdowns, list editors and record editors. |
| `[CustomAction]` | Method | A button on the settings page. |
| `[ConfigurationAction(ConfigurationActionType)]` | Method | A reactive handler, run as the user edits rather than on a button press. |
| `[HideDefaultSaveAction]` | Class | Removes the built-in save button, for a configuration that saves itself through a custom action. |

### Descriptions

A member's description comes from `[Display(Description = …)]` when it has one,
and otherwise from the member's XML doc `<summary>`. The same goes for the
configuration class itself. The summary is only there to read if your plugin
project generates its documentation file and ships it next to the assembly:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Without that, the build succeeds, the plugin loads, and every description that
relies on a summary is silently empty. `shoko-build` packs the whole output
directory, so once the file is generated it is shipped too.

Either way the text is re-flowed before it reaches the schema. A single line
break becomes a space, runs of spaces and tabs collapse to one, and a blank line
becomes a paragraph break. XML doc tags are converted to Markdown. A longer
description reads well as a multi-line raw string, with no stray indentation to
worry about:

```csharp
[Display(Name = "Sweep Interval (Hours)", Description = """
    How often the whole library is checked against the remote service.

    Lower values catch changes sooner, at the cost of more requests.
    """)]
public int SweepIntervalHours { get; set; } = 24;
```

### A custom action

A `[CustomAction]` method takes a `ConfigurationActionContext<TConfig>` and
returns a `ConfigurationActionResult`. It is an instance method on the
configuration class itself, or on the class of a nested object in it, and it
runs on that object. The button's label is the method's `[Display(Name)]`, or
failing that its name split into words with a trailing "Action" or "Handler"
dropped, so `TestTokenAction` reads "Test Token". This is how a "Test
connection" button works:

```csharp
[CustomAction(Theme = DisplayColorTheme.Primary, Position = DisplayButtonPosition.Start, SectionName = "Login")]
public ConfigurationActionResult TestToken(ConfigurationActionContext<MyConfiguration> context)
{
    try
    {
        var client = context.PluginManager.GetRequiredService<MyApiClient>();
        return client.TestAsync(context.Configuration.ApiToken).GetAwaiter().GetResult()
            ? new("Connected.", DisplayColorTheme.Important)
            : new("The token was rejected.", DisplayColorTheme.Warning);
    }
    catch (Exception ex)
    {
        context.Logger.LogError(ex, "Token test failed.");
        return new("Could not reach the service.", DisplayColorTheme.Danger);
    }
}
```

The context carries the `Configuration` as the user currently has it (unsaved
edits included), a `Logger`, the `ConfigurationService`, the `PluginManager`,
the `Path` of the member the action is attached to, the `User` who pressed the
button and the `Uri` they reached the server on.

`context.Configuration` is what is on screen, so treat it as untrusted beyond
the test itself. Core's AniDB test takes only the credentials from it and
re-loads the saved server address and ports; do the same when an unsaved value
could send a request somewhere unexpected.

A `ConfigurationActionResult` can also carry a redirect or a message; see
`ConfigurationActionRedirect` and `ConfigurationActionMessage`.

---

## `IConfigurationService` directly

You need it when you are working with configurations you do not know at compile
time: the API layer, a settings browser, a plugin inspecting another plugin.

- `GetAllConfigurationInfos()`, `GetConfigurationInfo(plugin)`,
  `GetConfigurationInfo(Guid)`, `GetConfigurationInfo(Type)`,
  `GetConfigurationInfo<TConfig>()`
- `Load(info, copy)`, `Save(info, json)`, `New(info)`, `Deserialize(info, json)`,
  `Serialize(config)`
- `Validate(info, json)`, `Validate(json, schema)`, `GetSchema(info)`,
  `GenerateSchema(type)`
- `CreateProvider<TConfig>()`, if you would rather build a provider than inject
  one
- `RestartPendingFor` and `LoadedEnvironmentVariables`, both keyed by
  configuration ID
- `Saved` and `RequiresRestart`, the unfiltered counterparts of the provider's
  own `Saved`. `RequiresRestart` only carries whether any configuration waits
  on a restart; `RestartPendingFor` names which, and which members.
  `ISystemService.RestartReasons` holds a single `Configuration` reason for
  all of them, alongside the plugin reasons

The service takes the discovered types once, at startup.

---

## Exceptions

| Exception | When |
|---|---|
| `ConfigurationValidationException` | Schema or custom validation failed on `Load` or `Save`. Carries the per-path errors and which of the two operations it was. |
| `InvalidConfigurationActionException` | A custom or reactive action was asked for that does not exist, or the path to it is invalid. |

An `IBaseConfiguration` throws for neither operation: `Load` returns a fresh
instance and `Save` returns `false` without writing anything, so a save against
one fails silently rather than telling you. Check the returned `bool` if it
matters.
