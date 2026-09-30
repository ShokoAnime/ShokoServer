# Logging

Two separate things share this word:

- **Writing a log line** is standard `Microsoft.Extensions.Logging`, with no
  Shoko-specific API.
- **Reading log files back** is `ILogService`, a DI singleton in this folder. It
  lists, reads, filters and downloads the server's own log files, and backs
  `/api/v3/Logging` and the WebUI's log viewer.

---

## Writing: getting a logger

Ask for `ILogger<T>` in the constructor of anything the container builds: a
provider, an action, a queue job, a service. The class implementing `IPlugin` is
the exception; it takes a logger in `Setup`.

```csharp
public class MyProvider(ILogger<MyProvider> logger)
{
    public void Refresh(int animeId)
        => logger.LogDebug("Refreshing anime {AnimeID}.", animeId);
}
```

The logging abstractions come with the ASP.NET Core framework reference
`Shoko.Abstractions` already carries, so there is no package to add.

Use message templates with named placeholders, as above, not string
interpolation: the values are captured as structured fields.

### Pick the level by what an operator should act on

A sweep may narrate every item at `Debug` or `Trace`; nobody sees it unless
they turn it on. It must not log per item at `Information` or above, which is
what a user sees by default.

| Level | For |
|---|---|
| `Trace` / `Debug` | Per-item narration in a sweep, skips, cache hits, "the source has nothing for this". Be as chatty as you like. |
| `Information` | Milestones a user would want to see unprompted, and roughly one per operation rather than one per item. "Swept 1,240 anime, wrote 83 schedules." |
| `Warning` | Something an operator should look into. A request that failed, a response that did not parse, a configuration that cannot work as written. |
| `Error` | Your plugin could not do its job and someone has to intervene. |

"There is no data here" reads like a warning while you write the code, and is
normal in production: an empty week, a 404 from a metadata source, an anime
the provider has never heard of are all `Debug`.

### Warning once for a persistent condition

A condition the user should fix but that recurs on every call, such as an unset
API token, is worth one warning. Warn the first time and drop to `Debug`
afterwards:

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

The flag needs a long-lived instance: a provider the core holds, not an action,
which is resolved fresh per execution.

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
current file.

### Maintenance

`StartMaintenance()` and `RunRotationMaintenance()` are driven by core from the
logging configuration. A plugin has no reason to call either.

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
