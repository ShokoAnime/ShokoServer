# Video Release Grouping, Comparison, and Auto-Management

`VideoReleaseGroupingService` maps a flat list of `VideoLocal_Place` objects into
`VideoReleaseCandidate` buckets. Each bucket represents a set of files estimated
to belong to the same release.

`ReleaseComparisonService` ranks those candidates using a configurable sequential
tie-breaker comparison, determines which is the primary, and identifies redundant
secondaries that are safe to delete.

`ReleaseAutoManagementService` is called at the end of the import pipeline
(inside `FinalizeReleaseSearchJob`) and, when `AllowDeletion` is enabled, removes
files from redundant candidates automatically.

The grouping is **deterministic** — given the same inputs it always produces the
same buckets — but it is a **heuristic**, not a guaranteed truth. Provider
metadata (when available) is the strongest signal; MediaInfo stream data fills in
the gaps for unrecognized files.

---

## What is a "release"?

A release is a collection of files published together by the same group at roughly
the same time, sharing the same encoding characteristics. All episodes of Doki's
BD encode of Air are one release. The DVD-only episode 13 they also published is a
different release, even though it came from the same group.

---

## Grouping signals

Files are compared on the following signals. The critical distinction is between
**hard separators** (always cause a split, even if one side has no data) and
**soft separators** (only cause a split when *both* sides have a known,
non-default value *and* those values differ).

| Signal | Source | Type | Notes |
|--------|--------|------|-------|
| Import folder ID | `VideoLocal_Place.ManagedFolderID` | Hard | Files in different library roots are always separate |
| Parent directory | Path segment before the filename in `RelativePath` | Hard | `Air/` vs `Naruto/` keeps series apart even when every other signal matches |
| Video codec | `MediaInfoUtility.TranslateCodec(videoStream)` → e.g. `H264`, `HEVC`, `AV1` | Hard (when known) | Normalized via the codec ID map; if either file has no codec signal, no conflict |
| Bit depth | `VideoStream.BitDepth` | Hard (when known) | 8-bit vs 10-bit; zero = unknown, never conflicts |
| Standard resolution | `MediaInfoUtility.GetStandardResolution(width, height)` → e.g. `1080p`, `720p` | Hard (when known) | Bucketed, not exact pixels; null = unknown, never conflicts |
| Release group | `StoredReleaseInfo.GroupID` + `.GroupSource` | Soft | Files with no SRI (or no group recorded) are compatible with any group |
| Release source | `StoredReleaseInfo.Source` | Soft | `BluRay`, `Web`, `DVD`, etc.; `Unknown` never conflicts |
| Audio language set | `StoredReleaseInfo.AudioLanguages` (primary); `AudioStream.Language` from MediaInfo (fallback for unrecognized files only) | Soft | Empty list = unknown, never conflicts; non-empty sets must match exactly (sorted) |
| Subtitle language set | `StoredReleaseInfo.SubtitleLanguages` (primary); internal `TextStream.Language` from MediaInfo (fallback for unrecognized files only) | Soft | Same rule |
| Primary audio codec | `MediaInfoUtility.TranslateCodec(first audioStream)` → e.g. `FLAC`, `AAC`, `AC3` | Soft | Null = unknown, never conflicts |
| Container format | `GeneralStream.Format` → e.g. `MATROSKA`, `MPEG-4` | Soft | Null = unknown, never conflicts |

**Version numbers and quality flags are excluded from first-pass grouping.**
A v2 patch of an episode is a correction to the same release, not a distinct
release. `IsCorrupted` and `IsChaptered` are normal per-file variations within a
single release (e.g. one corrupt file in an otherwise clean batch). These fields
only matter during the **collision split** phase described below.

### Language source priority

Language data comes from two sources, applied in strict priority order:

1. **`StoredReleaseInfo.AudioLanguages` / `.SubtitleLanguages`** (AniDB, manually
   curated) — used whenever an SRI record exists for the file, even if the
   language list is empty. An empty SRI language list means "AniDB has no
   language recorded for this file," which is treated as a wildcard, not a
   reason to fall back to MediaInfo.
2. **MediaInfo stream tags** (`AudioStream.Language`, `TextStream.Language`) —
   used only when no SRI record exists (unrecognized files). MediaInfo language
   tags are often missing or inaccurate, so they are never used in preference
   to AniDB data.

### Missing fields are wildcards

