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
  schedule's tracks. `en` and `eng` are the same language; `pt-BR` and `pt-PT`
  are not.
- `channel` — comma-delimited channel IDs, from `GET
  /api/v3/AiringSchedule/Channel`.
- `type` — comma-delimited AniDB episode types. Omit it for every type.
- `includeMissing` / `includeRestricted` — the same three-state filters the rest
  of v3 uses. By default a series nothing has been downloaded for is hidden, and
  restricted (H) series are hidden.

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

## Preference

Several airings can cover the same episode: two channels, a broadcast and a
simulcast, two providers reporting the same channel. `preferredOnly=true`
reduces the list to one airing per episode, the one the server would pick.

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

The server keeps the next hour of airings in memory and pushes each one as its
slot passes, so a calendar or a "now airing" strip stays current without
re-fetching the range every minute. Join the `airing` feed on the aggregate hub
and listen for `airing:episode.aired`:

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/signalr/aggregate?feeds=airing", { accessTokenFactory: () => apiKey })
  .build();

connection.on("airing:episode.aired", ({ AiredAt, IsEstimated, Airing }) => {
  if (IsEstimated) return;      // a prediction, not a fact
  markAsAired(Airing.ID, AiredAt);
});
```

`Airing` is the same `EpisodeAiring` object the airings endpoint returns, minus
the opt-in display data (`Series`, `EpisodeTitle`, `Poster` and `Thumbnail` are
never filled in on a push), so the same rendering code handles both. `AiredAt`
is the slot that passed, in UTC, and `IsEstimated` mirrors
`Airing.IsEstimated`.

Four things to build around:

- **One message per airing, not per episode.** An episode on three channels
  sends three messages. De-duplicate by `Airing.IDs.ShokoEpisode` if what you
  want is "this episode aired", or by `Airing.LinkID` for one card per slot.
- **Estimates are pushed too.** An estimated message is a prediction, and there
  is **no retraction message** if the estimate later moves. When the real slot
  arrives it is a different airing with its own ID and sends its own message, so
  a client that treats both as "it aired" shows it twice. Either skip the
  estimates, or key your UI on `Airing.ID` and let the real one replace the
  guess.
- **Nothing is replayed.** A client connecting after a server restart has a gap
  rather than a burst; re-read the range with the airings endpoint on connect if
  the gap matters.
- **It fires within a minute of the slot**, never before it.

## The dashboard calendars

`GET /api/v3/Dashboard/AniDBCalendar` is still there, and is still what the
dashboard widget uses. It is the *AniDB* calendar: it selects and orders by the
AniDB air date, covers every episode type, and does not change when a
broadcaster moves an episode to another day. Use it for "what airs this week
according to AniDB", and the airings endpoint for what a channel actually does.

`Dashboard/CalendarEpisodes` is gone. The airings endpoint replaces it with
kinds, channels, tracks, estimates and delay gaps.
