# Logging

Two separate things share this word, and it helps to keep them apart:

- **Writing a log line.** Standard `Microsoft.Extensions.Logging`. There is no
  Shoko-specific API for it, and nothing in this folder is involved.
- **Reading log files back.** That is `ILogService`, the interface in this
  folder. It lists, reads, filters and downloads the server's own log files, and
  it is what `/api/v3/Logging` and the WebUI's log viewer are built on.

`ILogService` is not an extension point. Nothing here is discovered by
`PluginManager.GetExports<T>()`, and there is no interface for a plugin to
implement. It is a DI singleton you inject when you want it.

---

## Writing: getting a logger

Ask for `ILogger<T>` in your constructor. Everything a plugin is constructed
through, the plugin class, a provider, an action, a queue job, resolves through
the container, so this works everywhere:

```csharp
public class MyProvider(ILogger<MyProvider> logger)
{
    public void Refresh(int animeId)
        => logger.LogDebug("Refreshing anime {AnimeID}.", animeId);
}
```

`Microsoft.Extensions.Logging.Abstractions` arrives with the ASP.NET Core
framework reference that `Shoko.Abstractions` already carries, so there is no
package to add.

Use message templates with named placeholders, as above, rather than string
interpolation. The values are captured as structured fields, which is what makes
them filterable later (see the DSL below). `$"Refreshing anime {animeId}"`
collapses the whole thing into one opaque string and loses that.

### Pick the level by what an operator should act on

This is the part plugins get wrong, and it is a question of *level*, not volume.

A sweep is allowed to be chatty. `Debug` and `Trace` exist precisely so that a
job walking thousands of anime can narrate every one of them, and a plugin
should feel free to do that. Nobody sees it unless they turn it on, and when
they are debugging your plugin they will be glad it is there.

What a sweep must not do is log per item at `Information` or above. `Information`
and up is what a user sees without turning anything on, so a line that fires once
per item in a loop is almost never the right thing to put there.

The bug this comes from: the AnimeSchedule.net plugin logged a **`Warning` for
every week it found no data in** while sweeping. A perfectly ordinary barren
range, an off-season, a show that has not started yet, filled the log with
warnings describing nothing wrong. Its per-anime "AnimeSchedule.net does not know
AniDB anime X" lines were already at `Debug`, and those were fine at any volume.
The fix was to move the no-data case down to `Debug` and keep `Warning` for
requests that genuinely failed.

A rough guide:

| Level | For |
|---|---|
| `Trace` / `Debug` | Per-item narration in a sweep, skips, cache hits, "the source has nothing for this". Be as chatty as you like. |
| `Information` | Milestones a user would want to see unprompted, and roughly one per operation rather than one per item. "Swept 1,240 anime, wrote 83 schedules." |
| `Warning` | Something an operator should look into. A request that failed, a response that did not parse, a configuration that cannot work as written. |
| `Error` | Your plugin could not do its job and someone has to intervene. |

The trap is that "there is no data here" reads like a warning while you are
writing the code, and is normal in production. An empty week, a 404 from a
metadata source, an anime the provider has never heard of: all normal, all
`Debug`.

### Warning once for a persistent condition

Some conditions really are worth a warning but recur on every call: an unset API
token, for instance, is something the user should fix, and a `Debug` line they
will never see does not tell them. Warn the first time and drop to `Debug`
afterwards, so the message is seen without the log being flooded:

```csharp
private int _hasWarnedMissingToken;

// ...
if (string.IsNullOrWhiteSpace(token))
{
    if (Interlocked.Exchange(ref _hasWarnedMissingToken, 1) == 0)
        logger.LogWarning("Request to {RequestUri} skipped: no app token configured.", requestUri);
    else
        logger.LogDebug("Request to {RequestUri} skipped: no app token configured.", requestUri);
    return null;
}
```

This only works if the instance holding the flag is long lived. A provider that
core constructs once and holds is; an action, which is resolved fresh per
execution, is not.

---

## Reading: `ILogService`

```csharp
public class MyController(ILogService logService) { }
```

### Finding a file

| Member | Notes |
|---|---|
| `GetAllLogFiles()` | Most recent first, with the current file at the front. |
| `GetCurrentLogFile()` | The file being written to right now. |
| `GetLogFileByID(Guid)` | `null` when not found. |
| `ProcessID` | The current process's ID, so you can filter to this run. |

A `LogFileInfo` carries `ID`, `Date`, `DailyNumber` (0 for the day's latest, then
1 upward from oldest), `FileName`, `FullPath`, `Size`, `IsCurrent`,
`IsCompressed`, `Format` and `LastModifiedAt`. `Size` is a snapshot taken when
the info was built, so it is already stale for the current file.