A missing or unknown value (null, empty string, zero, `ReleaseSource.Unknown`,
or an empty language list) is treated as a **wildcard**: it is compatible with
any value on the other side. Only when *both* sides supply a known, non-default
value that differs is a conflict declared and a separate candidate created.

This means a file whose AniDB entry lacks a language tag will still join the
release group that has the tag. Likewise, a file with no `StoredReleaseInfo`
at all will join the first compatible bucket in the same folder rather than
always forming its own candidate.

### Processing order

SRI-backed files are processed before unrecognised files. This ensures that
complete signals seed the buckets first, and partial-signal files merge into
the best-matching existing bucket rather than creating a new one that a
later complete file cannot join.

---

## Episode collisions and splitting

After the initial fuzzy grouping, each bucket may contain files that cover the
same episode. The grouper applies these checks in order and yields the first one
that applies:

1. **No collision** — each episode is covered by exactly one file → keep together.
2. **Partial collision** — *some* episodes are covered by multiple files but at
   least one episode has only a single file → keep together. This is a
   mixed-patch release: the group corrected a handful of episodes (v2 patches)
   while leaving the rest at v1. Dropping the v1 files would leave those
   unpatched episodes without any file.
3. **All episodes collide, different versions** → split one candidate per
   `StoredReleaseInfo.Version`. This is the "complete v1 and v2 batch" case —
   every episode has a file on both sides, so neither side becomes incomplete
   after the split.
4. **All episodes collide, same version, different quality tier**
   (`IsCorrupted` / `IsChaptered` differ) → split one candidate per quality
   tier so the caller can prefer the better set.
5. **All episodes collide, same version, same quality tier** → keep together
   (ambiguous; for example, a combined-episode file that covers the same episode
   as a single-episode file).

**Full vs. partial collision example:** a group releases 24 episodes where
only episodes 5, 10, and 15 have both v1 and v2 files. Episodes 1–4, 6–9, etc.
have only v1. That is a *partial* collision (step 2) — stay together. If
*all* 24 episodes have both a v1 and a v2 file, that is a *full* collision
(step 3) — split into two candidates.

---

## Aggregate quality fields on a candidate

When `BuildCandidate` assembles a `VideoReleaseCandidate` from its constituent
files it computes each quality field over the whole file set rather than taking
a single representative, because a release commonly contains outlier files (e.g.
a bonus H264 OVA in an otherwise HEVC release).

| Field | Aggregation |
|-------|-------------|
| `VideoCodec`, `Resolution`, `BitDepth`, `AudioCodec`, `Container` | **Majority (mode)** — most common non-null value; null if all are null |
| `AudioStreamCount`, `SubtitleStreamCount` | **Majority** of non-zero values; 0 if all are zero |
| `Source`, `Version` | **Majority** of non-unknown / non-zero values |
| `AudioLanguages`, `SubtitleLanguages` | **Majority language set** — the most common non-empty ordered set |
| `IsCorrupted` | `true` if **any** file is corrupted; otherwise `false` |
| `IsChaptered` | `true` if **any** file has chapters; `false` if any has chapter data but none have chapters; `null` if chapter data is absent for all files |
| `IsCensored` | `true` if **any** file is censored; `false` if any has the flag set to false; `null` if never reported |
| `GroupID`, `GroupName`, `GroupShortName`, `GroupSource` | From the **first SRI-backed file** in the candidate (consistent by definition within a grouped release) |

---

## Episode coverage

`VideoReleaseCandidate.EpisodeCoverage` is the union of all `(EpisodeType, int)`
pairs covered by any file in the candidate. It is used by the comparison system
to determine whether one candidate's episodes are a subset of another's.

Episode identifiers follow the AniDB / Shoko convention: a bare number for normal
episodes (`1`, `12`), and a letter prefix for other types:

| Type | Prefix | Example |
|------|--------|---------|
| Episode | _(none)_ | `1` |
| Special | `S` | `S2` |
| Credits | `C` | `C1` |
| Trailer | `T` | `T1` |
| Parody | `P` | `P1` |
| Other | `O` | `O1` |

`(Special, 1)` and `(Episode, 1)` are distinct keys, so a specials-only release
is never considered to cover a regular-episode release and vice versa.

---

## Release comparison and ranking

`ReleaseComparisonService` compares two `VideoReleaseCandidate` objects using a
**sequential tie-breaker** strategy: the first signal in
`ReleaseComparisonPreferences.SignalPriority` where the candidates differ decides
the winner. Later signals are only consulted when all earlier ones are tied.

