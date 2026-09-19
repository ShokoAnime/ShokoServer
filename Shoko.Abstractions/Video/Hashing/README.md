# Hash Providers

This folder defines the public API surface for computing digests of a video
file. Providers are pluggable: any plugin can register one or more
`IHashProvider` implementations to contribute hash types, alongside the
built-in hasher that ships in core.

---

## Where hashing sits

Hashing is the second step of the import pipeline, and everything downstream
depends on its output:

```
ScanFolderJob / file watcher   finds a file on disk
        │
        ▼
HashFileJob                    calls IVideoHashingService.GetHashesForPath
        │
        ├─ every enabled provider is asked for hashes
        ├─ the ED2K digest becomes VideoLocal.Hash, the file's identity
        ├─ every digest is stored as a VideoLocal_HashDigest row
        │
        ▼
release search                 ScheduleFindReleaseForVideo, keyed on ED2K + size
```

Two consequences worth internalising before writing a provider:

1. **ED2K is not optional.** If no provider returns a well-formed ED2K digest
   for a file, the hash run throws and the file is never imported. The throw
   comes from the service, inside `GetHashesForPath` / `GetHashesForFile`, once
   it has collected every provider's answer; your own
   `IHashProvider.GetHashesForVideo` is not where it happens and never throws
   for this. (There is no `GetHashesForVideo` on `IVideoHashingService` at all;
   the name belongs to the provider contract.) The service defends against this
   by always mapping ED2K back to the built-in provider when nothing else
   claims it.
2. **A hash run happens once per file,** not once per provider run. Your
   provider is called with the file already open to the rest of the pipeline,
   so a slow provider slows down every import.

---

## Implementing a provider

```csharp
public class MyHashProvider(ILogger<MyHashProvider> logger) : IHashProvider
{
    public const string HashType = "MyHash";

    public string Name => "My Hasher";

    public string Description => "Computes a perceptual digest over sampled frames.";

    // Declared once, fixed for the provider's lifetime. This is what the user
    // is offered in the UI, not what is actually turned on.
    public IReadOnlySet<string> AvailableHashTypes { get; } = new HashSet<string> { HashType };

    public async Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(
        HashingRequest request, CancellationToken cancellationToken = default)
    {
        var (file, existingHashes, enabledHashTypes) = request;

        // Nothing of ours is turned on for this run: do no work at all.
        if (!enabledHashTypes.Contains(HashType))
            return [];

        // The caller may have handed us a digest it already has on record.
        if (existingHashes.FirstOrDefault(hash => hash.Type == HashType) is { } existing)
            return [new HashDigest { Type = existing.Type, Value = existing.Value, Metadata = existing.Metadata }];

        var digest = await ComputeAsync(file.FullName, cancellationToken);
        return [new HashDigest { Type = HashType, Value = digest, Metadata = "v1" }];
    }
}
```

`Name`, `AvailableHashTypes` and `GetHashesForVideo` are the only members you
have to supply. `Description` defaults to `null` and `Version` falls back to
your assembly version.

To have the WebUI render a settings page for the provider, implement
`IHashProvider<TConfiguration>` where `TConfiguration : IHashProviderConfiguration`
instead. The configuration is loaded through a `ConfigurationProvider<T>` you
take in your constructor; it is not handed to `GetHashesForVideo`.

### Registering

Most providers need no DI registration at all: `PluginManager.GetExports<IHashProvider>()`
finds the type, constructs it with constructor injection, and
`IVideoHashingService` holds that instance for the life of the process.
Register the **concrete type** as a singleton only if your own code resolves
the provider, and never register it under the `IHashProvider` interface. The
full rule, and why the interface registration goes wrong, is in the
[abstractions README](../../README.md).

---

## Available versus enabled hash types

These two are easy to confuse, and the difference is where most surprises come
from.

| | Owner | Meaning |
|---|---|---|
| `IHashProvider.AvailableHashTypes` | The provider | Everything this provider *can* compute. Fixed, read once at startup. |
| `HashProviderInfo.EnabledHashTypes` | The user | The subset of the above the user actually turned on. Persisted, changeable at runtime. |

