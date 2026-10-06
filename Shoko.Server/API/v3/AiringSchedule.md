# Airing schedules, for client authors

Swagger lists the routes and their parameters. This explains the concepts behind
them, so a calendar built on `/api/v3/AiringSchedule` renders what the server
actually means.

An **airing schedule** is one provider's run of a series: optionally narrowed to
a season, on one channel or none, releasing a fixed set of tracks. An **episode
airing** is one entry on such a schedule: a time, the slot it was first
scheduled for, and the delay state the server inferred from the provider's own
line. Everything is read-only over REST except the provider settings, the two
preference lists and the channels' aliases and merges; schedules, airings and
channels belong to the providers that fetch them.

Names go over the wire as declared. Properties keep their `PascalCase` spelling
(`AiredAt`, `IsDelayed`, `TimeZone.IsResolved`) and enum values are their
declared names (`Original`, `Subtitled`, `Poster`). Query parameter names stay
`camelCase`, as everywhere else in v3.

## Reading the airings endpoint

`GET /api/v3/AiringSchedule/Airing?from=…&to=…` returns the airings in a
window, ordered by the time they occupy in it.

### The window

`from` and `to` are date-times that name their offset, in ISO 8601:
`2026-10-04T00:00:00Z` or `2026-10-04T00:00:00+09:00`. They are compared as
instants, `from` inclusive and `to` exclusive, so a client asks for its own
local week by sending its local midnights with its own offset, and the next
week starts where this one ended. A value without a time or an offset, such as
`2026-10-04` or `2026-10-04T00:00:00`, is refused with `400 Bad Request` rather
than read in the server's time zone. A `+` must be sent as `%2B` in a query
string; an unencoded one arrives as a space, which is read as a `+` too.

`from` defaults to the start of today in UTC (to now with `nextOnly`), counting
from `at` when it is given (see *Reading as of a time*), and `to` to a week
after `from`. A `to` before `from` is `400 Bad Request`. Times in the
response are always UTC.

### The filters

- `kind`: comma-delimited, defaults to `Original`. `Original` is the broadcast
  or the platform's own first release, `Subtitled` and `Dubbed` are localised
  releases.
- `language`: comma-delimited `TitleLanguage` values, matched against the
  schedule's tracks. Pass the enum's declared names, as everywhere else in v3:
  `Japanese`, `English`, `Portuguese`, `BrazilianPortuguese`. A language code
  such as `en`, `eng` or `pt-BR` is not a `TitleLanguage` and does not bind.
  The matching itself is per language, not per code, so a track a provider
  reported as `en` and one another reported as `eng` both answer to `English`,
  while `Portuguese` and `BrazilianPortuguese` are two languages.
- `channel`: comma-delimited channel IDs, from `GET
  /api/v3/AiringSchedule/Channel`. Without it, the channels the server hides
  are left out; with it, exactly those channels are read, hidden or not. See
  *Hidden channels*, below.
- `provider`: comma-delimited airing schedule provider IDs, from `GET
  /api/v3/AiringSchedule/Provider`.
- `type`: comma-delimited episode types. Omit it for every type. An unresolved
  airing counts as `Episode`.
- `includeUnresolved`: whether to return the unresolved airings, the ones no
  source lists the episode for yet. On by default. See *Unresolved airings*,
  below.
- `episodeKind`: comma-delimited kinds of showing, matched against each
  airing's `Kind`. Omit it for every kind. See *Advance screenings and
  reruns*, below.
- `inCollection`: the three-state filter on whether the series is in the
  collection, which means it has a shoko series. `only` (the default) keeps the
  series in the collection, `false` keeps the anime not in it, and `true` keeps
  both. An airing whose series resolves to nothing counts as not in the
  collection.
- `includeRestricted`: the three-state filter on restricted (H) series, hidden
  by default.
- `filterID`: a stored filter's ID, keeping only the airings of the Shoko
  series it passes for the current user. See *Narrowing by a filter*, below.
- `entityAnchor`: whose entities the answer is about. See *Entity anchor*,
  below.

So the defaults answer with the series in the collection, files or not, and
`inCollection=true` adds every anime the providers know about whether or not
it is in the collection. A filter narrows the collection further, for example
to the series you have files for.

An item's `Tracks` are the schedule's, repeated on every airing so a row can be
labelled without a second request. A global release is one item listing every
track it matches, not one item per language.

### Unresolved airings

A provider stores an airing by its place on the schedule's numbered line,
`SequenceNumber`, counted from `1`, and only pins it to an episode when the
place would not find it. The episode is worked out on every read, so an airing
for episode 14 of a show AniDB lists only 13 episodes of is still returned,
with `IsResolved: false`. Show it as "episode `Number`" of its series: `Type`
is `Episode`, `Number` is the AniDB episode number where one is known and the
number on the schedule's line otherwise, `IDs.AnidbAnime` and
`IDs.ShokoSeries` name the series, and the episode IDs are `null`. Once the
episode is listed the same airing, with the same ID, comes back resolved.