A null or unknown value on either side is treated as a **skip** (not a loss) —
the signal is ignored and the next one is tried. This means a candidate without
source metadata cannot lose to one that has it on the `Source` signal alone.

### Configurable signals

| `ReleaseSignalType` | Default priority | Logic |
|---------------------|-----------------|-------|
| `Source` | 1 | Index in `SourceOrder` preference list; lower index = better; unknown = last |
| `IsCorrupted` | 2 | Not corrupt beats corrupt |
| `Resolution` | 3 | Index in `ResolutionOrder` |
| `BitDepth` | 4 | Higher is better when `PreferHigherBitDepth = true` (default); 0 = skip |
| `VideoCodec` | 5 | Index in `VideoCodecOrder` |
| `Chapters` | 6 | Chaptered beats unchaptered; null = skip |
| `AudioStreamCount` | 7 | Higher = better; 0 = skip |
| `SubtitleStreamCount` | 8 | Higher = better; 0 = skip |
| `AudioCodec` | 9 | Index in `AudioCodecOrder` |
| `SubGroup` | 10 | Index in `SubGroupOrder`; empty list = always skip |
| `Version` | 11 | Higher = better; 0 = skip |
| `IsCensored` | 12 | Not censored beats censored; null = skip |

Default ordered preference lists (first = most preferred):

- **Source**: `BluRay, DVD, TV, Web` (unknown is treated as last)
- **Resolution**: `2160p, 1440p, 1080p, 720p, 480p`
- **VideoCodec**: `HEVC, H264, AV1, MPEG4, VC1, MPEG2`
- **AudioCodec**: `FLAC, DCA, AAC, AC3, MP3`
- **SubGroup**: _(empty — subgroup comparison is always a tie unless explicitly configured)_

### Redundancy and deletion

A secondary candidate C is **redundant** (eligible for deletion) when:

- The primary P ranks higher than C, **and**
- `C.EpisodeCoverage ⊆ P.EpisodeCoverage` — P already covers every episode C covers.

A secondary candidate that ranks lower but covers episodes the primary does not
is **not** redundant — the primary is incomplete relative to that candidate. This
handles airing series naturally without any end-date guard:

- 1080p has eps 1–6, 720p has eps 1–6 → 720p is redundant (fully covered).
- 1080p has eps 1–4, 720p has eps 1–6 → 720p is **not** redundant (1080p still
  missing eps 5–6). When eps 5 and 6 arrive for 1080p, the coverage check will
  pass and 720p becomes redundant.

### `AllowDeletion`

By default `AllowDeletion = false`. In this mode the service logs which candidates
(or files) it would delete but removes nothing. Set `AllowDeletion = true` to enable
actual file removal. Deletion removes files from the database only
(`removeFile: false`); the physical files on disk are not touched.

### `PerFileDeletionForAiringSeries`

Default: `true`.

For **completed series** the whole-candidate rule is always sufficient: once a
preferred release has caught up with every episode, every lower-ranked candidate
whose coverage is a subset is fully redundant and can be deleted as a unit.

For **airing series** a secondary candidate may be only *partially* covered —
the preferred release has files for eps 1–8 while the secondary has eps 1–10.
Deleting the entire secondary would remove eps 9 and 10, which nothing else covers.

When `PerFileDeletionForAiringSeries = true` and the series is still airing
(`AniDB_Anime.EndDate` is null or in the future), redundancy is evaluated
**per file** instead of per candidate:

- Each file in a secondary candidate is checked individually.
- A file is redundant if its own episode coverage (from its SRI cross-references)
  is a subset of the primary's episode coverage.
- Only redundant files are deleted; non-redundant files in the same candidate
  (covering episodes the primary has not yet reached) are retained.
- A file with no SRI record (unknown episode coverage) is always retained.

When the setting is `false`, or when the series is complete, the whole-candidate
rule applies regardless.

### `EpisodeTypeScope`

`KeepTogether` (default): coverage is measured holistically across all episode
types. A specials-only candidate is never superseded by a regular-episode candidate.

`BestPerType`: coverage is evaluated independently for regular episodes and
non-regular episodes, allowing the system to choose different primaries for each
type. *(Not yet implemented in the current release.)*

---

## Scenarios

### Standard release — same group, source, folder

> Doki's BD encode of 30-sai no Hoken Taiiku (12 episodes, all in the same folder)

