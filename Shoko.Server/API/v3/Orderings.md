# Orderings, for client authors

Swagger lists the routes and their parameters. This covers what they do not
say: how an ordering and its groups get images, and how orderings move between
servers.

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
  the ones named by `orderingIDs` (full IDs, of any kind, so a plugin's or
  TMDB's ordering can be forked) and the local ones of the Shoko series in
  `seriesIDs`. `includeGlobal` adds those series' stored global orderings.
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

The response lists every ordering of the file with its outcome, the episodes
dropped from its groups, and each image as `FromPayload`, `FromUrl`, `Pending`
(its download is queued), `Failed` or `Skipped`, with a reason. A file that
cannot be read at all gives a `400` and changes nothing.
