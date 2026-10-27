# Web Services

Two services live here, both **consumed** from DI:

- `IWebUIService` picks the web UI the server serves and keeps its
  releases up to date. A plugin **implements** `IWebUIProvider` to offer a
  web UI of its own; see [Web UIs](#web-uis).
- `IWebThemeService` is the service behind the Web UI's theme picker. Its models
  are next door in `Shoko.Abstractions/Web`: `IWebThemeDefinition` (a theme as
  the server sees it) and `WebThemeDefinitionData` (the JSON shape authors
  write).

This page also covers where a plugin's own web surface goes, in
[Routes a plugin serves](#routes-a-plugin-serves).

---

## Routes a plugin serves

A plugin that serves anything over HTTP uses one **namespace**: a short,
URL-safe name that is its own. The namespace the plugin reserves as
`PluginNamespace` (see [Namespace](../../README.md#namespace)) makes a good
one, since no other loaded plugin can claim it. Everything the plugin serves lives
under `plugin/<namespace>`, in three places:

| What | Path |
|---|---|
| API endpoints | `/api/plugin/<namespace>/<your paths>` |
| Pages and static assets | `/plugin/<namespace>/<your paths>` |
| WebSocket endpoints: SignalR hubs and any other | `/signalr/plugin/<namespace>/<your paths>` |

Use the same namespace in all three, so a client that knows one of your paths
can find the others. For a plugin whose namespace is `template`:

```csharp
[ApiController]
[Authorize]
[Route("api/plugin/template/[controller]")]
public class StatusController : ControllerBase { … }
```

A hub would go at `/signalr/plugin/template/<hub>`, and pages or assets
under `/plugin/template/`.

**Every WebSocket goes under `/signalr`, whatever it speaks.** `/signalr` is
where Shoko's sockets live, so it is the path an admin's reverse proxy
upgrades to WebSocket, and the one development proxies forward sockets for. A
plugin's socket that speaks something other than SignalR, JSON-RPC for a
language server or a protocol of its own, still goes under
`/signalr/plugin/<namespace>/`: SignalR is the WebSocket stack .NET ships and
Microsoft recommends, and the one the server's own hubs use, so its name
stands for "the server's sockets", as "to google" stands for searching the
web. Plain HTTP routes stay under `/api`, even when they belong to the same
feature as a socket. The core's `EnableSignalR` setting (`Web.EnableSignalR`
in the server settings) only decides whether the core's own hubs are mounted;
a plugin's sockets are its own to map, and SignalR stays configured either
way.

### Mapping a SignalR hub

To send events to clients, you rarely need a hub of your own: add a feed to
the server's aggregate hub instead, which clients already connect to (see
[Feeds on the aggregate hub](../SignalR/README.md)). A hub of your own is for
when clients call into your plugin over SignalR.

Map a hub from `IPluginApplicationRegistration.RegisterServices`, the hook that
runs while the request pipeline is built:

```csharp
public static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths)
{
    application.UseEndpoints(endpoints =>
    {
        endpoints.MapHub<TemplateHub>("/signalr/plugin/template/events").RequireAuthorization();
    });
}
```

- **You don't have to call `AddSignalR`.** Core already registers it, so a
  mapped hub works with no registration of your own.
- **Call it when you need per-hub options**, since `AddHubOptions<THub>` is only
  reachable from the builder `AddSignalR()` returns (a larger
  `MaximumReceiveMessageSize`, a hub filter):

  ```csharp
  serviceCollection.AddSignalR()
      .AddHubOptions<TemplateHub>(options =>
      {
          options.MaximumReceiveMessageSize = 512 * 1024;
          options.AddFilter(MyHubFilter.Instance);
      });
  ```

  Calling it a second time is safe. Core's `AddNewtonsoftJsonProtocol()` takes
  the `json` protocol name over from System.Text.Json, so every hub, yours
  included, serializes with Newtonsoft.Json and the API's contract resolver.
- **Always require authorization.** `RequireAuthorization()` admits any
  signed-in user, and `RequireAuthorization("admin")` admits administrators only.
- **Clients sign in with their API key** as a bearer token, which a browser
  can't set on a WebSocket, or as an `access_token` query parameter. The
  JavaScript client does this with
  `withUrl(url, { accessTokenFactory: () => apiKey })`.
- **To push from anywhere else in your plugin**, inject `IHubContext<TemplateHub>`
  rather than holding on to a hub instance, which only lives for one call.

Core keeps `/plugin` clear, and the WebUI never answers for it, even when it is
served from the root. Two plugins choosing the same namespace is a conflict
nothing detects for you, so pick something unmistakably yours.

### Listing mapped endpoints in Swagger

Your controllers are listed in your plugin's own Swagger documents, one per
API version, named `<DllName>-<version>` after your main DLL (`Template-v1`).
Endpoints you map yourself from `IPluginApplicationRegistration.RegisterServices`
go in `<DllName>-v1`, with the unversioned controllers. None go in the core's
documents.

- **A route handler is listed as it is**, described from its signature, and is
  yours because its handler is in your assembly. `ExcludeFromDescription()`
  keeps one out.
- **A plain `RequestDelegate` needs your group name.** It has no signature and
  nothing tying it to your plugin, like the endpoints a library maps for you.
  Add `WithGroupName("<DllName>")` (on a route group, it covers the group). Its
  body and responses come from `AcceptsMetadata` and
  `ProducesResponseTypeMetadata` added through `WithMetadata`; its query
  parameters and headers cannot be described.
- **The rest is standard endpoint metadata**: `WithTags`, `WithSummary`,
  `WithDescription`, `WithName` for the operation ID, and `RequireAuthorization`
  for the API key requirement.
- **Middleware is never listed.** A handler added with `application.Use` has no
  endpoint to describe.

```csharp
public static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths)
{
    application.UseEndpoints(endpoints =>
    {
        // Listed as is.
        endpoints.MapGet("/api/plugin/template/status", (bool? verbose) => GetStatus(verbose))
            .WithTags("Template")
            .WithSummary("Gets the plugin's status.")
            .RequireAuthorization();

        // A plain request delegate, listed through the group name.
        endpoints.MapPost("/api/plugin/template/raw", HandleRawAsync)
            .WithGroupName("Template")
            .WithMetadata(new AcceptsMetadata(["application/json"], typeof(RawRequest)))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(RawResponse), ["application/json"]))
            .WithTags("Template")
            .RequireAuthorization();
    });
}
```

---

## Web UIs

The server serves one web UI at a time, the **active web UI**, from the
path the web UI asks for. The server ships a web UI of its own, the Shoko Web
UI, always served from `/webui`. A plugin offers another by
exporting an `IWebUIProvider`, found like every other provider.

### Writing a web UI

The provider **is** an `IFileProvider`: the server mounts it with the standard
static file middleware, so ETags, ranges and content types are handled for you.
Answer every path of your web UI from `GetFileInfo`, including the fallback to
your index for client-side routes. A file you build or rewrite in memory, such
as an index with your paths filled in, can be a `MemoryFileInfo`
(`Shoko.Abstractions.Web`).

```csharp
public class MyWebUI(PluginPaths<MyPlugin> paths) : IWebUIProvider<MyWebUIConfiguration>
{
    private readonly PhysicalFileProvider _files = new(Path.Combine(paths.DataPath, "web"));

    public string Name => "My Web UI";

    public string MountPath => "/my-web-ui";

    public IReadOnlyDictionary<WebUILinkType, string> Links { get; } = new Dictionary<WebUILinkType, string>
    {
        [WebUILinkType.Series] = "/series/{seriesID}",
        [WebUILinkType.Video] = "/video/{videoID}",
    };

    public IFileInfo GetFileInfo(string subpath)
        => _files.GetFileInfo(subpath) is { Exists: true } file ? file : _files.GetFileInfo("/index.html");

    public IDirectoryContents GetDirectoryContents(string subpath) => _files.GetDirectoryContents(subpath);

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    // CurrentRelease, BundledRelease, ReleaseInstalled, GetReleases and InstallRelease below.
}
```

- **`MountPath`** is yours to pick, as long as it is not under `/api`,
  `/plugin`, `/signalr` or the Swagger UI's path; your plugin fails to start
  otherwise. A route the server or a plugin maps always wins over a file of the
  web UI, so even a web UI at `/` cannot hide them. Asking for the Shoko Web
  UI's path, `/webui`, takes its place, bookmarks included. Anywhere else, the
  Shoko Web UI's path redirects to yours. The server reads it on every
  request, so you may change it while running, such as from your
  configuration; a path it may not have is refused with a warning, and the
  last allowed one is kept.
- **`Links`** are templates relative to `MountPath`, with the placeholders each
  `WebUILinkType` names. Applications such as media server plugins read them
  from `GET /api/v3/WebUI` to link into your pages. Leave a type out when your
  web UI has no such page.
- **Releases** are the files of your web UI, on a cadence of their own. List
  them in `GetReleases`, newest first, with the one your plugin ships marked
  `IsBundled` and also returned by `BundledRelease`, which the server reads at
  start-up and so must not need the network. Install one in `InstallRelease`
  and raise `ReleaseInstalled` once it is in place. Install into a new folder
  and switch to it, so a request never sees half a release. Set
  `IncompatibleReason` on a release your plugin cannot serve, such as one
  needing an API your plugin no longer has; the server adds its own for
  releases outside `MinimumServerVersion` and `MaximumServerVersion`.
- **First-run setup** happens in the active web UI, so your web UI must be able
  to set up a new server from the release your plugin ships.
- **Configuration** goes through the generic form, as for every other provider:
  `IWebUIProvider<TConfiguration>` with a `TConfiguration` implementing
  `IWebUIProviderConfiguration`. Its page is rendered next to the web UI.

### Which web UI is served

The admin picks the web UI, through `IWebUIService.SelectProvider` or
`PATCH /api/v3/WebUI/Provider`, and turns serving on or off through
`IsEnabled`. Both apply at once, without a restart. While a web UI is served
away from `/`, a request for `/` that no route took is redirected to it.
Without a pick, the first plugin web UI in load order is served, else the Shoko
Web UI. A picked web UI that is gone or failed falls back the same way.

`ActiveProvider` is never `null`. While serving is off, it is still the web UI
whose releases the service lists and installs. Each `WebUIProviderInfo` says
whether it is the active one (`IsActive`) and whether the admin picked it
(`IsSelected`); a picked web UI whose plugin failed is selected but not active,
and when none is selected, the pick is automatic. `ActiveProviderChanged` is
raised whenever another web UI becomes active, serving is turned on or off, or
the active web UI's mount path changes. A web UI becoming active gets the same
care as at start-up: its bundled release is installed when nothing is.

Open browser tabs keep running the web UI they loaded until they reload. A web
UI with a service worker at a shared path should check its own version when it
starts, so a cached copy does not outlive a switch.

### Keeping it up to date

The service asks the active web UI for releases and applies the server's rules
on top: the release channel (`ReleaseChannel.Auto` is the installed release's,
else the server's), compatibility, and the notes since the installed release.
When the server starts it installs, for the active web UI:

1. the bundled release, when nothing is installed;
2. the newest compatible release, when the installed one became incompatible,
   such as after your plugin was upgraded;
3. the bundled release, when it is newer than the installed one on the same
   channel and `Web.AutoReplaceWebUIWithIncluded` is on. A `Debug` release is
   never replaced this way.

---

## What a theme is

A theme is a pair of files in the server's `themes/` directory:

```
themes/
  my-theme.json   the definition: name, version, author, CSS variables, URLs
  my-theme.css    optional CSS overrides
```

The **ID is the file name**, so the two files always share a base name, and
`IWebThemeDefinition.JsonFileName` and `CssFileName` are derived from it. A file
name is lowercased and its spaces and underscores folded to dashes on the way in
(`My Theme` becomes `my-theme`), and the name has to match
`^[A-Za-z][A-Za-z0-9_-]*$` or the call throws `ValidationException`. Only JSON
files with a conforming name are picked up as themes at all.

The definition carries `Values`, a dictionary of CSS custom properties, and
`CssContent`, free-form CSS. Both are rendered inside a `.theme-{ID}` selector
block, so a theme cannot leak styles outside its own scope. A definition with
neither is rejected as empty.

`CssUrl` and `UpdateUrl` make a theme updatable: the definition is re-fetched
from `UpdateUrl`, and the CSS from `CssUrl` (which may be relative, resolved
against `UpdateUrl`). Both have to be `http` or `https`, the JSON has to come
back as `application/json`, `text/json` or `text/plain`, and the CSS as
`text/css` or `text/plain`.

---

## Getting hold of it

The service is a singleton in the core container, so constructor injection
works in anything the container builds.

```csharp
// Registered from your plugin's RegisterServices.
public class MyThemeInstaller(IWebThemeService themeService)
{
    public IWebThemeDefinition? GetMine() => themeService.GetTheme("my-theme");
}
```

---

## Reading

```csharp
var themes = themeService.GetThemes();
var theme = themeService.GetTheme("my-theme");
```

Both read a cached listing of the themes directory that is refreshed at most
every ten minutes. Pass `forceRefresh: true` when you have reason to believe
someone dropped files in behind the server's back; a theme installed through
this service is folded into the cache immediately and needs no refresh.

`GetTheme` returns `null` for an unknown ID rather than throwing.

---

## Installing and updating

Four entry points, in descending order of how much the service does for you:

| Call | Takes | Notes |
|---|---|---|
| `InstallThemeFromUrl(url, preview)` | An absolute URL to a `.json` definition | Derives the ID from the URL's last path segment, fetches, then hands off to the JSON path |
| `InstallOrUpdateThemeFromJson(content, fileName, preview)` | Raw JSON plus a file name | Parses into `WebThemeDefinitionData`, then hands off below |
| `InstallOrUpdateThemeFromData(data, fileName, preview)` | A `WebThemeDefinitionData` you built | The path to use when your plugin ships a theme in code |
| `CreateOrUpdateThemeFromCss(content, fileName, preview)` | Raw CSS plus a file name | Writes a minimal definition wrapping that CSS |

All four validate first and write only if `preview` is `false`; a preview comes
back with `IsPreview` set and nothing touched on disk.

`UpdateThemeOnline(theme, preview)` re-fetches an installed theme from its
`UpdateUrl` and refuses a version that is not higher than the installed one.

A plugin shipping its own theme wants the data overload:

```csharp
await themeService.InstallOrUpdateThemeFromData(new WebThemeDefinitionData
{
    Name = "My Theme",
    Version = "1.2",
    Author = "me",
    // Keys are the CSS custom properties the Web UI reads; take the names from
    // the Web UI's own stylesheet rather than from this example.
    Values = new Dictionary<string, string>
    {
        ["--my-accent"] = "#7c9cff",
    },
}, fileName: "my-theme");
```

Call it once per start (from `IPlugin.Setup`, since the service only touches
files, or a hosted service's `StartAsync`) if the theme should reappear after a
user deletes it, or guard it on a stored flag of your own. Use an ID unlikely to
collide: an existing theme with the same ID is overwritten without warning.

---

## Removing

`RemoveTheme(theme)` deletes both files from disk and drops the theme from the
cache. It returns `false` when neither file exists, which is the only "not
found" signal you get.

---

## Watch out for

- **`UpdateThemeOnline` on a theme with no `UpdateUrl` returns that theme
  unchanged**, without throwing. Check `UpdateUrl` yourself if "this theme
  cannot be updated" needs to be distinguishable from "this theme was already
  current".
- **`CreateOrUpdateThemeFromCss` refuses a theme that has an `UpdateUrl`**, with
  a `ValidationException`. An online theme is owned by its author, and hand-
  editing it would be silently undone by the next update.
- **Inline CSS and `CssUrl` do not mix.** Updating a theme that already has
  inlined `CssContent` from a definition carrying a `CssUrl` throws; clear one
  of the two first.
- **`Version` is lenient on the way in, strict on the way out.** The data model
  accepts `1`, `1.2` or `1.2.3` as a string; `IWebThemeDefinition.Version` is a
  real `System.Version`, and `UpdateThemeOnline` compares with it.
- **Network calls have a one minute timeout** and surface as
  `HttpRequestException`; malformed content surfaces as `ValidationException`.
  Both escape the call, so wrap installs that run on start-up.
- **`IsInstalled` is computed from the files on disk** and cached per definition
  instance, so re-read the theme after installing or removing instead of
  re-checking the object you already held.
