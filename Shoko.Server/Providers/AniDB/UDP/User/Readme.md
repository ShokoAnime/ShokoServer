### INFO

[AniDB MyList Spec](https://wiki.anidb.net/w/UDP_API_Definition#MyList_Commands)

This has all files related to handling the AniDB User data. This includes MyList and Votes.

### MyList Notes

#### Architecture

All MyList logic lives in `MylistService` (`Shoko.Server/Services/MylistService.cs`), exposed to plugins through
`IMylistService` (`Shoko.Abstractions/Metadata/Anidb/Services/IMylistService.cs`). The queue jobs
(`AddAniDBMylistEntryJob`, `UpdateAniDBMylistEntryJob`, `RemoveAniDBMylistEntryJob`, `SyncAniDBMylistJob`) are thin
wrappers that forward to the service, so the rate-limit and concurrency attributes stay on the queue side while the
logic stays in one place.

The UDP request surface is four consolidated classes in this folder:

- `RequestAddMylist` — `MYLISTADD`, returns the new `MylistID` (or the existing entry on `FILE_ALREADY_IN_MYLIST`).
  Only adds identified by `fid` or `ed2k`+`size` come back with a list ID; a generic add (`aid` + `epno` + `generic=1`)
  returns the *number of entries added* instead, so those entries are cached by their episode ID.
- `RequestGetMylist` — `MYLIST`, returns a single entry by lid/fid/eid/ed2k+size/aid+epno
- `RequestRemoveMylist` — `MYLISTDEL`, by lid/fid/ed2k+size/aid+epno
- `RequestUpdateMylist` — `MYLISTADD ... &edit=1`, updates state/filestate/viewed/storage/source/other. Only the
  supplied fields change, so a successful edit patches the cached entry in place instead of costing a second round-trip

All four share the same identification modes and the shared `MylistEntry` DTO
(`Shoko.Abstractions/Metadata/Anidb/Models/MylistEntry.cs`). The full list is fetched over HTTP via
`RequestMylist` (`Providers/AniDB/HTTP/RequestMylist.cs`).

#### Generic Files

Generic Files are the proper way to handle manual links and files that are added as `generic`. Generic Files are added
via an anime ID and episode number (`aid` + `epno` + `generic=1`).

**The file state does not tell you whether an entry is generic.** AniDB calls it the *Type* in its UI, it belongs to
the user, and per the [Filetype definition](https://wiki.anidb.net/w/Filetype) `other` (100) appears in *both* the
normal-file and generic-file lists while `normal/original` (0) is the documented default for either. Marking an
episode you do not have yields Type 0, not 100. Shoko therefore never sets it on an add — `MylistAddData.FileState`
is passed through only when a caller explicitly asks for one.

Two things muddy this further:

- Importing into AniDB from another site (MAL and friends) marks the imported "files" as `other` (100), so a large
  share of generic entries do carry it — enough to look like a rule, which is where the assumption came from.
- **The UI splits this across two fields that share one numeric space**, which the UDP API hides behind a single
  `filestate` parameter:

  | UI field | Form name | Accepts |
  |---|---|---|
  | Type | `addl.filestate` | `0` normal/original, `1` corrupted/invalid crc, `2` self edited, `15` streamed, `100` other |
  | Generic Type | `addl.genericstate` | `10` self ripped, `11` on dvd, `12` on vhs, `13` on tv, `14` in theaters, `15` streamed, **`16` on bluray**, `100` other |

  `MYLIST` reads back whichever applies to the entry — a generic entry set to *on bluray* in the UI returns
  `filestate=16`. A *write* is validated against neither UI column but against **the UDP definition's own Filestates
  table**, which predates the Blu-ray option and was never updated to include it. That is the only set consistent
  with what AniDB accepts: 11, 15 and 100 go through, while 3, 16, 17 and 18 answer `505 ILLEGAL INPUT OR ACCESS
  DENIED` — note 11 is generic-only yet accepted, and 16 is a real Generic Type value yet rejected.
  `MylistFileState.OnBluRay` is therefore read-only from here, not because of which column owns it, but because the
  UDP validator has never heard of it.

  `MylistService` never sends a value the validator would reject: `MylistFileState.IsWritable` gates it, an
  unwritable one is dropped with a warning, and the desired-state short-circuit ignores it too — otherwise an entry
  could never reach a state no request is able to set, and every sync would re-send the same no-op. A 505 takes the
  whole command down with it, so it is worth not provoking one over a field that was only ever advisory.

  Which column an accepted write lands in is a separate question, and not one to rely on: on a generic entry
  `filestate=11` set Generic Type to *on dvd* and `filestate=15` set it to *streamed*, both leaving Type ignored,
  while `filestate=100` left both columns reading *other*. Filler EP is a legacy type and is deliberately not
  modelled.

Because the ranges only partly overlap, a returned value does say something: `1`, `2` come only from Type, and
`10`–`14`, `16` come only from Generic Type. That is not enough to classify on, though — `0`, `15` and `100` are
ambiguous, and a fresh generic entry defaults to `0`, which is exactly the case that matters.
- The UDP definition heads its state table with *"Filestates: (for normal files, i.e. not generic)"*, yet the table
  holds `self ripped`, `on dvd`, `on vhs`, `on tv`, `in theaters` and `streamed` — every one of which the wiki lists
  under *generic* files. The UDP header is stale; do not read a generic/normal split out of it.

Which leaves the file ID as the only reliable signal. `MylistGenericsCache` holds an index of generic file IDs,
refreshed over HTTP when it goes stale on the next use (never on a timer). It queries a third party rather than
AniDB, so it is gated behind `MyList_UseGenericFileIndex`, which is on by default. When it is enabled and available it
is the only thing consulted.

With it off the sync falls back to treating a file state outside `Normal`/`Corrupted` as generic. That heuristic is
wrong in both directions — a generic entry defaults to `Normal`, and a real file the user marked `SelfEdited` or
`Other` reads as generic — but it is the only signal available without the index, and it is what the sync did before
the index existed.

The sync (`IMylistService.SyncAsync`) is two-tier:

1. **File level** — entries for real files are matched to local files by their `FileID`
   (via `StoredReleaseInfo.ReleaseURI`), then watched/storage states are reconciled.
2. **Episode level** — generic entries are matched to episodes by their `EpisodeID`, and the same reconciliation
   happens against the episode's user data.

A generic entry stands for the *episode*, not for a particular file, so `MylistCache` indexes only generic entries by
episode ID — AniDB reports an episode ID for real files too, and indexing on that alone lets a real file answer a
lookup meant for the episode's generic entry.

`MylistEntry.IsGeneric` is the tri-state answer, and it is the only thing plugins need: `true` for an entry obtained
through a generic operation or matched by the generics index, `false` when the index positively says otherwise or the
entry was identified by `ed2k`+`size`, and `null` when neither applies — a genuine *unknown*, not a *no*.

Note which identification modes prove what. `ed2k`+`size` proves an entry is **not** generic: a generic entry stands
for an episode and has no file behind it to hash. `aid`+`epno`+`generic=1` proves it **is**. A bare `fid` proves
nothing either way — generic entries have file IDs too, which is exactly what the generics index is a list of. Only `true` reaches the episode index.
Plugins get the distinction without the index, the file-ID matching, or the file-state fallback leaking into the
abstractions.

Its watched state is likewise always sourced from the episode's user data — never from the video's. Several files can map to one episode, and feeding each file's watched
date into the shared entry would leave the two permanently out of sync. `AddVideoAsync` and `UpdateVideoAsync` only
resolve which entries a video maps to and then delegate to the entry-level methods; all protocol work lives there.

Entries that cannot be matched are treated as missing and disposed of per the configured delete type — but only when
we know whether they are generic. An entry whose `IsGeneric` is `null` is left alone and counted in the sync summary:
without that answer we do not know which tier should have matched it, so calling it missing is a guess, and acting on
the guess would remove a generic entry from the user's AniDB MyList over a file state that never meant what we read
into it. With `MyList_UseGenericFileIndex` off this is every unmatched entry, so the sync stops disposing of anything
— the safe reading of "we cannot tell", and the reason to consider turning the index on by default.

The full MyList is backed up on every fetch to `<Data>/MyList/Backups/`, as dated gzipped JSON rotated by
`MyList_RetainedBackupCount`. The working cache is `<Data>/MyList/mylist.json.gz` and is deliberately *not* the same
file — the two used to share a path, so each fetch overwrote the cache with a plain array moments after writing it,
losing the fetch stamp and making the cache read as never-fetched on the next restart.