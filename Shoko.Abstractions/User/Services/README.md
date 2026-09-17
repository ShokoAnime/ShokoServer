# User and User Data Services

This folder defines the three services behind users and their data:
`IUserDataService` (watch state, ratings, user tags), `IUserService` (accounts,
authentication, API tokens) and `IAuthenticationThrottleService` (lockout after
repeated authentication failures). All three are implemented by the core and
registered as singletons, so a plugin **consumes** them through constructor
injection. None of them is an extension point, and none is discovered through
`IPluginManager.GetExports<T>()`.

A plugin implements nothing in this folder. `IUser`, `IVideoUserData`,
`IEpisodeUserData`, `ISeriesUserData` and `IGroupUserData` are read models
handed to you, and the `…Update` classes in `User/Update/` are the write side.

---

## The four levels, and how a write travels between them

```
IVideoUserData     one file, one user     playback count, progress position, last played at
      ↕  propagation, both directions, guarded by flags
IEpisodeUserData   one episode, one user  watched state, favourite, user tags, rating
      ↓  stats only
ISeriesUserData    one series, one user   watched counts, votes, favourite, user tags
      ↓  stats only
IGroupUserData     one group, one user    watched counts, user tags
```

Marking a **video** watched marks each episode it maps to watched. Marking an
**episode** watched marks every video behind it. Series and group data are
derived stats and are recomputed rather than written through.

The video-to-episode step is not a simple copy, because a file and an episode
are not one-to-one. The service adds up the percentage of every cross-reference
whose video the user has watched, and flips the episode only once that total
passes **95%**. Two halves of one episode therefore mark it watched only when
both are watched, and marking one half unwatched marks the episode unwatched
again. A file spanning three episodes marks all three.

Both directions are guarded so the propagation cannot loop:
`SetVideoWatchedStatus(..., noEpisodePropagation: true)` and
`SetEpisodeWatchedStatus(..., noVideoPropagation: true)`, which is exactly what
the service passes on its own inner calls.

---

## Marking something watched

The core's own scrobbler is the shortest useful example. It is an
`IPlaybackObserver` that takes the service in its constructor and marks the file
watched once the final unit has been served. Note that an observer sees what the
player *fetched*, not what anyone watched, since players read ahead and seek, so
`Position` is an upper bound on progress rather than a measurement of it:

```csharp
public class LegacyScrobbleObserver(IUserDataService userDataService) : IPlaybackObserver
{
    public string Name => "Legacy Scrobbler";

    public async Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
    {
        if (context.User is null || !context.IsFinalUnit)
            return;

        await userDataService.SetVideoWatchedStatus(context.Video, context.User);
    }
}
```

`SetVideoWatchedStatus(video, user, isWatched, lastPlayedAt, reason, noEpisodePropagation, updateStatsNow)`
is the whole signature. `isWatched: false` clears the watched date, and
`lastPlayedAt` defaults to now when marking watched. `SetEpisodeWatchedStatus`
mirrors it at the episode level.

Tell the service **why** through `reason`. It is carried on the event that goes
out afterwards, so other listeners (yours included) can tell a scrobble from a
manual toggle from a bulk import:

| `VideoUserDataSaveReason` | When |
|---|---|
| `None` | Unspecified. |
| `UserInteraction` | The user did it explicitly. |
| `PlaybackStart`, `PlaybackPause`, `PlaybackResume`, `PlaybackProgress`, `PlaybackEnd` | Player lifecycle. |
| `Import` | Reserved for `ImportVideoUserData`. Passing it to `SaveVideoUserData` silently downgrades it to `None`. |

`EpisodeUserDataSaveReason`, `SeriesUserDataSaveReason` and
`GroupUserDataSaveReason` are separate `[Flags]` enums for the level they belong
to (`LastPlayedAt`, `PlaybackCount`, `IsFavorite`, `UserTags`, `UserRating`,
`SeriesStats`, `GroupStats`, `Import`), so one save can report several reasons
at once.

### Batching: `updateStatsNow`

Every write ends by recomputing the watched stats for the series and the groups
above it, which is the expensive part. When you are writing many entries for one
series, pass `updateStatsNow: false` on all but the last:

```csharp
for (var i = 0; i < episodes.Count; i++)
    await userDataService.SetEpisodeWatchedStatus(episodes[i], user, updateStatsNow: i == episodes.Count - 1);
```

---

## Partial updates

`SaveVideoUserData` takes a `VideoUserDataUpdate` rather than a full record, and
that class tracks which properties you assigned. `ProgressPosition`,
`LastPlayedAt`, `LastVideoStreamIndex`, `LastAudioStreamIndex` and
`LastSubtitleStreamIndex` each have a matching `Has…` flag that flips on
assignment, so setting a property to `null` clears the stored value while
leaving it alone keeps it. Do not read a value back from an update object to
learn the current state; construct it from an existing `IVideoUserData` if you
want to start from what is stored.