The persisted form is the important detail: the service stores a
`Dictionary<string hashType, Guid providerID>`, so **a hash type has exactly
one owning provider at a time**. Two providers both offering `CRC32` do not
both compute it. When more than one has a type enabled, the first in the
service's order keeps it: the built-in provider first, then plugins by plugin
name. So enabling a type for yours does nothing while an earlier provider still
has it enabled. To take it over, disable it on the current holder in the same
`UpdateProviders` call, and check afterwards that you actually got it. Read
your own current state through `IVideoHashingService.GetProviderInfo(this)`,
and subscribe to `ProvidersUpdated` to notice when the user changes it.

### What the defaults are

At startup, the hashing service seeds the map **only when it is completely
empty**, which in practice means the first ever startup:

```
ED2K  → the built-in CoreHashProvider
CRC32 → the built-in CoreHashProvider
```

Everything else the built-in provider offers (`MD5`, `SHA1`, `SHA256`,
`SHA512`) starts off, and so does every hash type from every third-party
plugin. **A newly installed plugin provider has no enabled hash types until a
user turns one on**, which means its `GetHashesForVideo` is never called
(`GetAvailableProviders(onlyEnabled: true)` skips a provider with an empty
set). If your plugin wants to be useful out of the box it has to say so in its
own documentation, or flip the state itself through
`IVideoHashingService.UpdateProviders`.

On every update the service re-derives the map from the live provider list,
drops types whose provider no longer offers them, and re-adds `ED2K` pointing
at the built-in provider if nothing claims it.

---

## The request and the result

`HashingRequest` deconstructs into three parts:

- **`File`**, a `FileInfo` for the file to hash. It is the resolved target when
  the entry on disk is a symbolic link, so read `file.FullName` rather than
  trying to resolve anything yourself.
- **`ExistingHashes`**, digests already on record for this video, **pre-filtered
  to your own enabled types**. It is empty when the caller asked for a forced
  re-hash, and never contains a type you do not own, so do not expect to find
  ED2K in here unless you are the ED2K provider.
- **`EnabledHashTypes`**, your enabled set for this run.

Each `HashDigest` you return carries a `Type`, a `Value` and an optional
`Metadata` string for anything type-specific (a format version, a compression
scheme, a sampling parameter). All three are persisted, and all three take part
in equality.

Output is filtered against `EnabledHashTypes` on the way back, so a digest of a
type you were not asked for is silently dropped. Compute only what was asked
for.

---

## Sequential and parallel mode

`IVideoHashingService.ParallelMode` decides whether providers run one after the
other or all at once over the same file. It is a user-facing switch, so your
provider has to work either way:

- In **sequential** mode (the default) providers run in order, core first.
- In **parallel** mode every provider is started on its own task against the
  same `FileInfo`, so each one is doing its own reads of the same file at the
  same time.

Nothing serialises your provider against itself either: `HashFileJob` has a
default concurrency of 2, so two files can be in flight at once. Keep instance
state thread-safe, or keep none.

---

## Mistakes that are easy to make

- **Computing a hash type that is not enabled.** The result is discarded. Check
  `request.EnabledHashTypes` first and return `[]` when there is nothing to do,
  rather than hashing the file and throwing the answer away.
- **Taking over `ED2K` without being able to produce it.** Taking it means
  disabling it on the built-in provider as well; do that and then return
  nothing for a file, and the whole hash run throws and the file never
  imports.
- **Renaming or moving the provider class.** The provider ID is derived as a
  v5 UUID over `"HashProvider={type.FullName}"` in the plugin's own ID
  namespace, so changing the namespace or the class name produces a different
  ID. The user's enabled hash types point at the old ID, and the renamed
  provider comes back with nothing enabled.
- **Unstable `Metadata`.** Stored digests are compared on `Type`, `Value` and
  `Metadata` together, so a metadata string that changes between runs (a
  timestamp, a machine name, a rehashed float) makes every re-hash delete and
  re-insert the row. Keep it deterministic, and version it explicitly if the
  format can change.
- **Assuming `ExistingHashes` means "skip".** It is only populated when the
  caller passed `useExistingHashes`, and the service clears it when the file
  size changed since the last run. Treat it as an optimisation, never as proof
  the file is unchanged.
- **Ignoring the cancellation token.** Hashing is long-running by definition and
  the job is cancelled on shutdown. A provider that runs to completion anyway
  holds up the shutdown.
- **Inventing a hash type name that collides.** Type names are plain strings
  matched exactly, and they end up in the database and in the settings map.
  Prefer something unmistakably yours over a generic name another plugin might
  also reach for.
