---
author: Eddie Rodriguez
date_created: 2026-06-18
summary: >-
  Rock's OpenID Connect server lets an admin register only a single redirect
  (callback) URI per OAuth client, even though the authorize-endpoint validator
  already accepts a comma-delimited list (added in 19.1.8 for MCP/Dynamic Client
  Registration). Expose that existing multi-value capability in the AuthClient
  detail block by swapping the single Redirect Uri textbox for a multi-value
  list control, fix the two server-side spots that still assume a single value,
  and add the per-URI format validation that is missing today. Storage stays the
  existing comma-delimited nvarchar(MAX) column; no migration. Post-logout
  redirect URI remains single-valued and JSON storage is out of scope.
---

# Multiple Redirect URIs for OpenID Connect Clients

## Summary

Rock acts as an OpenID Connect (OIDC) provider. Each registered OAuth client is an `AuthClient` entity with a single `RedirectUri` column. The admin UI (`AuthClientDetail` Obsidian block) edits that value through one plain textbox, so an administrator can only register one callback URL per client. Other OAuth providers allow several callback URLs per client so a single client registration can serve multiple environments or hosts.

The backend already supports this. As of Rock 19.1.8 (commit `f0917ef979`, 2026-04-27, shipped for MCP Dynamic Client Registration), the authorize endpoint splits `AuthClient.RedirectUri` on commas and validates the incoming `redirect_uri` against the resulting list. Both machine-driven registration paths (CIMD and RFC 7591 Dynamic Client Registration) already write multiple URIs into that one column, comma-joined. The only thing missing for human-configured clients is an admin UI that exposes the list, plus two narrow server-side corrections and the URL validation that does not exist anywhere today.

This spec exposes the existing capability and hardens its edges. It does **not** introduce a schema change, does **not** alter storage format (stays comma-delimited), and does **not** make the post-logout redirect URI multi-valued.

## Requirements

- An administrator MUST be able to register more than one redirect (callback) URI for a single OIDC `AuthClient` through the `AuthClientDetail` block.
- Existing single-URI clients MUST continue to work with no data migration and no behavior change (a value with no comma splits to a one-element list).
- Each registered redirect URI MUST be validated on save as a well-formed absolute `http` or `https` URI.
- A redirect URI MUST NOT contain a literal comma, because comma is the storage delimiter. The UI and server-side validation MUST reject such input with a clear message.
- Duplicate redirect URIs MUST be de-duplicated on save.
- When an authorization request omits `redirect_uri`, the server MUST honor the OIDC rule that omission is permitted only when exactly one redirect URI is registered; with multiple registered, the request MUST be rejected rather than validated against the raw comma-joined string.
- The change MUST NOT alter the public signature of any `Rock.Model` entity, service method, or other plugin-facing API.
- `PostLogoutRedirectUri` behavior MUST remain unchanged (single-valued, exact match).

## Background: Current State

### Library

Rock's OIDC **server** is built on AspNet.Security.OpenIdConnect.Server (ASOS), OWIN flavor — packages `Owin.Security.OpenIdConnect.Server` 2.0.0 and `Owin.Security.OAuth.Validation` 2.0.0 (`Rock.Oidc/Rock.Oidc.csproj`). It is not IdentityServer and not OpenIddict. ASOS is provider-based: Rock subclasses `OpenIdConnectServerProvider` (`Rock.Oidc/Authorization/AuthorizationProvider.cs`) and validates the request itself via `context.Validate(...)` / `context.Reject(...)`. There is no `IClientStore`, no library `Client` model, and no library-imposed single-URI limit. The single-URI behavior is entirely Rock's own — in the entity column and the editor UI.

### Storage

`AuthClient.RedirectUri` — `Rock/Model/Core/AuthClient/AuthClient.cs:120` — is a single `string`, `[Required]`, `StringValidationProfile.PlainText`, persisted as `nvarchar(MAX) NOT NULL` (migration `Rock.Migrations/Migrations/Version 12.0/Version 1.12.0/202008211513093_AddOidcModelsAndPages.cs:100`). No `MaxLength`, so it comfortably holds a comma-delimited list. The same entity already stores multi-value config (`AllowedScopes`, `AllowedClaims`) as JSON arrays, which is the convention-pure alternative considered and rejected below.