Two providers' airings of one place are of one episode: an item's
`IDs.AnidbAnime` and `Number` together say which, whether or not either is
resolved, and the calendar groups them under one episode. `includeUnresolved=false`
leaves the unresolved ones out. A series or season read returns the unresolved
airings of its own schedules; an episode read never has any.

### Date-only entries

AniDB knows many episodes only by their air date. Such an episode has no airing,
so by default it is not on the calendar. `includeDateOnly=true` adds one
date-only entry for each AniDB episode whose air date falls in the window and
that no provider has an airing for at all (counting estimates when
`includeEstimates` is on), whatever the other filters leave of its airings.

AniDB gives no air date before 1970: it leaves those episodes undated, or
rarely gives them a 1970-01-01 placeholder. The only regular episode of an
anime that started on a known day by 1970-01-01 takes the anime's start date.
Any other undated regular episode of an anime starting by 1970-01-01 takes the
earliest pre-1970 air date among the episodes linked to it from any other
source, such as TMDb. With no such date there is no entry.

A date-only entry has `IsDateOnly: true` and `AirDate` set to that date, a
calendar date in no particular time zone. It has no time, so `AiredAt`,
`OriginalAiredAt`, `ScheduleID`, `Source`, `Channel`, `TimeZone` and `LinkID`
are `null`, and `Tracks` is empty. `ID` is derived from the episode and is
stable, but `GET /Airing/{airingID}` does not resolve it. `IDs`, `Type`,
`Number`, `VideoCount` and the opt-in display data are filled in as for any
airing.

An entry is in the window when its date falls between the calendar date of
`from` and that of the last instant before `to`, each read in its own offset.
A client sending its local midnights therefore gets exactly the dates of its
own days. The entry sorts at the start of its day in `from`'s offset.

A date-only entry counts as a `Normal` `Original` showing in no particular
language on no channel by no provider: `provider`, `channel` and `language`
leave it out, and so do a `kind` without `Original` and an `episodeKind`
without `Normal`. `type`, `inCollection`, `includeRestricted`, `filterID`
and `entityAnchor` apply as to any airing.

### Next only

`nextOnly=true` reduces the answer to the next airing on air at or starting
after `from` (now, by default), per group. An airing is on air from `AiredAt`
until `EndsAt` (see *Airing now*, below), so an episode stays next while it
airs rather than giving way to the following one the moment it starts. `nextPer` says what a group is, comma-delimited and
combinable:

- `Series` (the default): one airing per series.
- `Channel`: one per channel. Airings with no channel share one group.
- `Kind`: one per track kind. An airing whose schedule releases several kinds
  counts for each of them, and a date-only entry counts as `Original`.

`nextPer=Series,Channel` is the next airing of each series on each of its
channels. Next means the next *new* episode: one that premiered before `from`
(now, for the entity routes), with a real `Normal` airing then on any schedule,
provider or channel, hidden or not, is never next, so a regional channel airing
episode 1 a week after Tokyo did is not "Ep 1 airs in 13 hours" but the next
episode nobody has shown yet. An episode counts as shown once such an airing
has ended. Its late airings are still in a plain range read.
A date-only entry is next until its day is over. In each group the earliest
remaining episode wins, and the group answers with that episode's best airing
by the server's preference, so "Ep 5 airs in 19 hours" shows the channel and
track the server would pick, even when another channel airs it a little
earlier. A delayed airing's original slot is never next, and `to` still bounds
the read, so widen it for a show on hiatus.

The series, episode and schedule airing routes take `nextOnly` and `nextPer`
too, and count from now, or `at`.

### Airing now

Every airing with a time carries `EndsAt`, when its slot ends. It is on air
from `AiredAt` until `EndsAt`, which a client compares with its own clock. A
date-only entry has no slot, so its `EndsAt` is `null`. `Duration` is the
episode's own length, for display: the AniDB
episode's length, else the median length of its anime's regular episodes, or
`null` when neither is known.

The slot is `AiredAt` plus `Duration`, or 24 minutes when `Duration` is
`null`. A streaming channel, or an airing with no channel or a channel of an
unknown type, takes exactly that. A television slot carries ads, so its length
is rounded up to the next 15 minutes (24 minutes take 30, 46 take 60), and it
ends early at the start of the channel's next stored airing, of any anime, when
that comes sooner. A next airing that starts before the episode itself is over
is an overlap and is ignored. A calendar still shows an airing on the day it
started, whatever its end.

### Reading as of a time

Every read that counts from now takes `at`, a date-time with an offset like
`from`, to count from that time instead: the airings and channel airings
routes and the calendar (their default `from`), the series, episode and
schedule airing routes (`nextOnly`) and the season routes (the season under
way, the next airings and `AiringStatus`). It only changes what
counts as now for that read, never anything stored, so a client can check what
a card or a calendar showed, or will show, at any time. It is on every build,
not only debug ones.

### Display data is opt-in

The default item is deliberately slim: IDs, times, flags, the channel, the time
zone, the tracks, `SequenceNumber`, `IsResolved`, the episode's `Type` and
`Number`, and `VideoCount` (how many videos the collection holds for the
episode, `0` when none are or when the airing is unresolved). A calendar week is many episodes of few series, so
anything that costs a lookup is asked for through `include`:

