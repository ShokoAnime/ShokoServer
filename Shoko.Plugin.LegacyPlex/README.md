# Shoko Legacy Plex Sync Support

The bundled first-party plugin holding the server's legacy Plex support. It
ships with the server in `plugins/Shoko.Plugin.LegacyPlex/`, is enabled by
default and cannot be uninstalled. Disabling it turns off everything below.

## What it does

- **Webhook.** Plex posts its events to `/plex` (or `/plex.json`). A
  `media.scrobble` for an item matched by the Shoko agents
  (`com.plexapp.agents.shoko` or `com.plexapp.agents.shokorelay`) marks the
  episode watched for the calling user, or else for the first user naming the
  Plex account among their Plex usernames. The webhook takes no API key, as
  Plex sends none.
- **Account linking.** `GET /plex/loginurl` starts a Plex pin and returns the
  URL to approve it at, `GET /plex/pin/authenticated` tells whether the
  user's token works, and `GET /plex/token/invalidate` forgets the token.
- **Server and libraries.** `GET /plex/server/list` and `POST /plex/server`
  pick the Plex Media Server, `GET /plex/libraries` and `POST /plex/libraries`
  the libraries to sync.
- **Sync.** `SyncPlexWatchedStatesJob` walks the picked libraries for one
  user, finds each episode's file by its folder and file name, and marks what
  is watched on one side only as watched on the other. It is queued by
  `GET /plex/sync` (the caller), `GET /plex/sync/{userID}` and
  `GET /plex/sync/all` (admins), by `GET /api/v3/Action/PlexSyncAll` (admins),
  and by the "Plex Sync All" scheduled action, which has no default triggers.

The routes under `/plex` are version-neutral and keep the paths and shapes
they always had. Apart from the webhook they need an API key.

## Where its data lives

The plugin keeps nothing of its own. The Plex data stays in the core, where
it always was, and the plugin compiles against `Shoko.Server` to reach it:

- The picked Plex Media Server and libraries are the `Plex` section of the
  server settings (`Server`, `Libraries`), still read and patched through
  `/api/v3/Settings`.
- Each user's Plex token and Plex users are the `PlexToken` and `PlexUsers`
  columns of the user table, the latter still served as `PlexUsernames` by
  the APIv3 user model.

## Thumbnail and icon

`Assets/thumbnail.svg` and `Assets/icon.svg` are embedded resources, read by
the server through the plugin.
