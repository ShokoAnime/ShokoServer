# Optional OIDC / SSO Sign-In

Shoko can optionally authenticate against an external OpenID Connect provider
(Authentik, Keycloak, Authelia, or any spec-compliant IdP) as a second,
opt-in way to sign in — alongside, never instead of, local username/password
accounts.

> **This is not a security boundary.** OIDC here is a convenience/deterrent
> layer on top of Shoko's existing auth, not a hardened multi-tenant access
> control system. Do **not** expose Shoko directly to the internet because
> OIDC is enabled — put it behind a VPN or a reverse proxy you control either
> way.

## How it works

- Local accounts and `Auth/SignIn` keep working exactly as before.
- An already-authenticated local user explicitly **links** their account to
  an external identity (`GET /api/v3/Auth/Oidc/Link`). Sign-in never
  auto-matches an external identity to a local account by username or email —
  only an explicit link can create that association.
- Once linked, that user can sign in via `GET /api/v3/Auth/Oidc/Challenge`
  instead of the password form.
- Optionally, `AutoCreateUsers` lets an unlinked identity provision a brand
  new local account on first sign-in (off by default) — this never links to
  or takes over an *existing* account.

## 1. Register Shoko with your IdP

Create a new OAuth2/OIDC client/application for Shoko with:

- **Grant type:** Authorization Code (with PKCE)
- **Redirect URI:** `https://<your-shoko-public-url>/api/v3/Auth/Oidc/Callback`
  — must match exactly what you put in `PublicUrl` below.
- **Scopes:** `openid profile email`

Copy the resulting **Client ID** and **Client Secret**, and note the
provider's **issuer/authority URL** (the base URL that serves
`/.well-known/openid-configuration`, e.g.
`https://auth.example.com/application/o/shoko/` for Authentik).

## 2. Configure Shoko

OIDC settings are deliberately **not** exposed in the settings UI or API —
enabling it means editing `settings-server.json` directly on the server, so
turning it on is a conscious, hands-on-the-server action rather than a
checkbox reachable from a browser. Stop Shoko, edit the file, then restart.

```json
{
  "Oidc": {
    "Enabled": true,
    "DisplayName": "Authentik",
    "Authority": "https://auth.example.com/application/o/shoko/",
    "PublicUrl": "https://shoko.example.com",
    "ClientID": "your-client-id",
    "ClientSecret": "your-client-secret",
    "AutoCreateUsers": false
  }
}
```

| Field | Required | Notes |
|---|---|---|
| `Enabled` | yes | Master on/off switch. |
| `Authority` | yes | Must exactly match the issuer your IdP's discovery document reports at `<Authority>/.well-known/openid-configuration` — Shoko rejects a mismatch rather than trusting whatever the document claims. |
| `PublicUrl` | yes | The externally-reachable base URL of this Shoko instance, e.g. `https://shoko.example.com`. Used to build the fixed `redirect_uri` sent to the provider — must be the exact origin registered with the IdP in step 1. Shoko does **not** derive this from the incoming request's `Host` header, to avoid trusting a header a reverse proxy might not fully sanitize. |
| `ClientID` / `ClientSecret` | yes | From step 1. |
| `DisplayName` | no | Label for the WebUI's SSO button (e.g. "Sign in with Authentik"). Defaults to `SSO`. |
| `AutoCreateUsers` | no | Off by default. When on, an unlinked identity gets a brand-new local account (random password) on first sign-in instead of being rejected. |

The WebUI only shows the SSO option when `GET /api/v3/init/status` reports
`OidcEnabled: true` — provider details are never exposed to unauthenticated
clients.

## 3. Link an account

1. Sign in to Shoko normally with a local account.
2. Visit `GET /api/v3/Auth/Oidc/Link` (authenticated) to start the linking
   flow — this redirects to your IdP, then back to Shoko, and links the
   external identity to whichever local account started the flow. It never
   matches by username or email.
3. From then on, that user can sign in via `Challenge` instead of the
   password form.

To remove the link, call `POST /api/v3/Auth/Oidc/Unlink` — this also
invalidates every API token previously minted through that identity.

## Design notes (why it's built this way)

- **PKCE (`S256`)** is used on every authorization-code exchange, per current
  OAuth 2.1 guidance — closes the code-interception gap even though a client
  secret is also in play.
- **`state`/nonce/PKCE verifier** travel in a single ASP.NET Data-Protection
  signed-and-encrypted payload, never as a raw cookie or client-visible
  value, and expire after 10 minutes.
- **Discovery document caching**: Shoko keeps one long-lived
  `ConfigurationManager` per configured authority (auto-refreshing per its
  own internal cache policy) instead of re-fetching on every login.
- **Issuer pinning**: the ID token's issuer is validated against your
  configured `Authority`, not against whatever the discovery document
  self-reports — the document's own `issuer` field is first checked to
  match `Authority` before anything from it is trusted.
- **Fixed `redirect_uri`** built from `PublicUrl`, never derived from the
  request's `Host` header.
- **Token delivery**: on success, Shoko redirects to the WebUI with the
  minted API token in the URL **fragment** (`#oidcToken=...`), not a query
  parameter — fragments are never sent to the server or logged by a reverse
  proxy.
- **Token lifetime**: the minted Shoko API token's expiry matches the OIDC
  ID token's own `exp` claim rather than a fixed window.