- `EpisodeTitle`: the episode's title.
- `Series`: the series' title and IDs.
- `Poster`: the series' primary image.
- `Thumbnail`: the episode's backdrop, falling back to the series' own.

`Poster` and `Series` resolve once per distinct series in the response;
`Thumbnail` is per episode and therefore the pricier of the two. Both images use
the same `Image` shape as the rest of v3, so URL building is unchanged.

### Time zones

`TimeZone` is an object, not a string: `ID` is always set, `IsResolved` says
whether this host knows the zone, and `DisplayName`, `StandardName`,
`BaseUtcOffset`, `CurrentUtcOffset` and `SupportsDaylightSavingTime` are filled
in when it does. Times themselves are always UTC; the zone is what a client
needs to render "23:30 JST" without a second lookup, and an unresolvable id
still comes back so it can be shown as-is. `GET
/api/v3/AiringSchedule/TimeZone` lists the zones a schedule may use, all
resolved.

### One channel, and one schedule

`GET /api/v3/AiringSchedule/Channel/{channelID}/Airing` is the same range read
narrowed to one channel, and filters the same way: `from`, `to`, `provider`,
`type`, `inCollection`, `includeRestricted`, `filterID`,
`includeEstimates`, `includeDelayedOriginalSlots`, `preferredOnly`, `nextOnly`,
`nextPer`, `episodeKind` and `entityAnchor` all mean what they mean on
`/Airing`, with the same defaults. It has no `includeDateOnly`, since a
date-only entry is on no channel, and it answers for a hidden channel, since it
names one.

`kind` is the one deliberate difference: it defaults to every kind rather than
to `Original`, because naming a channel has already narrowed the read and a
streaming channel carries no `Original` track at all, so the calendar's default
would answer nothing for one.

`GET /api/v3/AiringSchedule/{scheduleID}/Airing` is one provider's whole line
for one run: every episode it covers, in airing order, with no window and no
channel filter of its own, since a schedule has exactly one channel. It answers
for a schedule on a hidden channel too. `nextOnly` and `nextPer` work there
too, counting from now.

## Preference

Several airings can cover the same episode: two channels, a broadcast and a
simulcast, two providers reporting the same channel. `preferredOnly=true`
reduces the list to one airing per episode, the one the server would pick.

Two providers reporting one channel are collapsed before any of this, whether
or not `preferredOnly` is set, and whether they spell the language `en` or
`eng`. A channel's own repeat broadcasts are not: a late-night rerun is a
second slot the channel listed itself, so it is a second airing with its own
ID, and it is what a read of the day it falls on answers with.

On a range read the window is applied first and the preference second, so
`preferredOnly=true` answers with one airing per episode *that airs in the
window* rather than dropping an episode whose best airing is somewhere else.
An episode airing on AT-X on the 5th and on TBS on the 3rd is on the 5th's
calendar as its AT-X airing, not missing from it.

Without `preferredOnly`, each airing still says whether it is that one:
`IsPreferred` is `true` on the airing `preferredOnly=true` would keep for its
episode in the same read, window and filters included, and `false` on the
episode's other airings, so a client grouping a read by episode can lead each
group with the server's pick. A date-only entry is always preferred, and
`GET /Airing/{airingID}` and `/Airing/{airingID}/Linked` answer `false`, since
they rank no episode's airings.

The server's preference is two ordered lists, both under
`/api/v3/AiringSchedule`:

- `GET`/`PUT /Channel/Priority`: channel IDs, best first.
- `GET`/`PUT /Track/Priority`: `{ Kind, LanguageCode }` entries, best first. A
  `null` language matches any language of that kind.

An empty list means no preference at all, and the earliest airing wins. Track
preference beats channel preference, which beats a real airing over an
estimated one, which beats the earlier time. Provider priority only ranks
sources; it is not where someone would rather watch, which is why these two
lists exist.

## Channels

`GET /api/v3/AiringSchedule/Channel` lists the shared channel registry. A
channel is keyed by its `Type`, its normalised `Name` and its `CountryCode`
(upper-case ISO 3166-1 alpha-2, or `null`), and the name never carries the
country: a client showing two channels called "ABC" tells them apart by their
`CountryCode`. A TV station has its country; a streaming service has one only
when the service itself is regional, and a global one such as Netflix has
`null`. `GET /Channel/ByName?name=…` matches own names, then aliases, among the
channels of `type` and `countryCode`; without `countryCode`, a channel with no
country answers first, then one in any country. With `countryCode` and no
match in it, the one channel without a country that matches answers, an own
name before an alias, unless a channel in another country has its own name: a
channel without a country is one whose country is unknown, or a global
service. A provider registering a TV station in a country that way gives it
the country, and with it a new ID.

Two admin routes manage the registry:

- `PUT /Channel/{channelID}/Aliases` replaces the channel's aliases with the
  list in the body, a JSON array of strings, and returns the channel. A name
  another channel of the same type and country answers to is rejected with a
  `400`, and nothing changes.