All 12 files share `GroupID=584 / GroupSource=AniDB / Source=BluRay / Audio=ja / Sub=en / 1080p Hi10P FLAC / Matroska` and the same parent directory. They produce **one candidate**.

---

### v1 / v2 patches stay together

> Air episodes: most are v1, episodes 3 and 7 are v2

```
Air/[Doki] Air - 01 (1280x720 Hi10P BD FLAC) [77D45BED].mkv   ← ep 1,  Version 1
Air/[Doki] Air - 03v2 (1280x720 Hi10P BD FLAC) [96E5D20E].mkv  ← ep 3,  Version 2
Air/[Doki] Air - 07v2 (1280x720 Hi10P BD FLAC) [88DFF7DA].mkv  ← ep 7,  Version 2
```

Each file covers a different episode (1, 3, 7), so there is no collision. All
three land in the **same candidate**. The `v2` suffix changes
`StoredReleaseInfo.Version`, but that field is not part of the grouping key.

---

### Complete v1 and v2 batches → two candidates

> The same group re-releases all episodes with a full v2 encode; the user keeps both

```
Air/[Doki] Air - 01v1.mkv   ← ep 1, Version 1  ┐
Air/[Doki] Air - 02v1.mkv   ← ep 2, Version 1  ├ candidate A (v1)
Air/[Doki] Air - 03v1.mkv   ← ep 3, Version 1  ┘
Air/[Doki] Air - 01v2.mkv   ← ep 1, Version 2  ┐
Air/[Doki] Air - 02v2.mkv   ← ep 2, Version 2  ├ candidate B (v2)
Air/[Doki] Air - 03v2.mkv   ← ep 3, Version 2  ┘
```

Every episode (1, 2, 3) is covered by both a v1 and a v2 file — a **complete
collision**. This signals two parallel releases rather than an in-progress patch
series, so the bucket is split into one candidate per version. The comparison
service will rank B higher (higher version wins by default) and mark A as
redundant if it is fully covered.

---

### Specials alongside normal episodes — no collision

> A release that bundles episode 1, episode 2, and special 1 together

```
Air/[Doki] Air - 01 (BD FLAC).mkv     ← ep 1  ┐
Air/[Doki] Air - 02 (BD FLAC).mkv     ← ep 2  ├ one candidate
Air/[Doki] Air - S01 (BD FLAC).mkv    ← S1    ┘
```

Episode identifiers are `(type, number)` pairs: `(Episode, 1)` and `(Special, 1)` are
distinct, so special 1 does not collide with episode 1. All three files belong to
the same candidate.

The episode identifier format mirrors the AniDB convention used throughout Shoko:
a bare number for normal episodes (`1`, `12`), and a letter prefix for other types
(`S1` = special 1, `C2` = credits 2, `T1` = trailer 1, `P1` = parody 1, `O1` = other 1).

---

### Same group, different source → two candidates

> Air also has a DVD-only episode 13 from the same Doki group

```
Air/[Doki] Air - 01  (1280x720 Hi10P BD FLAC) ...  ← Source = BluRay  ┐ candidate A
Air/[Doki] Air - 12  (1280x720 Hi10P BD FLAC) ...  ← Source = BluRay  ┘
Air/[Doki] Air - 13  (720x480 h264 DVD AC3)   ...  ← Source = DVD       candidate B
```

`Source` is part of the key, so the DVD episode is always its own candidate.

---

### Different series folders → two candidates

> SubsPlease releasing both One Piece and Naruto

```
One Piece/[SubsPlease] One Piece - 01 [1080p].mkv   ┐ candidate A
One Piece/[SubsPlease] One Piece - 02 [1080p].mkv   ┘
Naruto/[SubsPlease] Naruto - 01 [1080p].mkv          candidate B
```

Even though every other signal (group, source, languages, codec) matches, the
different parent directory means two candidates.

---

### Different groups in the same folder → two candidates

> Both Doki and Coalgirls have BD encodes of Clannad in the same directory

```
Clannad/[Doki]      Clannad - 01 (BD 1080p FLAC) ...   candidate A
Clannad/[Coalgirls] Clannad - 01 (1920x1080 Hi10P BD FLAC) ...  candidate B
```

`GroupID` differs, so they are separate.

---

### Missing language tag on one file — merges into the group