```csharp
var update = new VideoUserDataUpdate
{
    ProgressPosition = TimeSpan.FromMinutes(12),  // sets HasProgressPosition
    LastAudioStreamIndex = 1,
};
update.SetClientData("my-player", JToken.FromObject(new { theme = "dark" }));

await userDataService.SaveVideoUserData(video, user, update, VideoUserDataSaveReason.PlaybackProgress);
```

Assigning a negative `ProgressPosition` throws `ArgumentOutOfRangeException` on
the spot rather than at save time. `SetClientData(key, null)` removes a key;
store an explicit JSON null with `JValue.CreateNull()`. `ClearClientData` wipes
every client's data for that video, not just yours.

The other update classes cover their own levels: `EpisodeUserDataUpdate`
(playback count, last played at, favourite, user tags, rating),
`SeriesUserDataUpdate` (favourite, user tags, rating with a vote type),
`GroupUserDataUpdate` (user tags only) and `UserUpdate` (username, password,
admin and AniDB flags, avatar, restricted tags). A property that has to tell
"clear it" from "leave it alone" carries the same kind of flag, spelled
`HasSet…` on the rating and avatar properties. `UserRating` also validates on
assignment: 1 to 10 rounded to one decimal, or `-1`/`null` to unset, otherwise
`ArgumentOutOfRangeException`.

---

## Importing from somewhere else

A plugin syncing watch state in from another service uses the `Import…` methods
rather than the `Save…` ones:

```csharp
await userDataService.ImportEpisodeUserData(episode, user, update, importSource: "MyTracker");
```

The import source is a free-form string naming where the data came from, and it
matters for three reasons:

- It rides along on the event as `ImportSource`, with `IsImport` true, so your
  own sync can recognise its own writes and not bounce them straight back out.
- The core uses it to break exactly that loop for AniDB: an import whose source
  is `"AniDB"` does not sync the resulting watched state back to AniDB MyList.
  A sync plugin wants the same guard for its own source.
- `VideoUserDataSaveReason.Import` is only ever set by these methods. An
  `Import` reason with no source is normalised back to `None` when the event is
  constructed.

---

## Reacting to changes

Subscribe from a service of your own rather than from the `IPlugin` class, which
is constructed without DI during the plugin scan:

```csharp
// Registered in your plugin's RegisterServices, and started as a hosted service
// or resolved by something that is.
public class MyWatchStatePush : IDisposable
{
    private readonly IUserDataService _userDataService;
    private readonly IQueueScheduler _queueScheduler;

    public MyWatchStatePush(IUserDataService userDataService, IQueueScheduler queueScheduler)
    {
        _userDataService = userDataService;
        _queueScheduler = queueScheduler;
        _userDataService.EpisodeUserDataSaved += OnEpisodeUserDataSaved;
    }

    public void Dispose() => _userDataService.EpisodeUserDataSaved -= OnEpisodeUserDataSaved;

    private void OnEpisodeUserDataSaved(object? sender, EpisodeUserDataSavedEventArgs e)
    {
        // Skip anything this plugin wrote itself, or the sync never settles.
        if (e.IsImport && e.ImportSource == "MyTracker")
            return;

        if (!e.Reason.HasFlag(EpisodeUserDataSaveReason.LastPlayedAt))
            return;

        // Off the event's thread: everything downstream goes through the queue.
        _queueScheduler.Enqueue<MyPushJob>(job => (job.EpisodeID, job.UserID) = (e.Episode.ID, e.User.ID));
    }
}
```

`IUserDataService` exposes `VideoUserDataSaved`, `EpisodeUserDataSaved`,
`SeriesUserDataSaved` and `GroupUserDataSaved`. `IUserService` exposes
`UserAdded`, `UserUpdated` and `UserRemoved`, each carrying a
`UserChangedEventArgs` with just the `User`.

Three things to build around:

- **One user action can raise several events.** Marking a file watched raises a
  video event and an episode event, and can raise a series event for the stats.
  Decide which level you care about and ignore the rest, rather than reacting to
  all of them and de-duplicating afterwards.
- **`Reason` is the only way to tell writes apart.** A handler that writes back
  into the service re-enters it. Filter on `Reason`, `IsImport` and
  `ImportSource` before acting.
- **Dispatch differs by level.** `VideoUserDataSaved` is raised inline on the
  thread doing the save, so a slow handler slows the write down. The episode,
  series and group events are handed to a thread-pool task that nobody awaits,
  so they can land after the call that caused them returned, and two of them can
  overlap. A handler that throws is logged and stepped over either way, and
  never fails the write. Keep handlers short and hand the real work to the
  queue.

---

## Ratings, favourites and user tags

Beyond watch state, `IUserDataService` covers the rest of a user's opinions:

| Level | Members |
|---|---|
| Episode | `RateEpisode`, `UnrateEpisode`, `ToggleEpisodeAsFavorite`, `SetEpisodeAsFavorite`, `AddUserTagsForEpisode`, `RemoveUserTagsForEpisode`, `SetUserTagsForEpisode` |
| Series | `RateSeries` (with an optional `SeriesVoteType`, `Permanent` or `Temporary`), `UnrateSeries`, `ToggleSeriesAsFavorite`, `SetSeriesAsFavorite`, and the same three tag methods |
| Group | `AddUserTagsForGroup`, `RemoveUserTagsForGroup`, `SetUserTagsForGroup` |