### Validation already accepts a list

`Rock.Oidc/Authorization/AuthorizationProvider.cs`, `ValidateAuthorizationRequest`, lines 285–305:

```csharp
if ( context.RedirectUri.IsNotNullOrWhiteSpace() )
{
    // Verify that the redirect URI is one of the URIs registered for
    // this client application.
    var validRedirectUris = authClient.RedirectUri?.SplitDelimitedValues( "," ) ?? new string[0];

    if ( !validRedirectUris.Contains( context.RedirectUri, StringComparer.Ordinal ) )
    {
        context.Reject(
            error: OpenIdConnectConstants.Errors.InvalidRequest,
            description: "The specified 'redirect_uri' is invalid." );
        return;
    }
    context.Validate( context.RedirectUri );
}
else
{
    context.Validate( authClient.RedirectUri );   // line 304 — multi-value bug
}
```

The `if` branch is already correct for multiple URIs. The `else` branch (request omits `redirect_uri`) hands the whole comma-joined string to `context.Validate`, which is only correct when exactly one URI is registered.

Machine writers already comma-join multiple URIs into the column:
- CIMD: `Rock/Model/Core/AuthClaim/AuthClientService.cs:234` — `RedirectUri = string.Join( ",", metadata.RedirectUris )`.
- DCR: `Rock.Oidc/DynamicRegistrationMiddleware.cs:147` — `RedirectUri = request.redirect_uris.JoinStrings( "," )`.

### The admin UI is the gap

The `AuthClientDetail` block is Obsidian-only (the legacy WebForms `.ascx` was chopped). The redirect URI editor is a single textbox:

`Rock.JavaScript.Obsidian.Blocks/src/Security/Oidc/AuthClientDetail/editPanel.partial.obs:52–58`:

```html
<TextBox v-model="redirectUri"
         label="Redirect Uri"
         help="After the person has provided their credentials, they will be redirected back to the client via the path indicated here. This URI is provided by the client system."
         rules="required" />
```

The bag carries it as a single `string` (`Rock.ViewModels/Blocks/Core/AuthClientDetail/AuthClientBag.cs:56–59`), the block reads it verbatim (`Rock.Blocks/Security/Oidc/AuthClientDetail.cs:186`) and writes it verbatim (`AuthClientDetail.cs:255–256`). Server-side validation (`ValidateAuthClient`, `AuthClientDetail.cs:99–110`) only calls `entity.IsValid` — there is no URL-format check anywhere in the codebase today.

## Proposed Change

