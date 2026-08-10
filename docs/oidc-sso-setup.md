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
`/.well-known/openid-configuration`).

Below are worked examples for three common self-hosted providers — the
underlying steps are the same for any OIDC-compliant IdP not listed here
(Zitadel, Pocket ID, Casdoor, an Entra ID / Google Workspace tenant, etc.):
just create a confidential Authorization Code client with the redirect URI
above and copy the resulting values into the same three settings fields.

<details>
<summary><strong>Authentik</strong></summary>

1. **Applications → Providers → Create** → type **OAuth2/OpenID Provider**.
2. **Authorization flow**: `default-provider-authorization-explicit-consent`
   (or your own).
3. **Client type**: `Confidential`.
4. **Redirect URIs**: `https://<your-shoko-public-url>/api/v3/Auth/Oidc/Callback`.
5. **Scopes**: include `openid`, `email`, `profile`.
6. Save, then **Applications → Applications → Create** and attach the
   provider you just made.
7. `Authority` = `https://<authentik-host>/application/o/<slug>/` (the
   provider's **OpenID Configuration Issuer** shown on its overview page).

Reference: [Create an OAuth2 provider — authentik docs](https://docs.goauthentik.io/add-secure-apps/providers/oauth2/create-oauth2-provider/)

</details>

<details>
<summary><strong>Keycloak</strong></summary>

1. **Clients → Create client**, protocol `openid-connect`.
2. **Client authentication**: `On` (this makes it confidential and generates
   a client secret under the **Credentials** tab after saving).
3. **Standard flow**: `On` (this is the Authorization Code flow). Leave
   Direct access grants / Implicit flow off.
4. **Valid redirect URIs**: `https://<your-shoko-public-url>/api/v3/Auth/Oidc/Callback`.
5. `Authority` = `https://<keycloak-host>/realms/<realm-name>` — Keycloak
   serves discovery at `<Authority>/.well-known/openid-configuration`.

Reference: [Managing OpenID Connect and SAML Clients — Keycloak/Red Hat docs](https://docs.redhat.com/en/documentation/red_hat_build_of_keycloak/22.0/html/server_administration_guide/assembly-managing-clients_server_administration_guide)

</details>

<details>
<summary><strong>Authelia</strong></summary>

Authelia's OIDC provider is configured via YAML, not a web UI. Add a client
under `identity_providers.oidc.clients` in your Authelia configuration:

```yaml
identity_providers:
  oidc:
    clients:
      - client_id: shoko
        client_name: Shoko
        client_secret: '$pbkdf2-sha512$...'  # hash of your chosen secret, see docs below
        public: false
        authorization_policy: one_factor
        redirect_uris:
          - https://<your-shoko-public-url>/api/v3/Auth/Oidc/Callback
        scopes:
          - openid
          - email
          - profile
        grant_types:
          - authorization_code
        response_types:
          - code
```

`Authority` = the base URL of your Authelia instance (e.g.
`https://auth.example.com`). `client_secret` in the YAML must be a *hashed*
value generated with Authelia's crypto CLI — the plaintext secret you hash
is what goes into Shoko's `ClientSecret`.

Reference: [OpenID Connect 1.0 Clients — Authelia docs](https://www.authelia.com/configuration/identity-providers/openid-connect/clients/)

</details>

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

## Further reading

Background on the standards this implementation follows, if you want to
understand what's actually happening under the hood:

- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) — the spec defining the Authorization Code flow, ID tokens, and discovery document used here.
- [RFC 7636 — Proof Key for Code Exchange (PKCE)](https://datatracker.ietf.org/doc/html/rfc7636) — why every exchange includes a `code_challenge`/`code_verifier` pair.
- [RFC 8414 — OAuth 2.0 Authorization Server Metadata](https://datatracker.ietf.org/doc/html/rfc8414) — the `/.well-known/openid-configuration` discovery document format.
- [oauth.net PKCE explainer](https://oauth.net/2/pkce/) — a more approachable, less spec-dense description of PKCE than the RFC.

Provider docs:

- [authentik documentation](https://docs.goauthentik.io/)
- [Keycloak documentation](https://www.keycloak.org/documentation)
- [Authelia documentation](https://www.authelia.com/overview/prologue/introduction/)