`Add`/`Remove` are incremental, `Set` replaces the whole set. The `Add` and
`Remove` methods come in `params string[]` and `IEnumerable<string>?`
overloads; the three `Set` methods take `IEnumerable<string>?` only, so pass a
collection rather than loose arguments there.

Rating an episode or series as a user flagged `IsAnidbUser` also queues the
corresponding AniDB vote job. That is a network round trip on a rate limiter, so
it will not have happened by the time the call returns.

### Reads

`GetVideoUserData(video, user)` returns `null` when the user has never touched
that file. The episode, series and group equivalents return a record either way,
so treat a fresh one as "no data" rather than checking for null.
`Get…ForUser(user)` and `Get…For<Entity>(entity)` give the two cross sections.

---

## `IUserService`

Accounts, authentication and API tokens. The members a plugin actually reaches
for:

- **Resolving the caller.** `GetUserFromHttpContext(HttpContext)` in a
  controller of yours, `GetUserFromHubCallerContext(HubCallerContext)` in a
  SignalR hub. Both return `null` when the request is not authenticated.
  `GetApiTokenFromHttpContext` gives the token itself, with its device name and
  expiry.
- **Lookups.** `GetUsers()`, `GetUserByID(int)`, `GetUserByUsername(string)`.
- **Accounts.** `CreateUser(UserUpdate)`, `UpdateUser(IUser, UserUpdate)`,
  `DeleteUser(IUser)`, `ChangeUserPassword`, `ResetUserPassword` (which sets an
  empty password, it does not mail anyone anything). Create and update throw
  `GenericValidationException` when the update does not validate, which reports
  every problem at once rather than failing on the first. `DeleteUser` faults
  with `GenericValidationException` when the user is the last administrator.
  For a user that is not stored it throws **nothing**: it quietly skips the
  delete and still clears that user ID's API tokens and group/series user rows,
  so a wrong ID looks like success. Check `GetUserByID` first if you need to
  know. (`IUserService`'s own XML docs claim `ArgumentException` for both
  cases; the docs are stale, the behaviour above is what the implementation
  does. `GenericValidationException` derives directly from `Exception`, so
  catching `ArgumentException` will not catch it.)
- **API tokens.** `GenerateApiTokenForUser(user, deviceName)` returns the
  existing token for that device if there is one; the overload taking an
  `expiresAt` always creates a new one and requires at least 57 seconds in the
  future. `InvalidateApiDeviceForUser`, `InvalidateApiTokensForUser` and the two
  `InvalidateApiToken` overloads revoke.
- **Authentication.** `AuthenticateUser(username, password)` returns the user or
  `null`. It throttles by username on its own, but see below.

`IUser` also carries the visibility rules: `RestrictedTags` and the three
`IsAllowedToSee` overloads for a group, a series and an AniDB anime. Any list a
plugin shows a user should be filtered through them.

---

## `IAuthenticationThrottleService`

If your plugin exposes an endpoint that checks a credential, use this, in this
order:

```csharp
// 1. Before looking the user up, so an unknown username and a locked-out one
//    answer identically and the response reveals neither.
if (throttleService.ThrottleAuthentication(HttpContext, username) is { } blocked)
    return blocked;   // 429, with Retry-After already set on the response

var user = userService.AuthenticateUser(username, password);
if (user is null)
{
    // 2a. Record the failure against the client.
    throttleService.RegisterFailure(HttpContext);
    return Unauthorized();
}

// 2b. Clear the client's failures on success.
throttleService.Reset(HttpContext);
```

The service tracks failures per client and per user, with an escalating lockout:
`InitialLockout` on the first excess attempt, doubling for each one after,
capped at `MaxLockout`, counting failures within a sliding `AttemptWindow` up to
`MaxFailedAttempts`. Lowering `MaxLockout` shortens lockouts already in flight.

Four things worth knowing:

- **It is shared.** One singleton for the core and every plugin, so a client
  locked out by one endpoint is locked out for all of them.
- **The policy values change at runtime**, whenever the admin saves settings.
  Read the properties when you need them instead of caching them.
- **`ThrottleAuthentication` only reads.** It sets `Retry-After` and logs the
  throttled attempt, but recording the outcome is on you, with
  `RegisterFailure` or `Reset`. The exception is `AuthenticateUser`, which
  already records the outcome for the username itself.
- **`RegisterFailure(IUser)` needs an existing user.** Failures for an unknown
  username can only be registered through `AuthenticateUser`, which is another
  reason to check with `ThrottleAuthentication` before the lookup.

`GetRemainingLockout` has overloads for an `HttpContext`, a `HubCallerContext`
and an `IUser`, for showing a countdown rather than deciding a request. So do
`RegisterFailure` and `Reset`, which is how a hub-based login path reports its
own outcome.

---

For how a plugin is discovered, constructed and registered, see the
[plugin overview](../../README.md).