- `POST /Channel/{channelID}/Merge` with `{ "SourceIDs": [ … ] }` merges those
  channels into this one and returns it. They must share its type; their
  country may differ, and this one's is kept, except that a TV station
  without a country takes the one every source with a country agrees on, and
  with it a new ID. Their schedules move here
  without any schedule or airing ID changing, their names and aliases become
  this channel's aliases, and they are deleted. In the preferred channels
  this one takes the best position any of them had, and it keeps its own
  hidden state. A provider registering a merged name later lands here.

## Hidden channels

The server can hide channels nobody watches: every airing read that names no
channels leaves out the airings on them. A read that names channels, through
`channel` or the channel and schedule routes, returns exactly those, hidden or
not, so a client showing hidden channels lists the channels it wants. `GET
/api/v3/AiringSchedule/Channel` and the other channel routes say whether a
channel is hidden with `IsHidden`.

- `GET /Channel/Hidden`: the IDs of the hidden channels.
- `PUT /Channel/Hidden` (admin) with the IDs to hide hides those and shows
  every other channel, and answers with the hidden IDs. An unknown ID is
  rejected with a `400`, and nothing changes. An empty list shows them all.

The hidden state lives on each channel, so a re-keyed channel keeps it and a
merge keeps the target's own.

## Estimates

When a schedule has a steady cadence, the server fills in the episodes the
provider has not reported yet. An estimate carries `IsEstimated: true` and is
otherwise shaped like a real airing, with an ID of its own. Nothing is estimated
past a schedule's last covered episode, on a finished schedule, or while the
schedule is on hiatus. Pass `includeEstimates=false` to leave them out.

An episode the schedule's source does not list yet is estimated too, when its
anime is linked to the schedule's series and the anime's episodes linked into
that series agree on one numbering. AniDB episodes 1 and 2 linked to a show's
S1E1 and S1E2 give an offset of 0, so AniDB episode 3 is the schedule's episode
3 and gets its slot. The linked episodes have to form a run with no gaps and a
single offset, a schedule narrowed to a season only learns from the episodes
linked into that season, and only an episode past the last linked one is
placed. No episode on the schedule's source has to exist for this: the
schedule's own numbering and cadence place the estimate, and the AniDB episode
receives it. Its `IDs` name the AniDB and Shoko episodes, so it is ordered,
marked `IsPreferred` and picked as the next airing like any other airing of the
episode. An anime with no linked episodes on that source gets no such estimate.

An estimate's ID is derived from its schedule's and its place on the line
exactly as the ID of a stored airing without a key of its own is, so
`GET /Airing/{airingID}` resolves it and `GET /Airing/{airingID}/Linked`
answers with an empty list, an estimate never being part of a link set. The ID
is stable for as long as the estimate exists, but the estimate itself is
recomputed on every read and its slot can move. When the provider finally
reports the real airing, the estimate is gone: a real airing without a key of
its own takes over the same ID, and one with its own key has a different ID,
while the estimate's stops resolving.

## Delays

`AiredAt` is where an episode airs now; `OriginalAiredAt` is the slot it was
first scheduled for, and is only set once the slot moved. `IsDelayed` marks the
airing whose own slot was postponed; the episodes that merely shifted behind it
are not flagged, because they were not the cause.

By default a range read also returns a delayed airing whose *original* slot
falls in the window (`includeDelayedOriginalSlots`, default `true`). Draw the
card at `AiredAt` when that is in the window, and a "no episode" marker at
`OriginalAiredAt` when that is. One item can produce both, and items are ordered
by whichever of the two falls in the window first. Markers are per channel,
since every airing belongs to exactly one schedule.

## Advance screenings and reruns

`Kind` on an airing says what kind of showing it is: `Normal` for the regular
airing, `Advance` for an advance screening ahead of it, `Rerun` for a repeat
after it that the provider marked, and `DetectedRerun` for one the server
detected itself. This is a property of the airing, not of the schedule's
tracks, so it has nothing to do with the `kind` query filter, which matches the
schedule's track kind (`Original`, `Subtitled`, `Dubbed`). `episodeKind`
filters on it instead, and defaults to every kind: a calendar without reruns
sends `episodeKind=Normal,Advance`, leaving out both `Rerun` and
`DetectedRerun`.

Few providers mark reruns, so the server looks at each schedule as a whole and
reads its `Normal` airings as `DetectedRerun` when the same episodes, matched
through their links, already had a `Normal` airing on another schedule sharing
one of its tracks:

- **Late run.** The schedule's first airing comes 8 weeks or more after the
  earliest of those airings. A regional channel a few weeks behind stays
  `Normal`.
- **Marathon.** One local day of the schedule, in its time zone, holds at
  least 3 of its airings and at least half of them, and the schedule starts at
  least a day after the earliest of those airings. A batch release with no
  earlier showing, such as a streaming drop, stays `Normal`.

Only the schedule's first three episodes are looked up elsewhere. The
provider's own `Advance` and `Rerun` are never changed, and an estimate takes
its schedule's kind. Nothing is stored: the detection runs on every read, so
it follows the schedules as they change.