Five edits, no migration. The block already owns a storage/structured-form boundary for scopes and claims (it serializes `ScopeClaims` to/from the entity's JSON columns in C#); the redirect URI work mirrors that pattern — the bag carries the list, the C# block owns the comma join/split against the storage column.

### 1. Bag — `Rock.ViewModels/Blocks/Core/AuthClientDetail/AuthClientBag.cs`

Replace the single `string RedirectUri` (lines 56–59) with `List<string> RedirectUris`. Regenerate `Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Core/AuthClientDetail/authClientBag.d.ts`. `PostLogoutRedirectUri` is left as a single `string`.

Backward-compatibility note: `AuthClientBag` is a block-internal view model consumed only by this block's own Vue partials and its generated `.d.ts`. No `Rock.Model` entity or service signature changes, so the plugin-facing public API is untouched.

### 2. Edit UI — `…/AuthClientDetail/editPanel.partial.obs`

Swap the single `<TextBox>` (lines 52–58) for the `ValueList` control (`@Obsidian/Controls/valueList.obs`), which binds and emits `string[]` — one URL per row, so an administrator never types a comma as a separator. Relabel to "Redirect URIs" and update the help text to say multiple may be entered, one per row. Change the `redirectUri` property ref (line 146), `propRefs` (line 155), the watch sync, and the emit to carry the `string[]` from `bag.redirectUris`. `PostLogoutRedirectUri` keeps its existing single `<TextBox>`.

Add a client-side rule (or a thin local validator) that flags any entry that is not a valid absolute `http`/`https` URI or that contains a comma, for immediate feedback. Server-side validation (edit 4) remains the authority.

### 3. View UI — `…/AuthClientDetail/viewPanel.partial.obs`

Render the list of redirect URIs (lines 68–73 currently show a single value) — one per line.

### 4. C# block — `Rock.Blocks/Security/Oidc/AuthClientDetail.cs`

- Read map (`GetCommonEntityBag`, line ~186): `RedirectUris = entity.RedirectUri.SplitDelimitedValues( "," ).Select( u => u.Trim() ).Where( u => u.IsNotNullOrWhiteSpace() ).ToList()`.
- Save map (`UpdateEntityFromBox`, lines ~255–256): `entity.RedirectUri = box.Bag.RedirectUris.Select( u => u.Trim() ).Where( u => u.IsNotNullOrWhiteSpace() ).Distinct( StringComparer.Ordinal ).JoinStrings( "," )`.
- Validation (`ValidateAuthClient`, lines ~99–110): for each redirect URI, require a well-formed absolute `http`/`https` URI (`Uri.TryCreate( value, UriKind.Absolute, out var uri )` and scheme check) and reject any value containing a literal comma, returning a clear `errorMessage` (and surface a hint that comma-containing URIs are unsupported by the current storage format). Require at least one URI (preserves the existing `[Required]` intent).

### 5. Validation provider — `Rock.Oidc/Authorization/AuthorizationProvider.cs`

- Fix the `else` branch (line 304): when `context.RedirectUri` is empty, split the stored value; if exactly one URI is registered, `context.Validate( that )`; if more than one, `context.Reject` with a description like "The 'redirect_uri' parameter is required when multiple redirect URIs are registered." This implements the OIDC rule and removes the multi-value bug.
- Defensive trimming: trim each split value in both branches so a hand-entered or DCR-supplied `"a, b"` (with spacing) still matches. (`SplitDelimitedValues` does not trim.)

## Backward Compatibility

- **Data**: no migration. Single-URI rows have no comma, so `SplitDelimitedValues(",")` yields a one-element list and the UI shows one row. CIMD/DCR rows are already comma-joined and continue to work unchanged.
- **API**: no `Rock.Model` entity or service signature changes. `AuthClient.RedirectUri` stays a `string`. Only the block view model (`AuthClientBag`) and the block's own Vue change.
- **Behavior**: a client with exactly one registered URI behaves identically before and after, including the `redirect_uri`-omitted flow (validates against the single URI). Only the previously-undefined multi-URI-omitted case changes (now a spec-correct rejection instead of validating against a comma blob).

## Verification Steps

1. Build `Rock.sln`; run `Rock.Tests`.
2. Check for an existing OIDC/`AuthorizationProvider` test seam. If one exists, add unit coverage for: multi-URI accept, unregistered-URI reject, omitted-`redirect_uri` with one URI (accept), omitted-`redirect_uri` with multiple URIs (reject), and the per-URI save validation (invalid scheme, comma-containing, duplicate). ASOS provider methods are awkward to unit-test in isolation; where a direct unit test is impractical, factor the split/validate decision into a testable helper.
3. Manual end-to-end:
   - Configure an `AuthClient` with two redirect URIs via the block UI; confirm both persist (comma-joined in the column) and both render in the view panel.
   - Run an authorization-code flow against each registered URI; confirm both succeed.
   - Attempt the flow with an unregistered `redirect_uri`; confirm rejection.
   - Issue an authorize request omitting `redirect_uri` against the two-URI client; confirm rejection. Repeat against a one-URI client; confirm acceptance.
   - Save attempts with an invalid URI, a comma-containing URI, and a duplicate; confirm each is rejected/de-duplicated with a clear message.
4. Confirm an existing single-URI client (pre-change data) loads, edits, and authorizes unchanged.

## Out of Scope

- **Post-logout redirect URI as a list.** `PostLogoutRedirectUri` stays single-valued. Making it multi-valued would require splitting the exact-match lookup in `AuthClientService.GetByPostLogoutRedirectUrlAsync` (`Rock/Model/Core/AuthClaim/AuthClientService.cs:147–151`) and the logout validation in `AuthorizationProvider.ValidateLogoutRequest` — a separate, opt-in change.
- **JSON storage.** Keeping the comma-delimited column matches the pattern SparkDev just shipped for CIMD/DCR (Prime Directive: follow existing patterns) and avoids a data migration. See Considered but Rejected.
- **WebForms editor.** None exists; the block is Obsidian-only.

## Considered but Rejected

### Deliver as a standalone plugin instead of a core PR

Rejected as the primary vehicle. The comma-splitting validator exists only in Rock 19.1.8+; a plugin that writes multiple URIs to the column would work only on the newest Rock and would silently break clients on older instances, where the validator does not split. The OIDC provider is instantiated as a hard-coded `new AuthorizationProvider()` in `Rock.Oidc/Startup.cs:85` and mounted before any plugin OWIN startup runs (`RockWeb/App_Code/Startup.cs:58` vs `:80`), with no DI/MEF/interface seam — so a plugin cannot replace or wrap redirect validation, nor fix the `else`-branch bug, without core changes. A core PR is the natural home and is timely, since SparkDev built this backend in the current development cycle.

### Migrate `RedirectUri` to a JSON array of strings

Rejected for this change (noted as the convention-pure alternative). It matches Rock's "JSON not delimiters" rule and the `AllowedScopes`/`AllowedClaims` precedent on the same entity, and it would correctly support redirect URIs that contain commas. But it requires a data migration (`'a,b'` → `'["a","b"]'`), a backward-compatible reader that tolerates both bare strings and JSON for in-field/plugin-written rows, and reworking the CIMD and DCR writers SparkDev just shipped. That is a larger, riskier change than the feature warrants, and it diverges from the established pattern in this exact code path. If a future requirement needs comma-bearing URIs or per-URI metadata, revisit JSON or a child table then.

### Introduce a child `AuthClientRedirectUri` table

Rejected. Proper normalization, but it adds an entity, EntityType GUID, EF config, a `CreateTable` migration, a data migration, service queries, and UI/bag plumbing — substantially more work — and it breaks the same-entity precedent (scopes and claims are stored on the parent, not in child tables). No relational requirement exists today (nothing references an individual redirect URI; no per-URI metadata is needed).

### Minimal UI-only change (leave the bug and validation)

Rejected. Swapping the control without fixing the `else`-branch multi-value bug or adding URL validation would ship a half-correct feature: omitting `redirect_uri` against a multi-URI client would validate against a comma blob, and malformed or comma-bearing URIs would persist and then fail opaquely at runtime.

## Related

- `f0917ef979`, "Added support for DCR (Dynamic Client Registration) and CIMD registration for MCP clients using OAuth." (2026-04-27) — introduced the comma-splitting validator; first shipped in 19.1.8.
- `fc2d44bb15`, "Added new OpenId (OIDC) server …" — original OIDC server (single-URI validation).
- `Rock.Oidc/Authorization/AuthorizationProvider.cs` — authorize/token/logout provider; redirect validation at lines 285–305.
- `Rock/Model/Core/AuthClient/AuthClient.cs` — entity (`RedirectUri` at line 120).
- `Rock/Model/Core/AuthClaim/AuthClientService.cs` — service, CIMD writer (line 234), post-logout lookup (lines 147–151).
- `Rock.Oidc/DynamicRegistrationMiddleware.cs` — RFC 7591 DCR writer (line 147).
- `Rock.Blocks/Security/Oidc/AuthClientDetail.cs`, `…/editPanel.partial.obs`, `…/viewPanel.partial.obs`, `AuthClientBag.cs` — the admin block changed by this spec.
- `Rock.JavaScript.Obsidian/Framework/Controls/valueList.obs` — the multi-value list control used for the editor.
