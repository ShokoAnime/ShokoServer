# Orderings, for client authors

Swagger lists the routes and their parameters. This covers what they do not
say: how specials are placed, how an ordering and its groups get images, and
how orderings move between servers.

## Placed specials

An episode in the special group (season 0) and in a regular group is a placed
special. It stays a special, numbered in the special group; its entry in the
regular group only says where it airs. So the regular group numbers its other
episodes without it, and `EpisodeCount` counts it once.

With `includeGroups`, a regular group lists the special where it airs with
`IsSpecial: true` and its special group number, and the special group's entry
carries where it airs. `AirsBeforeSeasonNumber` and `AirsBeforeEpisodeNumber`
name the regular episode of that group that follows it; when none does,
`AirsAfterSeasonNumber` names the group. `AirsAfterEpisodeID` and
`AirsBeforeEpisodeID` are the regular episodes around it, in any group.

```json
[
  { "ID": "user://season/a1", "SeasonNumber": 1, "IsSpecial": false, "Episodes": [
    { "ID": "shoko://episode/11", "ShokoEpisodeID": 11, "EpisodeNumber": 1, "IsSpecial": false },
    { "ID": "shoko://episode/90", "ShokoEpisodeID": 90, "EpisodeNumber": 1, "IsSpecial": true },
    { "ID": "shoko://episode/12", "ShokoEpisodeID": 12, "EpisodeNumber": 2, "IsSpecial": false }
  ] },
  { "ID": "user://season/b2", "SeasonNumber": 0, "IsSpecial": true, "Episodes": [
    { "ID": "shoko://episode/90", "ShokoEpisodeID": 90, "EpisodeNumber": 1, "IsSpecial": true,
      "AirsBeforeSeasonNumber": 1, "AirsBeforeEpisodeNumber": 2,
      "AirsAfterEpisodeID": "shoko://episode/11", "AirsBeforeEpisodeID": "shoko://episode/12" }
  ] }
]
```

To edit, send each group's `EpisodeIDs` as listed: the special in both
groups. The generic metadata routes give the episode one place in that
ordering, in the special group, with the same `Airs*` fields.
The default ordering places specials too without moving them out of season 0:
a plugin's series where its provider said they air, and an AniDB anime or a
Shoko series by titles such as `Episode 17.5`. TMDB's episode groups count
a group numbered 0 as the special group. `GET
/api/v3/Series/{seriesID}/Ordering/preferred` returns the ordering the series
uses, next to `default`.

## Generic titles

Generic titles such as `Episode 6` or `Season 2` are not stored: AniDB's
generic episode titles and TMDB's generic episode titles and season names
are left out when they are saved. An episode or season with no title left
gets one synthesized when it is read, with the source `generated`: `Episode 6`
for an episode (in the first preferred episode naming language that has a
form for it, and `Special 1` and the like for the other types), and `Season
2`, or `Specials` for season 0, for a season. When an episode has real
titles, the title choice prefers them over generic ones.

The TMDB routes do the same for the ordering being shown: an episode TMDB
gave no English title is `Episode {number}` with its number in that
ordering, so it can differ between orderings. TMDB's English title, when it
gives one, is the main title of its entries; the original title is the main
one only when there is no English title.

## Images on an ordering and its groups

`GET /api/v3/Series/{seriesID}/Ordering` returns `Images` on each ordering, and
on each group when `includeGroups` is set. For the default ordering the groups
are the series' own seasons, with their own images; any other ordering starts
with none.

A user's ordering and its groups take images through the generic image
cross-reference endpoints under `/api/v3/Image/Management`. The ordering is
`CrossReference/Entity/user/ordering/{LocalID}` and a group is
`CrossReference/Entity/user/season/{local part of the group's ID}`. Any image
type fits, the same as for a season. Deleting the ordering, dropping a group
from it in an update, or removing the series takes the links along; the images
themselves stay until the orphan purge.

## Moving orderings between servers

Every route here is admin-only.

- `GET /api/v3/Ordering/Export` writes every local ordering on the server, or
  the ones named by `orderingIDs` (full IDs of any kind, so a plugin's or
  TMDB's ordering can be forked, or a user's ordering's local ID) and the
  local ones of the Shoko series in `seriesIDs`. `includeGlobal` adds those
  series' stored global orderings, and `includePreferred` (on by default)
  records which ordering each series uses.
- `GET /api/v3/Series/{seriesID}/Ordering/Export` does the same for one
  series; `orderingIDs` there also takes a local ID or `default`.
- `images` is `None`, `UrlOnly` (the default), `EmbedMissingRemote` (files
  only for images with no remote source, such as uploads) or `EmbedAll`.
  `container` is `Auto` (zip when embedding, else JSON), `Json` (files inline
  as base64) or `Zip`.
- `POST /api/v3/Ordering/Import` takes the file as a multipart form field
  named `file`; `POST /api/v3/Ordering/Import/Raw` takes it as the body. JSON
  and zip are told apart by their content.

An import always makes local orderings. It finds each series by its AniDB
anime and each episode by its AniDB episode, falling back to its type and
number when the episode is of that same anime, and reports what it could not
find. `conflict` decides what happens to a local ordering of the series with
the same name, ignoring case: `Skip` (the default), `Replace` (same ID and
choice, new groups and images) or `KeepBoth` (the import is named
`Name (2)`). `images` is `PayloadFirst` (the
default), `UrlFirst`, `PayloadOnly`, `UrlOnly` or `None`; `verifyHashes`
refuses a file whose SHA-256 does not match; `applyPreferred` chooses each
ordering that was chosen where it came from; `dryRun` writes nothing.

Each ordering of the file lists its networks in `networks`, by their full IDs
such as `tmdb://network/82`, and nothing else about them. An import links the
local ordering to them in that order. A network this server does not have yet
is kept as a stub, with an empty `Name`, until its source saves it, such as
when a series it aired is refreshed. A network on a source this server does
not know is left out with a note. A file without `networks` makes an ordering
with none, and `Replace` then keeps the networks the local ordering had.

The response lists every ordering of the file with its outcome, the episodes
dropped from its groups, the `Networks` it was linked to and the
`StubbedNetworks` among them, and each image as `FromPayload`, `FromUrl`,
`Pending` (its download is queued), `Failed` or `Skipped`, with a reason. A dry
run lists the networks it would link and stub. A file that cannot be read at
all gives a `400` and changes nothing.