> Steins;Gate BD rip by FFF: episode 01 and 03 have `ja` in their SRI, but episode 02 was submitted with no language recorded

```
Steins Gate/[FFF] Steins Gate - 01 (BD FLAC) [AAA].mkv  ← AudioLangs=[ja]  ┐
Steins Gate/[FFF] Steins Gate - 02 (BD FLAC) [BBB].mkv  ← AudioLangs=[]    ├ one candidate
Steins Gate/[FFF] Steins Gate - 03 (BD FLAC) [CCC].mkv  ← AudioLangs=[ja]  ┘
```

Episode 02's empty language list is a wildcard — it does not conflict with `[ja]`.
All three files land in the same candidate.

---

### Corrupt file is the only option — included in the release

> Air ep 2 was marked corrupt by AniDB; the user has no other copy

```
Air/[Doki] Air - 01.mkv     ← ep 1, not corrupt  ┐
Air/[Doki] Air - 02.mkv     ← ep 2, corrupt       ├ one candidate  IsCorrupted=true
Air/[Doki] Air - 03.mkv     ← ep 3, not corrupt  ┘
```

Each episode is covered by exactly one file, so there is no collision. Mixed quality
flags within a single release are normal — only when there is an alternative (all
episodes collide) does the quality tier matter. The corrupt ep 2 file stays in the
candidate and sets `IsCorrupted=true` on the aggregate via "any true" semantics.

---

### Quality-tier split on full collision (same version, corrupt vs clean)

> The user somehow has two complete copies of a batch — one clean, one corrupt — all at the same version number

```
Air/[Doki] Air - 01.mkv         ← ep 1, v1, clean  ┐
Air/[Doki] Air - 02.mkv         ← ep 2, v1, clean  ├ candidate A  IsCorrupted=false
Air/[Doki] Air - 03.mkv         ← ep 3, v1, clean  ┘
Air/[Doki] Air - 01 [bad].mkv   ← ep 1, v1, corrupt ┐
Air/[Doki] Air - 02 [bad].mkv   ← ep 2, v1, corrupt ├ candidate B  IsCorrupted=true
Air/[Doki] Air - 03 [bad].mkv   ← ep 3, v1, corrupt ┘
```

Every episode has two files at the same version, but the files differ on quality.
The grouper falls through to a **quality-tier split**: one candidate for the clean
set, one for the corrupt set. The comparison service will rank A higher
(`IsCorrupted` signal: clean beats corrupt) and mark B as redundant.

The quality tier is determined by `(IsCorrupted, IsChaptered)`. If both signals
agree between the colliding files, the split is skipped and everything is kept
together (ambiguous — e.g. a combined-episode file covering the same episode as a
single-episode file).

---

### 1080p and 720p from the same group — airing series

> A series is still airing; the user has both 1080p and 720p batches through episode 4 out of 12

```
Show/[Group] Show - 01 [1080p].mkv  ← ep 1  ┐
Show/[Group] Show - 02 [1080p].mkv  ← ep 2  ├ candidate A  Resolution=1080p
Show/[Group] Show - 03 [1080p].mkv  ← ep 3  │
Show/[Group] Show - 04 [1080p].mkv  ← ep 4  ┘
Show/[Group] Show - 01 [720p].mkv   ← ep 1  ┐
Show/[Group] Show - 02 [720p].mkv   ← ep 2  ├ candidate B  Resolution=720p
Show/[Group] Show - 03 [720p].mkv   ← ep 3  │
Show/[Group] Show - 04 [720p].mkv   ← ep 4  ┘
```

Resolution is a hard separator, so A and B are two candidates regardless of source
or version. The comparison service ranks A higher (1080p > 720p). Both cover the
same four episodes so `B.EpisodeCoverage ⊆ A.EpisodeCoverage` is true → B is
redundant and can be deleted when `AllowDeletion = true`.

When ep 5 arrives for both, the check is run again: 1080p still fully covers 720p
→ B remains redundant. If ep 5 arrives only in 720p, 1080p's coverage is now
missing ep 5, so `B.EpisodeCoverage ⊄ A.EpisodeCoverage` and B is no longer
redundant until 1080p catches up.

---

### Multiple groups, airing series — per-file deletion

> Three groups are releasing the same show. Group A (HEVC) is preferred but is
> two episodes behind Group C (H264). Group B has one episode and ranks below A.