The server learns a schedule's line from the airings the provider left
`Normal`, detected reruns included. Advance screenings and the provider's
reruns are returned like any other airing, but they are never counted towards
the cadence estimates are drawn from, never flagged as delays or kept as a
hiatus, and never taken as the airing a simulpub is measured from.

## Linked airings

One slot can cover several episodes, e.g. a double bill. Those airings share a
`LinkID` (the ID of the link head) and are adjacent in a list, so a client can
render one card per link. `GET /api/v3/AiringSchedule/Airing/{airingID}/Linked`
returns the whole set, the head first. `LinkID` is `null` for an unlinked
airing, and changes if the head is removed.

## Simulpub

`OffsetFromOriginal` is this airing's time minus the episode's earliest known
real `Original` airing, which is what a client labels as a simulcast ("+1 h") or
a lag ("+14 d"). Advance screenings and the provider's reruns are left out when
that airing is picked, so an early preview never becomes the anchor. It is
`null` when this *is* that airing, or when there is none. A negative offset is
valid.

## Entity anchor

A schedule is stored against whatever entity its provider knew about: an
AniList anime, a TMDB show, a plugin's own series. The same run can therefore be
reached from either side of a link, and `entityAnchor` says which side you want
back. It takes one of three values, spelled in `PascalCase` like every other
enum over this API:

- `Auto` (the default): infer it. A route that takes an entity takes the anchor
  from that entity, so `/api/v3/Series/{seriesID}/AiringSchedule/Airing` and
  `/api/v3/Episode/{episodeID}/AiringSchedule/Airing` anchor to shoko because a
  shoko series and episode is what they were given. A route that takes no entity
  (`/Airing`, `/{scheduleID}/Airing`, `/Channel/{channelID}/Airing`) falls back
  to `Raw`.
- `Raw`: the providers' own entities, exactly as stored. Nothing is dropped for
  having no counterpart in the collection.
- `Shoko`: only what resolves to a shoko entity. An airing whose episode is not
  in the collection drops out, and on the series schedules route a schedule with
  no shoko series behind it does too.

Leaving it off gives `Auto`. Reach for `Shoko` on a range read when you are building something that
can only act on what the collection actually holds, and `Raw` when you want the
provider's view of a run whether or not it has been matched yet.

It is a named value rather than a missing field on purpose, so `"Auto"` reads
back as a deliberate answer. That is also why it is not the `bool?` three-state
that `linkedEntityAirings` uses: a bool has no room for a third name.

## Series and episode routes

- `GET /api/v3/Series/{seriesID}/AiringSchedule`: the schedules covering a
  shoko series, including its seasons' own unless `includeSeasonSchedules=false`.
- `GET /api/v3/Series/{seriesID}/AiringSchedule/Airing` and `GET
  /api/v3/Episode/{episodeID}/AiringSchedule/Airing`: the airings, best first
  for the episode route. Both take `channel`, `provider`, `episodeKind`,
  `includeDateOnly`, `nextOnly` and `nextPer` as `/Airing` does. A date-only entry is added for an episode with an AniDB air
  date, or a linked one before 1970, and no airing at all, and next counts
  from now, so `nextOnly=true` on the series route is a season card's
  countdown.
- `POST …/AiringSchedule/Refresh`: asks every enabled provider to refresh.
  `wait=false` (the default) queues the work and answers `202 Accepted`;
  `wait=true` waits up to `timeout` seconds (60 by default, capped at 300) and
  answers with what each provider did and the schedules afterwards. Disconnect
  cancels the wait, never the queued work; a wait that runs out reports every
  provider as `TimedOut` while the work carries on.

The `linkedEntitySchedules` and `linkedEntityAirings` queries are the same
three-state switch as `linkedEntityImages`: `false` for the entity's own,
`true` to also walk its linked entities, and omitted to let the server decide,
which means linked for shoko entities and own-only for everything else.

## The season view

