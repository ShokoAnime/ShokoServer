# User and User Data Services

The services behind users and their data: `IUserDataService` (watch state,
ratings, user tags), `IUserService` (accounts, authentication, API tokens),
`IActorContext` (who the current work is done for) and
`IAuthenticationThrottleService` (lockout after repeated failures). The core
implements them as singletons; a plugin **consumes** them through constructor
injection and implements nothing here. `IUser` and the `I…UserData` interfaces
are read models, and the `…Update` classes in `User/Update/` are the write side.

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

A file and an episode are not one-to-one, so the service adds up the
percentage of every cross-reference whose video the user has watched, and flips
the episode only once that total passes **95%** (a fixed share of the episode,
not the [completion threshold](#the-completion-rule) of a single video): two
halves of one episode mark it watched only when both are watched. A file
spanning three episodes marks all three. `noEpisodePropagation` and `noVideoPropagation` stop the propagation, as
the service's own inner calls do.

---

## Marking something watched

```csharp
await userDataService.SetVideoWatchedStatus(video, user, reason: VideoUserDataSaveReason.PlaybackEnd);
```

The core's own scrobbler, an `IPlaybackObserver`, does this once the final unit
of a stream has been served (see [Streaming](../../Video/Streaming/README.md)).

`isWatched: false` clears the watched date, and `lastPlayedAt` defaults to now
when marking watched. `SetEpisodeWatchedStatus` mirrors it at the episode level.

Tell the service **why** through `reason`. It is carried on the event that goes
out afterwards, so listeners can tell a scrobble from a manual toggle from an
import:

| `VideoUserDataSaveReason` | When |
|---|---|
| `None` | Unspecified. |
| `UserInteraction` | The user did it explicitly. |
| `PlaybackStart`, `PlaybackPause`, `PlaybackResume`, `PlaybackProgress`, `PlaybackEnd` | Player lifecycle. |
| `Import` | Reserved for `ImportVideoUserData`. Passing it to `SaveVideoUserData` silently downgrades it to `None`. |

`EpisodeUserDataSaveReason`, `SeriesUserDataSaveReason` and
`GroupUserDataSaveReason` are `[Flags]` enums for their level (`LastPlayedAt`,
`PlaybackCount`, `IsFavorite`, `UserTags`, `UserRating`, `SeriesStats`,
`GroupStats`, `Import`), so one save can report several reasons at once.
`UserSaveReason`, on the user added and updated events, is the same for an
account (`Username`, `Password`, `IsAdmin`, `IsAnidbUser`, `RestrictedTags`).

### Batching: `updateStatsNow`

Every write ends by recomputing the watched stats of the series and groups
above it, the expensive part. Writing many entries for one series, pass
`updateStatsNow: false` on all but the last:

```csharp
for (var i = 0; i < episodes.Count; i++)
    await userDataService.SetEpisodeWatchedStatus(episodes[i], user, updateStatsNow: i == episodes.Count - 1);
```

---

## Partial updates

`SaveVideoUserData` takes a `VideoUserDataUpdate`, which tracks which
properties you assigned: each nullable one has a `Has…` flag that flips on
assignment, so setting it to `null` clears the stored value while leaving it
alone keeps it. Construct one from an existing `IVideoUserData` to start from
what is stored.

```csharp
var update = new VideoUserDataUpdate
{
    ProgressPosition = TimeSpan.FromMinutes(12),  // sets HasProgressPosition
    LastAudioStreamIndex = 1,
};
update.SetClientData("my-player", JToken.FromObject(new { theme = "dark" }));

await userDataService.SaveVideoUserData(video, user, update, VideoUserDataSaveReason.PlaybackProgress);
```

`SetClientData(key, null)` removes a key; `ClearClientData` wipes every
client's data for that video, not just yours.

### The completion rule

A saved `ProgressPosition` at or past the server's completion threshold counts
as a finished playthrough: the position is cleared and the video is marked
watched. The threshold is the admin's `CompletionThresholdPercent` setting, a
share of the video's runtime from 50 to 100, and 95 by default.

By default the rule applies only to saves whose reason is `PlaybackEnd`,
`UserInteraction` or `None`. A progress, pause or resume save stores the
position as given, so a player reporting progress near the end does not mark
the video watched before playback has ended. Set
`VideoUserDataUpdate.ApplyCompletionThreshold` to `true` or `false` to force
the rule on or off for one save, for instance on a player that decides on its
own when a playthrough is over. Saving past the threshold again with nothing
played since does not count as a second watch.

The other update classes cover their own levels: `EpisodeUserDataUpdate`,
`SeriesUserDataUpdate`, `GroupUserDataUpdate` and `UserUpdate`, with the same
kind of flag (spelled `HasSet…` on the rating and avatar properties). Values are
validated on assignment, so a bad one throws where it is set, not at save time.

---

## Importing from somewhere else

A plugin syncing watch state in from another service uses the `Import…`
methods rather than the `Save…` ones:

```csharp
await userDataService.ImportEpisodeUserData(episode, user, update, importSource: "MyTracker");
```

The import source is a free-form string naming where the data came from. It
rides along on the event as `ImportSource`, with `IsImport` true, so a sync can
recognise its own writes and not bounce them back out. The core does exactly
that for AniDB: an import from `"AniDB"` is not synced back to the AniDB MyList.

---

## Reacting to changes

Subscribe from a service of your own rather than from the `IPlugin` class, which
is constructed without DI during the plugin scan:

```csharp
// Registered in RegisterServices, and started as a hosted service.
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
        _queueScheduler.Enqueue<MyPushJob>(job => (job.EpisodeID, job.UserID) = (e.Episode.LocalID, e.User.LocalID));
    }
}
```

`IUserDataService` exposes `VideoUserDataSaved`, `EpisodeUserDataSaved`,
`SeriesUserDataSaved` and `GroupUserDataSaved`. `IUserService` exposes
`UserAdded`, `UserUpdated` and `UserRemoved`, each a `UserChangedEventArgs` with
the `User`, the `Reason` (`UserSaveReason`) and the `Actor`, and
`ApiTokenGenerated` and `ApiTokenInvalidated` (see below).

- **One user action can raise several events.** Marking a file watched raises a
  video event, an episode event and possibly a series event. Pick the level you
  care about.
- **Filter on `Reason`, `IsImport` and `ImportSource`** before acting; a handler
  that writes back into the service re-enters it.
- **Dispatch differs by level.** `VideoUserDataSaved` is raised inline on the
  saving thread. The episode, series and group events run on an unawaited
  thread-pool task, so they can land after the call returned and overlap. A
  throwing handler is logged and never fails the write. Hand real work to the
  queue.

---

## Ratings, favourites and user tags

Beyond watch state, `IUserDataService` covers the rest of a user's opinions:

| Level | Members |
|---|---|
| Episode | `RateEpisode`, `UnrateEpisode`, `ToggleEpisodeAsFavorite`, `SetEpisodeAsFavorite`, `AddUserTagsForEpisode`, `RemoveUserTagsForEpisode`, `SetUserTagsForEpisode` |
| Series | `RateSeries` (with an optional `SeriesVoteType`, `Permanent` or `Temporary`), `UnrateSeries`, `ToggleSeriesAsFavorite`, `SetSeriesAsFavorite`, and the same three tag methods |
| Group | `AddUserTagsForGroup`, `RemoveUserTagsForGroup`, `SetUserTagsForGroup` |

`Add`/`Remove` are incremental, `Set` replaces the whole set. Rating as a user
flagged `IsAnidbUser` also queues the AniDB vote, which has not happened by the
time the call returns.

### Reads

`GetVideoUserData(video, user)` returns `null` when the user has never touched
that file; the episode, series and group equivalents return a fresh record
instead. `Get…ForUser(user)` and `Get…For<Entity>(entity)` give the two cross
sections.

---

## `IUserService`

Accounts, authentication and API tokens. The members a plugin reaches for:

- **Resolving the caller.** `GetUserFromHttpContext(HttpContext)` in a
  controller of yours, `GetUserFromHubCallerContext(HubCallerContext)` in a
  SignalR hub. Both return `null` when the request is not authenticated.
  `GetApiTokenFromHttpContext` gives the token itself, with its device name and
  expiry.
- **Lookups.** `GetUsers()`, `GetUserByID(int)`, `GetUserByUsername(string)`.
- **Accounts.** `CreateUser(UserUpdate)`, `UpdateUser(IUser, UserUpdate)`,
  `DeleteUser(IUser)`, `ChangeUserPassword`, `ResetUserPassword` (sets an empty
  password). A failed validation throws `GenericValidationException` (not an
  `ArgumentException`) with every problem at once; `DeleteUser` faults with it
  for the last administrator. Deleting a user that is not stored is not an
  error: its tokens and user rows are still cleared, and no `UserRemoved` is
  raised.
- **API tokens.** `GenerateApiTokenForUser(user, deviceName)` returns the
  existing token for that device if there is one; the overload taking an
  `expiresAt` always creates a new one. The `InvalidateApi…` methods revoke.
- **Token events.** `ApiTokenGenerated` fires when a token is created, never
  when an existing one is handed back. `ApiTokenInvalidated` fires once per
  token the `InvalidateApi…` methods or `DeleteUser` remove; expired tokens
  cleaned up on their own raise nothing. The token's key is ignored by every
  serializer, so logging the args is safe.
- **Authentication.** `AuthenticateUser(username, password)` returns the user or
  `null`. It throttles by username on its own, but see below.

`IUser` also carries the visibility rules: `RestrictedTags` and the three
`IsAllowedToSee` overloads for a group, a series and an AniDB anime. Any list a
plugin shows a user should be filtered through them.

---

## `IActorContext`: who did it

Some events carry an `Actor`: the `ApiToken` (user and device) of whoever
caused them, or `null` when the system did. The core stamps it when the event is
raised, from `IActorContext.Current`, which it sets itself:

- for a request, right after authentication, from the request's token;
- for a SignalR hub call, and for connecting and disconnecting, from the
  connection's token;
- for a queued job, from the user and device stored with the job (never the
  key), looked up again when it runs: a token revoked since runs the job for
  the system. Jobs queued from a job inherit its actor.

The events stamped are `LinksChanged`, the series, season, episode and movie
added/updated/removed events of `IMetadataService`, the file events of
`IVideoService` (hashed, relocated, deleted), `ReleaseSaved` and
`ReleaseDeleted`, the user and API token events here, configuration `Saved`,
and the plugin installed, uninstalled, enabled and disabled events. In a
handler, read the actor from the event args, not from `IActorContext`: many
events run on a thread-pool task, after the request has ended.

The actor follows the async flow only until the scope that set it is disposed.
A timer or loop started from a request should run for the system
(`actorContext.BeginScope(null)`); work that outlives the request but should
stay the caller's captures `Current` and begins a scope of its own.
`BeginScope(token)` is also how a plugin runs its own work for a user.

---

## `IAuthenticationThrottleService`

An endpoint of yours that checks a credential uses it in this order:

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

Failures are tracked per client and per user within a sliding
`AttemptWindow`; past `MaxFailedAttempts` the lockout starts at
`InitialLockout` and doubles for each attempt after, capped at `MaxLockout`.

- **It is shared.** One singleton for the core and every plugin, so a client
  locked out by one endpoint is locked out for all of them.
- **The policy changes at runtime** when the admin saves settings; read the
  properties when you need them.
- **`ThrottleAuthentication` only reads.** Recording the outcome is on you, with
  `RegisterFailure` or `Reset`, except for the username charge
  `AuthenticateUser` makes itself.
- **`RegisterFailure(IUser)` needs an existing user.** An unknown username is
  only charged through `AuthenticateUser`.

`GetRemainingLockout`, `RegisterFailure` and `Reset` also take a
`HubCallerContext` or an `IUser`, for a hub-based login path or a countdown.
Every `RegisterFailure` raises `AuthenticationFailed` on the calling thread with
an `AuthenticationFailedEventArgs` (address, username when known, path, whether
it started a lockout and until when); the password tried is never part of it.

---

For how a plugin is discovered, constructed and registered, see the
[plugin overview](../../README.md).