```
show/[a] ep01 hevc gerdub.mkv   ← ep 1, HEVC, German dub  ┐ candidate A
show/[a] ep02 hevc gersub.mkv   ← ep 2, HEVC, German sub  ┘
show/[b] ep01 h264 gersub.mkv   ← ep 1, H264, German sub    candidate B
show/[c] ep01 h264 engsub.mkv   ← ep 1, H264, English sub ┐
show/[c] ep02 h264 engsub.mkv   ← ep 2, H264, English sub ├ candidate C
show/[c] ep03 h264 engsub.mkv   ← ep 3, H264, English sub ┘
```

Grouping creates three candidates (different groups / codecs / language sets).
The comparison service (with default `VideoCodecOrder = [HEVC, H264, ...]`) ranks
A first. Series is still airing and `PerFileDeletionForAiringSeries = true`:

| File | File episode coverage | ⊆ A's coverage {1,2}? | Action |
|------|-----------------------|------------------------|--------|
| B ep1 | {1} | yes | **delete** |
| C ep1 | {1} | yes | **delete** |
| C ep2 | {2} | yes | **delete** |
| C ep3 | {3} | no  | **keep** |

Result:

```
show/[a] ep01 hevc gerdub.mkv   ← kept (primary)
show/[a] ep02 hevc gersub.mkv   ← kept (primary)
show/[c] ep03 h264 engsub.mkv   ← kept (not yet covered by A)
```

When ep 3 arrives for Group A, `CheckAndAutoManage` runs again: A now covers
{1,2,3} and C ep3's coverage {3} ⊆ {1,2,3} → C ep3 is deleted.

---

### Unrecognized file, same MediaInfo — merges into SRI-backed group

> Air BD rip by Doki: episodes 01–02 are in AniDB, episode 03 is not

```
Air/[Doki] Air - 01 (1280x720 Hi10P BD FLAC).mkv  ← has SRI, GroupID=584  ┐
Air/[Doki] Air - 02 (1280x720 Hi10P BD FLAC).mkv  ← has SRI, GroupID=584  ├ one candidate
Air/[Doki] Air - 03 (1280x720 Hi10P BD FLAC).mkv  ← no SRI                ┘
HasReleaseInfo = false  (not every file has SRI)
GroupShortName = "Doki" (from the SRI-backed representative)
```

Episode 03 has no group info, but its codec and resolution match the existing
Doki bucket. A missing group field is a wildcard, so it joins the bucket.
`HasReleaseInfo` becomes `false` because not every file has an SRI record.

---

### Unrecognized file, different codec — stays separate

> The same folder contains a recognised H264 release and an unrecognised HEVC file

```
Show/[Group] Show - 01 (h264).mkv  ← SRI, H264  ┐ candidate A (H264)
Show/[Group] Show - 02 (h264).mkv  ← SRI, H264  ┘
Show/[Group] Show - 03 (hevc).mkv  ← no SRI, HEVC  candidate B (HEVC)
```

Codec is a hard separator when both values are present. Even though the HEVC
file has no group info (which would normally be a wildcard), the codec conflict
with the existing bucket means a new bucket is created.

---

### Unrecognized files — MediaInfo fallback

> A folder of files that were never submitted to AniDB

When there is no `StoredReleaseInfo`, group and source are treated as empty
strings and language is read from the MediaInfo streams. Files in the same folder
with the same codec / resolution / audio codec are still grouped together:

```
Unknown Show/episode01.mkv   (AVC 1080p 10-bit FLAC Matroska)  ┐
Unknown Show/episode02.mkv   (AVC 1080p 10-bit FLAC Matroska)  ├ one candidate
Unknown Show/episode03.mkv   (AVC 1080p 10-bit FLAC Matroska)  ┘
HasReleaseInfo = false
```

---

### Mixed codec folder — two unrecognized candidates

> A directory that contains files from two different encode tools

```
Mixed/[OldGroup] Show - 01 [h264].mkv   (AVC)    candidate A
Mixed/[NewGroup] Show - 01 [hevc].mkv   (HEVC)   candidate B
```

Even without provider metadata, the codec difference splits them.

---

### Partially recognized folder — merges into one candidate

> A group whose episode 03 was never submitted to AniDB, so only episodes 01–02 have a StoredReleaseInfo

```
Show/[FFF] Show - 01 (BD 1080p FLAC) [AAA].mkv  ← has SRI  ┐
Show/[FFF] Show - 02 (BD 1080p FLAC) [BBB].mkv  ← has SRI  ├ one candidate  HasReleaseInfo = false
Show/[FFF] Show - 03 (BD 1080p FLAC) [CCC].mkv  ← no SRI   ┘
```

