# Web Services

One interface lives here: `IWebThemeService`, the service behind the Web UI's
theme picker, which a plugin **consumes** from DI. Its models are next door in
`Shoko.Abstractions/Web`: `IWebThemeDefinition` (a theme as the server sees it)
and `WebThemeDefinitionData` (the JSON shape authors write).

This page also covers where a plugin's own web surface goes, in
[Routes a plugin serves](#routes-a-plugin-serves).

---

## Routes a plugin serves

A plugin that serves anything over HTTP picks one **namespace**: a short,
URL-safe name that is its own, usually the plugin's name. The casing is up to
you; for reference, core's own APIv3 uses PascalCase. Everything the plugin
serves lives under `plugin/<namespace>`, in three places:

| What | Path |
|---|---|
| API endpoints | `/api/plugin/<namespace>/<your paths>` |
| Pages and static assets | `/plugin/<namespace>/<your paths>` |
| SignalR hubs | `/signalr/plugin/<namespace>/<your paths>` |

Use the same namespace in all three, so a client that knows one of your paths
can find the others. For a plugin whose namespace is `Template`:

```csharp
[ApiController]
[Authorize]
[Route("api/plugin/Template/[controller]")]
public class StatusController : ControllerBase { … }
```

A hub would go at `/signalr/plugin/Template/<hub>`, and pages or assets
under `/plugin/Template/`.

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
        endpoints.MapHub<TemplateHub>("/signalr/plugin/Template/events").RequireAuthorization();
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
        endpoints.MapGet("/api/plugin/Template/status", (bool? verbose) => GetStatus(verbose))
            .WithTags("Template")
            .WithSummary("Gets the plugin's status.")
            .RequireAuthorization();

        // A plain request delegate, listed through the group name.
        endpoints.MapPost("/api/plugin/Template/raw", HandleRawAsync)
            .WithGroupName("Template")
            .WithMetadata(new AcceptsMetadata(["application/json"], typeof(RawRequest)))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(RawResponse), ["application/json"]))
            .WithTags("Template")
            .RequireAuthorization();
    });
}
```

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