Two routes serve a season chart of the cached AniDB anime, in the collection or
not, and two more serve them grouped (see [Sections](#sections) and
[By year](#by-year)).

- `GET /api/v3/AiringSchedule/Season` lists the yearly seasons the anime are
  in, newest first, with how many anime are in each (`Count`) and the season
  under way flagged (`IsCurrent`). Upcoming seasons stop at the one after the
  season under way, which is always listed. It takes `type`, `inCollection`,
  `includeRestricted`, `filterID` and `channel`, and `fromYear` to leave
  out older years. `include=Images` adds a `Poster` and a `Backdrop` for each
  season, from its best rated anime starting in it that has a poster.
- `GET /api/v3/AiringSchedule/Season/{year}/{season}` lists the anime of one
  season, `season` being `Winter`, `Spring`, `Summer` or `Fall` in any case,
  by air date. It takes the same anime filters, and `kind` (`Original` by
  default), `provider`, `episodeKind` (`Normal,Advance` by default, leaving
  out reruns) and `includeEstimates` for the airings.

An anime starts in the season of its first regular episode, and is then only
in the calendar quarters holding one of its dated regular episodes. Once it has
an end date, the last three are left out, so a finished show's final few
episodes never carry it into the next season, while a show still running is in
a new season from its first episode there. The stored normal airings of the
regular episodes AniDB gives no date, or does not list yet, count as dated
episodes, from enabled providers and leaving out schedules detected as reruns;
estimates never count. An anime without dated regular episodes is in its start
season alone, and `continuing` means in the season but started before it.

`channel` works the same on both, as the calendar's channel filter: without
it, the seasons hold every anime by the rule above and the airings come from
the visible channels. With it, a season only holds the anime with a stored
airing on those channels, hidden or not, in its calendar quarter, or, for the
season under way and the next, one still to come; the counts and images are
taken among those, and the airings come from those channels alone. Airings are
only kept for the service's retention window, so an old season counts fewer
anime, or none, with a channel filter.

Each anime carries what a card shows: `ID` (AniDB), `ShokoID`, `Type`,
`Title`, `Poster`, `Overview`, `AirDate`, `EndDate`, `EpisodeCount`,
`Restricted`, `Studios`, `SourceMaterial`, up to five genre `Tags`,
`VideoCount`, `StartSeason`, `IsStartSeasonOverridden` (whether a user set the
start season by hand, see [Start season overrides](#start-season-overrides))
and `EpisodeDuration`, and its airing:

- `NextAiring`: the airing the server prefers of the episode on air now, else
  of the next new episode, in the `EpisodeAiring` shape of the airings
  endpoint, or a date-only entry for an episode known only by its AniDB air
  date. `null` when there is none. Its `AiredAt` and `EndsAt` tell the two
  apart, and the anime keeps its place by that airing's start until `EndsAt`.
- `OtherAirings`: that episode's other upcoming airings, the next one on each
  other channel, in airing order.
- `AiringStatus`: `Upcoming` with a next airing, `Finished` without one once
  the end date has passed, else `Unknown`.

The airings are read through each anime's Shoko series, or through the anime
itself outside the collection, so they need no AniDB ID on the airing to be
matched. An airing on a provider's series no AniDB anime is linked to is on
no card.

### Sections

`GET /api/v3/AiringSchedule/Season/{year}/{season}/Sections` returns the same
anime, with the same filters, already grouped and sorted. Each section has an
`ID`, a `Title` to show as its heading and its `Anime`. The default layout is:

| `ID` | `Title` | Takes |
|---|---|---|
| `new` | TV & Web | `TV` and `Web` that started this season, 16 minutes or more an episode |
| `new-half` | TV & Web (Half Length) | the same, under 16 minutes an episode |
| `continuing` | Continuing | `TV` and `Web` that started before this season |
| `movies` | Movies | `Movie` |
| `other` | OVAs & Specials | everything else |

Each anime goes to the first section that takes it, an anime no section takes is
left out, and a section that took nothing is left out. An anime with no known start counts as new, and one with
no known episode length as full length. Within a section the anime with a
scheduled next airing come first, soonest first, then those known only by an
AniDB air date, then the rest by premiere (an unknown one last); each tier
then goes by title.

`POST` to the same route with a body brings a layout of your own, a filter, or
both, taking the same query filters:

```json
{
  "Sections": [
    { "ID": "tv", "Title": "TV", "Types": ["TV", "Web"], "Continuing": false, "HalfLength": null },
    { "ID": "rest", "Title": "Everything else" }
  ],
  "Filter": { "ApplyAtSeriesLevel": true, "Expression": { "Type": "HasVideoFiles" } }
}
```

`Sections` left out or `null` is the default layout. `Types` left out or
`null` takes every type, which makes the section the rest group; a layout
without one leaves the others out. `Continuing` and `HalfLength` are `true`,
`false` or `null` for both. IDs must be unique. `Filter` is optional and takes
the place of `filterID`, which may not be sent with it.

### By year

`GET /api/v3/AiringSchedule/Season/ByYear` takes the filters of
`/api/v3/AiringSchedule/Season` and returns its seasons grouped by year:
each year has its `Year` and its `Seasons`, from winter to fall, and the
years come newest first. A year whose seasons hold no anime at all is left
out; a year's empty seasons stay. `fromYear` leaves out earlier years, but
not the year of the season under way.

### Start season overrides

The rule matches AniDB's own hand-corrected seasons for most anime, but not
all, so an admin may set the season an anime starts in by hand. An override is
kept by AniDB anime ID, never by Shoko series ID: it holds for an anime outside
the collection, for one not yet in the local AniDB cache (it applies once the
anime is fetched), and through its series being removed and added again.

Only the start is overridden. The seasons after it are still worked out from
the episodes, by the same rule:

- The anime starts in the overridden season, and is then in every season the
  rule places it in after that one.
- A start later than the computed one drops the computed seasons up to it. A
  start past every computed season leaves the overridden season alone.
- A start earlier than the computed one keeps every computed season. No season
  is filled in between the two, as for a break.
- An anime with no dates to go by is in the overridden season alone.

The override applies wherever the rule does: the season view and its sections
(an anime started by hand in an earlier season is `continuing`), the season
counts and images, `GET /api/v3/AiringSchedule/Season/ByYear`, the series and
group `YearlySeasons`, the filters' season condition and the AniDB anime list's
`seasons` filter.

| Route | Who | What |
|---|---|---|
| `GET /api/v3/Series/AniDB/{anidbID}/StartSeason` | anyone who may see the anime | `{ "Year", "Season", "IsOverridden", "Computed": { "Year", "Season" } }`. `Year` and `Season` are the start in effect, `null` with neither dates nor an override. `Computed` is the rule's own start, `null` when the anime is not cached or has no dates. `404` when the anime is neither cached nor overridden. |
| `PUT /api/v3/Series/AniDB/{anidbID}/StartSeason` | admin | Sets the override from `{ "Year": 2015, "Season": "Summer" }`, the year from 1900 to 9999. Answers like the `GET`. Setting the season already set changes nothing. |
| `DELETE /api/v3/Series/AniDB/{anidbID}/StartSeason` | admin | Removes the override: `204`, or `404` when none was set. |
| `GET /api/v3/Series/AniDB/StartSeason/Overrides` | anyone | Every override by AniDB anime ID: `AnidbAnimeID`, `Title`, `Year`, `Season`, `CreatedAt`, `UpdatedAt` and `UserID` (who last set it, `null` for the system). `Title` is the anime's preferred title, its series' one when it is in the collection, or `null` when the anime is not cached or the user may not see it. |
| `GET /api/v3/Series/AniDB/StartSeason/Overrides.csv` | anyone | The overrides as CSV. |
| `POST /api/v3/Series/AniDB/StartSeason/Overrides.csv` | admin | Imports a CSV file, sent as a `text/csv` body or as a multipart form field named `file`. |

The CSV file has the header `AnidbAnimeID,Year,Season`, then one override per
line, the season by name:

```csv
AnidbAnimeID,Year,Season
1530,2005,Fall
17860,2024,Winter
```

On import the header is optional, blank lines and lines starting with `#` are
skipped, fields may be quoted, and season names match in any case (`Autumn` is
`Fall`). The answer counts the `Added`, `Updated` and `Unchanged` overrides and
lists the `Rejected` lines, each with its `Line` number, its `Text` and the
`Reason`: the wrong number of fields, an AniDB anime ID that is not a number
above 0, a year outside 1900 to 9999, a season that is not a name, or an AniDB
anime ID an earlier line named already. The other lines are imported all the
same. An import never removes an override the file leaves out.

## The calendar endpoint

`GET /api/v3/AiringSchedule/Calendar?from=…&to=…&timeZone=…` is the airings
endpoint laid out for a month, week or agenda view, which only differ by
range. It takes the same filters, but `includeDateOnly` is on by default, and
`nextOnly` has no place here.

- `timeZone` is an IANA or Windows ID, or a fixed `±HH:MM` offset, and the
  days are its days. Without it they are UTC days; an unknown one answers
  `400`. `from` defaults to the start of today in that zone, and `to` to a
  week after `from`.
- A timed airing goes on the day of its slot when that is in the range, else
  on the day of the slot it was moved out of (with `MovedTo` set), so a delay
  still shows its gap. One shown at its new slot after a delay has
  `MovedFrom`. A date-only entry goes on its AniDB air date, which is a date
  and stays on that day in every zone, with `IsAllDay` set and `Time` at the
  start of its day.
- Each day has its local `Date` and its `Episodes`: the date-only entries
  first, then by time, one per episode. An episode's `Lead` is its preferred
  airing, else its first timed one, and its `Others` are the rest that day in
  the same order. An airing of an unknown episode stands alone.
- `everyChannel=false` leaves `Others` empty, for a view that shows one
  airing per episode.
- Every entry carries the `Airing`, in the shape of the airings endpoint, and
  its `Time` with the zone's offset. Days with nothing on them are left out.

## Narrowing by a filter

The airing reads, the calendar, the season view and the AniDB anime list
(`GET /api/v3/Series/AniDB`) can be narrowed to the Shoko series a filter
passes, the filter of `/api/v3/Filter`, evaluated once per request for the
current user. Anime and airings of series not in the collection are left out,
and series without local files stay unless the filter leaves them out. Group
filters and series filters both work: a group filter keeps every series of the
groups it passes.

- `filterID` names a stored filter. An unknown ID answers `404`.
- `POST` to the calendar (`/Calendar`), the season list (`/Season`), the years
  (`/Season/ByYear`), a season's anime (`/Season/{year}/{season}`) or the
  AniDB anime list (`/api/v3/Series/AniDB`) sends a filter in the body instead,
  in the shape `POST /api/v3/Filter/Preview/Series` takes, with the same query
  parameters as the `GET`. A season's sections take it as `Filter` in their
  body (see [Sections](#sections)).

A filter with a sorting expression decides the order: a season's anime and the
AniDB anime list come in the filter's order, ignoring `orderBy`, and each
section keeps that order. Without one they are sorted as usual. Calendar days
and airings stay in time order either way.

## Live updates

The server keeps the next hour of airings in memory and pushes each minute's
worth as its slot passes, so a calendar or a "now airing" strip stays current
without re-fetching the range every minute. Join the `airing` feed on the
aggregate hub and listen for `airing:episode.aired`:

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/signalr/aggregate?feeds=airing", { accessTokenFactory: () => apiKey })
  .build();

connection.on("airing:episode.aired", ({ AiredAt, Airings }) => {
  for (const airing of Airings) {
    if (airing.IsEstimated) continue;   // a prediction, not a fact
    markAsAired(airing.ID, AiredAt);
  }
});
```

`AiredAt` is the minute that passed, in UTC. `Airings` is a list of the same
`EpisodeAiring` objects the airings endpoint returns, minus the opt-in display
data (`Series`, `EpisodeTitle`, `Poster` and `Thumbnail` are never filled in on
a push), so the same rendering code handles both.

Five things to build around:

- **One message per minute, not per airing.** A simulcast puts several airings
  on the same minute, say the same episode at 11:25 on both テレビ愛知 and
  テレビ東京, and they arrive in one message. Group `Airings` by
  `IDs.ShokoEpisode` if what you want is "this episode aired", by `LinkID` for
  one card per slot, or leave it alone for a row per channel. `Airings` is never
  empty.
- **Estimates are pushed too.** An estimated airing is a prediction, and there
  is **no retraction message** if the estimate later moves. When the real slot
  arrives it is a different airing with its own ID in its own message, so a
  client that treats both as "it aired" shows it twice. Either skip the
  estimates, or key your UI on `ID` and let the real one replace the guess.
- **Nothing is replayed.** A client connecting after a server restart has a gap
  rather than a burst; re-read the range with the airings endpoint on connect if
  the gap matters. The airings endpoint is the pull side of the same filtering,
  so nothing has to be re-implemented to do that.
- **It fires within a minute of the slot**, never before it.
- **Each user gets what they may see.** An airing of a series the user's
  restrictions hide is left out of their copy of the message, and a minute
  with nothing left for them sends them nothing.

## Providers and their sweeps

`GET /api/v3/AiringSchedule/Provider` lists the providers and their settings,
`PUT /Provider/{providerID}` changes one and `POST /Provider` changes several.
A provider with `HasIcon` serves its icon, its own or its plugin's, at
`GET /Provider/{providerID}/Icon`, which needs no API key so an `<img>` can
load it.

Two fields concern sweeping, which is the server walking a provider's whole
source a chunk at a time:

- `IsSwept`: whether the provider supports being swept. `false` for one that
  only answers on-demand refreshes, and every sweep field is meaningless there.
- `SweepInterval`: how long after a sweep finishes before the next one starts,
  as a duration (`"12:00:00"`). It starts at whatever the provider suggests for
  its own source, and is the user's from then on: send it back to change it.
  Values under fifteen minutes are clamped rather than rejected, so read the
  response back rather than assuming what was sent stuck.

A sweep runs as queue jobs, so it shows up in the queue like any other work, and
how long one chunk may run is the server's own and not exposed here. It is a
minute by default and up to ten at the most, so a sweep job sitting in the queue
for minutes is the ordinary case rather than a stuck one. Each
finished chunk pushes `airing:provider.swept` on the same `airing` feed as
`airing:episode.aired`, carrying `ProviderID`, `ProviderName`, `Outcome` (one of
`Completed`, `TimedOut`, `Stopped`, `Cancelled`, `Failed`), `StartedAt`,
`CompletedAt`, `DurationSeconds`, `IsFinished` and `ErrorMessage`. A sweep of a
long source is several chunks and therefore several messages, and `IsFinished`
is what says the last one has arrived. Nothing is stored server-side, so there
is no endpoint to read a sweep's history back from: a client that wants one
keeps it.

## The service's configuration

The preference lists, the cleanup
(`AutoCleanup`, `RetentionMonths`) and the sweep budget (`SweepBudgetSeconds`)
are the airing schedule service's own configuration.
`GET /api/v3/AiringSchedule/Configuration` (admin) answers with its
`ConfigurationInfo`, and its `ID` is what the generic
`/api/v3/Configuration/{configID}` routes take: `GET` it, read its `/Schema`,
`PUT` or `PATCH` it. A client never needs to know the ID up front. The schema
carries each number's limits as `minimum` and `maximum`.

## Knowing when to refetch

The `airing` feed on the aggregate hub carries two events:

- `airing:episode.aired`, once a minute with the airings whose slot passed.
- `airing:provider.swept`, once per finished chunk of a provider's sweep;
  `IsFinished` marks the last chunk of a sweep. A sweep is what writes most
  schedules, so a calendar refetches its window when one finishes.

A refresh asked for through `POST …/AiringSchedule/Refresh` sends no event of
its own; `wait=true` answers when it is done.

## The dashboard calendars

`GET /api/v3/Dashboard/AniDBCalendar` is what the dashboard widget uses. It is the *AniDB* calendar: it selects and orders by the
AniDB air date, covers every episode type, and does not change when a
broadcaster moves an episode to another day. Use it for "what airs this week
according to AniDB", and the airings endpoint for what a channel actually does,
with kinds, channels, tracks, estimates and delay gaps.