When codec, resolution, audio codec, and container all match, the unrecognized
file's empty group key is a wildcard and it merges into the SRI-backed bucket.
`HasReleaseInfo` is `false` because not every file has an SRI record, but the
group metadata fields (`GroupName`, etc.) are populated from the first SRI-backed
file.

---

### Different import folders

> The same file physically duplicated across two library roots

Files in different `ManagedFolderID` values are **always** separate candidates,
even when every other signal (including the relative path) is identical.

---

### Different resolutions from the same group

> HorribleSubs releasing both 1080p and 720p batches

```
SAO/[HorribleSubs] SAO - 01 [1080p].mkv  → 1080p candidate
SAO/[HorribleSubs] SAO - 01 [720p].mkv   → 720p  candidate
```

Resolution is bucketed (via `MediaInfoUtility.GetStandardResolution`) and
included in the key.

---

## `HasReleaseInfo`

`VideoReleaseCandidate.HasReleaseInfo` is `true` only when **every** file in the
candidate has a `StoredReleaseInfo` record. A single unrecognized file in a
group is enough to set it to `false` for that candidate.

The group identity fields (`GroupID`, `GroupName`, `GroupShortName`, `GroupSource`)
are populated from the **first SRI-backed file** in the candidate, or are
`null`/default when no file has an SRI record. Quality fields use majority voting
(see [Aggregate quality fields](#aggregate-quality-fields-on-a-candidate)).

---

## API

### Grouping service

```csharp
// Standard path — resolves from repositories internally
IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<VideoLocal_Place> places);

// Pre-resolved path — useful when data is already loaded (e.g. in tests)
IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<ResolvedVideoPlace> resolved);
```

`ResolvedVideoPlace` is a plain record:

```csharp
record ResolvedVideoPlace(VideoLocal_Place Place, VideoLocal Video, StoredReleaseInfo? ReleaseInfo);
```

### Comparison service

```csharp
// -1 = a wins, 0 = tied, 1 = b wins
int Compare(VideoReleaseCandidate a, VideoReleaseCandidate b);

// Best-first; stable on ties (input order preserved)
IReadOnlyList<VideoReleaseCandidate> Rank(IEnumerable<VideoReleaseCandidate> candidates);

// Returns candidates whose EpisodeCoverage ⊆ primary.EpisodeCoverage and who rank lower.
// Single-candidate input always returns empty.
IReadOnlyList<VideoReleaseCandidate> GetRedundantCandidates(IReadOnlyList<VideoReleaseCandidate> ranked);

// Like Compare, but also returns which signal decided the outcome and the display
// values on both sides, for use in UI explanations.
CompareDecision CompareWithDecision(VideoReleaseCandidate a, VideoReleaseCandidate b);

record CompareDecision(
    int Result,
    ReleaseSignalType? DecidingSignal,
    string? PrimaryValue,   // winner's value for the deciding signal
    string? RunnerUpValue); // loser's value for the deciding signal
```

### HTTP endpoint

```
GET /api/v3/Episode/{episodeID}/PrimaryRelease
```

Groups all files for the episode's series, filters to candidates that cover the
given episode, ranks them, and returns the primary with a human-readable
explanation.

**Response (`200 OK`):**

| Field | Description |
|-------|-------------|
| `candidateCount` | Number of candidates covering this episode |
| `primary` | `ReleaseCandidateSummary` — key quality fields of the winning candidate |
| `reason` | `"OnlyRelease"` or `"Ranked"` |
| `decidingSignal` | Name of the signal that broke the tie; null when `reason` is `OnlyRelease` or all signals tied |
| `primaryValue` | Winner's value for `decidingSignal` (e.g. `"BluRay"`, `"1080p"`) |
| `runnerUpValue` | Runner-up's value for `decidingSignal` |

`ReleaseCandidateSummary` includes: `key`, `groupName`, `groupShortName`,
`source`, `resolution`, `videoCodec`, `bitDepth`, `audioCodec`,
`audioStreamCount`, `subtitleStreamCount`, `isChaptered`, `isCorrupted`,
`isCensored`, `version`, `fileCount`, `episodeCoverage` (Shoko string format),
`audioLanguages`, `subtitleLanguages`.

Returns **404** when no release covers the episode.