### Reading entries

`ReadLogFile(fileInfo, options)` pages through one file and `ReadRange(options)`
pages across every readable file. Both return a `LogReadResult` with `Entries`
and a `NextOffset` that is `null` once there is nothing left.

**Order is ascending unless you ask for descending.** `LogReadOptions.Descending`
is a plain `bool`, so it is `false` on any options object you construct.
`ReadRange` substitutes `Descending = true` only when you pass no options at
all, which means `ReadRange()` and `ReadRange(new LogReadOptions())` return
opposite orders. Set `Descending` explicitly whenever you pass options:

```csharp
var options = new LogReadOptions
{
    Limit = 200,
    Descending = true,
    Levels = [LogLevel.Warning, LogLevel.Error],
    Logger = "c#:myplugin",
};

while (true)
{
    var page = logService.ReadRange(options);
    foreach (var entry in page.Entries)
        Handle(entry);

    if (page.NextOffset is not { } next)
        break;
    options.Offset = next;
}
```

`Limit` defaults to 100, and `0` disables the limit entirely, which on a large
range means materialising the lot. `Offset` is a line offset into the filtered
result, not a byte offset.

**Only JSONL files can be read.** `LogFileFormat` distinguishes `JsonL` from
`Legacy`, and structured reading needs the former. `ReadLogFile` on a `Legacy`
file throws `InvalidOperationException`; `ReadRange` quietly skips them. Check
`fileInfo.Format` before calling, or use the range read and accept that old files
are not included.

A `LogEntry` is `TimeStamp` (UTC), `Level`, `ThreadID`, `ProcessID`, `Logger`,
`Caller`, `Message` and an optional `Exception`. `ToString(LogSerializeFormat)`
renders it as `Simple`, `Full`, `Json`, `Legacy` or `Console`.

### Downloading

`DownloadLogFile(fileInfo, options)` and `DownloadRange(options)` return a
`LogDownloadResult` with a suggested `FileName`, a `ContentType` and an open
`Stream`. The stream is yours: dispose it, or hand it to something that will.
`LogDownloadOptions.Format` picks the layout and defaults to `Simple`; an
out-of-range value throws `ArgumentOutOfRangeException`. Unlike reading, a
`Legacy` file can be downloaded, decompressed and served as plain text.

### Deleting

`DeleteLogFile(fileInfo)` throws `InvalidOperationException` when handed the
current file. It returns `void`, whatever its doc comment suggests.

### Maintenance

`StartMaintenance()` and `RunRotationMaintenance()` are driven by core out of the
logging configuration. A plugin has no reason to call either, and calling
`StartMaintenance()` a second time fights with the schedule core already set up.

---

## The filter DSL

The text filters on `LogBaseOptions` (`Logger`, `Caller`, `Message`,
`Exception`) each take a single string encoding both a match mode and a pattern.
A property is inactive only when it is `null`: an empty string is a valid filter,
not an absent one.

A string with **no `:`** is shorthand for "contains, case-sensitive". Otherwise
the form is `<mode><modifiers>:<payload>`, where the payload is everything after
the *first* colon.

| Mode | Meaning |
|---|---|
| `c` | Contains |
| `=` | Equals |
| `^` | Starts with |
| `$` | Ends with |
| `~` | Fuzzy match, always case-insensitive |
| `*` | Regex |

Between the mode and the colon: `!` negates, and `#` makes it case-insensitive.
Either order, each at most once. `#` is ignored for `~` and `*`. A regex payload
is a raw pattern or `/pattern/flags`, case-sensitive unless you pass the `i` flag.

| Filter | Matches |
|---|---|
| `foo` | Contains `foo`, case-sensitive |
| `^#:GET` | Starts with `GET`, case-insensitive |
| `=!:x` | Is not exactly `x` |
| `*:/error/i` | Regex `error`, case-insensitive |
| `c:^:foo` | Contains the literal `^:foo` |

The exception field compares against `entry.Exception ?? ""`, which gives you two
useful special cases: `=:` matches entries with no exception, and `=!:` matches
entries that have one.

The non-text filters are `From`/`To` (inclusive timestamp bounds), `Levels` (the
entry's level must be one of them, when non-empty), `ProcessID` and `ThreadID`.
`HasFilters` reports whether any of these are set; it does not count paging or
the download format.

Invalid options, a malformed regex for instance, throw
`GenericValidationException` from every read and download.
