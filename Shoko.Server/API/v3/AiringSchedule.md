# Airing schedules, for client authors

Swagger lists the routes and their parameters. This explains the concepts behind
them, so a calendar built on `/api/v3/AiringSchedule` renders what the server
actually means.

An **airing schedule** is one provider's run of a series: optionally narrowed to
a season, on one channel or none, releasing a fixed set of tracks. An **episode
airing** is one entry on such a schedule: a time, the slot it was first
scheduled for, and the delay state the server inferred from the provider's own
line. Everything is read-only over REST except the provider settings and the two
preference lists; schedules, airings and channels belong to the providers that
fetch them.

Names go over the wire as declared. Properties keep their `PascalCase` spelling
(`AiredAt`, `IsDelayed`, `TimeZone.IsResolved`) and enum values are their
declared names (`Original`, `Subtitled`, `Poster`). Query parameter names stay
`camelCase`, as everywhere else in v3.

## Reading the airings endpoint

`GET /api/v3/AiringSchedule/Airing?startDate=…&endDate=…` returns the airings in
a window, ordered by the time they occupy in it. The window defaults to the next
seven days.

The hard filters:

- `kind` — comma-delimited, defaults to `Original`. `Original` is the broadcast
  or the platform's own first release, `Subtitled` and `Dubbed` are localised
  releases.
- `language` — comma-delimited `TitleLanguage` values, matched against the
  schedule's tracks. Pass the enum's declared names, as everywhere else in v3:
  `Japanese`, `English`, `Portuguese`, `BrazilianPortuguese`. A language code
  such as `en`, `eng` or `pt-BR` is not a `TitleLanguage` and does not bind.
  The matching itself is per language, not per code, so a track a provider
  reported as `en` and one another reported as `eng` both answer to `English`,
  while `Portuguese` and `BrazilianPortuguese` are two languages.
- `channel` — comma-delimited channel IDs, from `GET
  /api/v3/AiringSchedule/Channel`.
- `type` — comma-delimited AniDB episode types. Omit it for every type.
- `includeMissing` / `includeRestricted` — the same three-state filters the rest
  of v3 uses. By default a series nothing has been downloaded for is hidden, and
  restricted (H) series are hidden.
- `entityAnchor` — whose entities the answer is about. See *Entity anchor*,
  below.

An item's `Tracks` are the schedule's, repeated on every airing so a row can be
labelled without a second request. A global release is one item listing every
track it matches, not one item per language.

Only timed airings are returned. An episode known solely by an AniDB date has no
airing and stays on the dashboard calendars.

### Display data is opt-in

The default item is deliberately slim: IDs, times, flags, the channel, the time
zone, the tracks, the episode's `Type` and `Number`, and `VideoCount` — how many
videos the collection holds for the episode, `0` when none are or when no
episode could be resolved. A calendar week is many episodes of few series, so
anything that costs a lookup is asked for through `include`:

- `EpisodeTitle` — the episode's title.
- `Series` — the series' title and IDs.
- `Poster` — the series' primary image.
- `Thumbnail` — the episode's backdrop, falling back to the series' own.

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
narrowed to one channel, and filters the same way: `type`, `includeMissing`,
`includeRestricted`, `includeEstimates`, `includeDelayedOriginalSlots`,
`preferredOnly` and `entityAnchor` all mean what they mean on `/Airing`, and a
series nothing has been downloaded for, or a restricted (H) one, is hidden
unless it is asked for.

`kind` is the one deliberate difference: it defaults to every kind rather than
to `Original`, because naming a channel has already narrowed the read and a
streaming channel carries no `Original` track at all, so the calendar's default
would answer nothing for one.

`GET /api/v3/AiringSchedule/{scheduleID}/Airing` is one provider's whole line
for one run: every episode it covers, in airing order, with no window and no
channel filter of its own, since a schedule has exactly one channel.

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

The server's preference is two ordered lists, both under
`/api/v3/AiringSchedule`:

- `GET`/`PUT /Channel/Priority` — channel IDs, best first.
- `GET`/`PUT /Track/Priority` — `{ Kind, LanguageCode }` entries, best first. A
  `null` language matches any language of that kind.

An empty list means no preference at all, and the earliest airing wins. Track
preference beats channel preference, which beats a real airing over an
estimated one, which beats the earlier time. Provider priority only ranks
sources — it is not where someone would rather watch, which is why these two
lists exist.

## Estimates

When a schedule has a steady cadence, the server fills in the episodes the
provider has not reported yet. An estimate carries `IsEstimated: true` and is
otherwise shaped like a real airing, with an ID of its own. Nothing is estimated
past a schedule's last covered episode, on a finished schedule, or while the
schedule is on hiatus. Pass `includeEstimates=false` to leave them out.

An estimate's ID is derived from its schedule's and its episode's exactly as a
stored airing's is, so `GET /Airing/{airingID}` resolves it and
`GET /Airing/{airingID}/Linked` answers with an empty list, an estimate never
being part of a link set. The ID is stable for as long as the estimate exists,
but the estimate itself is recomputed on every read and its slot can move.
When the provider finally reports the real airing it is a different airing with
its own ID, and the estimate's ID stops resolving, which means the guess is
gone, not the episode.

## Delays

`AiredAt` is where an episode airs now; `OriginalAiredAt` is the slot it was
first scheduled for, and is only set once the slot moved. `IsDelayed` marks the
airing whose own slot was postponed — the episodes that merely shifted behind it
are not flagged, because they were not the cause.

By default a range read also returns a delayed airing whose *original* slot
falls in the window (`includeDelayedOriginalSlots`, default `true`). Draw the
card at `AiredAt` when that is in the window, and a "no episode" marker at
`OriginalAiredAt` when that is. One item can produce both, and items are ordered
by whichever of the two falls in the window first. Markers are per channel,
since every airing belongs to exactly one schedule.

## Linked airings

One slot can cover several episodes, e.g. a double bill. Those airings share a
`LinkID` — the ID of the link head — and are adjacent in a list, so a client can
render one card per link. `GET /api/v3/AiringSchedule/Airing/{airingID}/Linked`
returns the whole set, the head first. `LinkID` is `null` for an unlinked
airing, and changes if the head is removed.

## Simulpub

`OffsetFromOriginal` is this airing's time minus the episode's earliest known
real `Original` airing, which is what a client labels as a simulcast ("+1 h") or
a lag ("+14 d"). It is `null` when this *is* that airing, or when there is none.
A negative offset is valid.

## Entity anchor

A schedule is stored against whatever entity its provider knew about: an
AniList anime, a TMDB show, a plugin's own series. The same run can therefore be
reached from either side of a link, and `entityAnchor` says which side you want
back. It takes one of three values, spelled in `PascalCase` like every other
enum over this API:

- `Auto` (the default) — infer it. A route that takes an entity takes the anchor
  from that entity, so `/api/v3/Series/{seriesID}/AiringSchedule/Airing` and
  `/api/v3/Episode/{episodeID}/AiringSchedule/Airing` anchor to shoko because a
  shoko series and episode is what they were given. A route that takes no entity
  (`/Airing`, `/{scheduleID}/Airing`, `/Channel/{channelID}/Airing`) falls back
  to `Raw`.
- `Raw` — the providers' own entities, exactly as stored. Nothing is dropped for
  having no counterpart in the collection.
- `Shoko` — only what resolves to a shoko entity. An airing whose episode is not
  in the collection drops out, and on the series schedules route a schedule with
  no shoko series behind it does too.

`Auto` is what every existing caller already gets, so leaving it off changes
nothing. Reach for `Shoko` on a range read when you are building something that
can only act on what the collection actually holds, and `Raw` when you want the
provider's view of a run whether or not it has been matched yet.

It is a named value rather than a missing field on purpose, so `"Auto"` reads
back as a deliberate answer. That is also why it is not the `bool?` three-state
that `linkedEntityAirings` uses: a bool has no room for a third name.

## Series and episode routes

- `GET /api/v3/Series/{seriesID}/AiringSchedule` — the schedules covering a
  shoko series, including its seasons' own unless `includeSeasonSchedules=false`.
- `GET /api/v3/Series/{seriesID}/AiringSchedule/Airing` and `GET
  /api/v3/Episode/{episodeID}/AiringSchedule/Airing` — the airings, best first
  for the episode route.
- `POST …/AiringSchedule/Refresh` — asks every enabled provider to refresh.
  `wait=false` (the default) queues the work and answers `202 Accepted`;
  `wait=true` waits up to `timeout` seconds (60 by default, capped at 300) and
  answers with what each provider did and the schedules afterwards. Disconnect
  cancels the wait, never the queued work; a wait that runs out reports every
  provider as `TimedOut` while the work carries on.

The `linkedEntitySchedules` and `linkedEntityAirings` queries are the same
three-state switch as `linkedEntityImages`: `false` for the entity's own,
`true` to also walk its linked entities, and omitted to let the server decide —
which means linked for shoko entities and own-only for everything else.

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

Four things to build around:

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

## The dashboard calendars

`GET /api/v3/Dashboard/AniDBCalendar` is still there, and is still what the
dashboard widget uses. It is the *AniDB* calendar: it selects and orders by the
AniDB air date, covers every episode type, and does not change when a
broadcaster moves an episode to another day. Use it for "what airs this week
according to AniDB", and the airings endpoint for what a channel actually does.

`Dashboard/CalendarEpisodes` is gone. The airings endpoint replaces it with
kinds, channels, tracks, estimates and delay gaps.
