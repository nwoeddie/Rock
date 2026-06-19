# Multiple Redirect URIs for OIDC Clients — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a Rock admin register multiple redirect (callback) URIs on a single OpenID Connect `AuthClient`, exposing the comma-delimited capability the OIDC server already has.

**Architecture:** All redirect-URI rules (split, normalize/join, validate) become pure helper methods on the `AuthClient` entity so they are unit-testable with no DB or OIDC harness. The OIDC authorization provider, the detail block, and the Obsidian UI all consume those helpers. Storage stays the existing single comma-delimited `nvarchar(MAX)` column — no migration.

**Tech Stack:** C# / .NET Framework (Rock, Rock.Oidc, Rock.Blocks, Rock.ViewModels), MSTest (Rock.Tests), Vue 3 + TypeScript (Rock.JavaScript.Obsidian.Blocks), AspNet.Security.OpenIdConnect.Server (OWIN).

## Global Constraints

- Spec of record: `specs/260618-oidc-multiple-redirect-uris.md`. Every requirement there is in force here.
- Storage stays **comma-delimited** in the existing `AuthClient.RedirectUri` column. No schema change, no migration.
- **No** change to any existing public method signature. New `public` members are additive only (no backward-compatibility break). `AuthClient.RedirectUri` stays a `string`.
- `PostLogoutRedirectUri` behavior is **unchanged** (single-valued).
- A redirect URI MUST be a well-formed absolute `http`/`https` URI and MUST NOT contain a literal comma (the storage delimiter).
- Redirect URIs are trimmed, empty entries dropped, duplicates removed (ordinal) before storage; at least one is required.
- C# style: braces always, `var`, padded parens `foo( bar )`, copyright header on every new file. TypeScript/Vue style: 4-space indent, double quotes, semicolons, `===`, no padded parens, one-line `.obs` copyright header.
- Commits go through the repo's normal flow; the `/code-review` commit gate and the "no push / no PR without explicit OK" rule apply. Do not `git push` or open a PR as part of executing this plan.

---

### Task 1: Redirect-URI helper methods on the `AuthClient` entity

Pure, DB-free helpers that own every redirect-URI rule. TDD — tests first.

**Files:**
- Modify: `Rock/Model/Core/AuthClient/AuthClient.cs` (add usings; add helper methods inside the `AuthClient` class, after `ScopeApprovalExpiration` at line 142)
- Test (create): `Rock.Tests/Model/AuthClientTests.cs`

**Interfaces:**
- Produces:
  - `public List<string> GetRedirectUris()` — splits `RedirectUri` on `,`, trims, drops empties, de-dupes (ordinal). Empty/blank column → empty list.
  - `public static string JoinRedirectUris( IEnumerable<string> redirectUris )` — trims, drops empties, de-dupes (ordinal), joins with `,`. Null → `""`.
  - `public static bool ValidateRedirectUris( IEnumerable<string> redirectUris, out string errorMessage )` — requires ≥1; each must pass `IsValidRedirectUri`.
  - `private static bool IsValidRedirectUri( string redirectUri, out string errorMessage )` — non-blank, no comma, absolute `http`/`https`.

- [ ] **Step 1: Write the failing tests**

Create `Rock.Tests/Model/AuthClientTests.cs`:

```csharp
// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rock.Model;

namespace Rock.Tests.Model
{
    [TestClass]
    public class AuthClientTests
    {
        [TestMethod]
        public void GetRedirectUris_WithSingleUri_ReturnsOneItem()
        {
            var authClient = new AuthClient { RedirectUri = "https://example.com/callback" };

            var result = authClient.GetRedirectUris();

            Assert.AreEqual( 1, result.Count );
            Assert.AreEqual( "https://example.com/callback", result[0] );
        }

        [TestMethod]
        public void GetRedirectUris_WithCommaDelimitedUris_ReturnsTrimmedItems()
        {
            var authClient = new AuthClient { RedirectUri = "https://a.com/cb, https://b.com/cb" };

            var result = authClient.GetRedirectUris();

            CollectionAssert.AreEqual( new List<string> { "https://a.com/cb", "https://b.com/cb" }, result );
        }

        [TestMethod]
        public void GetRedirectUris_WithEmptyValue_ReturnsEmptyList()
        {
            var authClient = new AuthClient { RedirectUri = "" };

            Assert.AreEqual( 0, authClient.GetRedirectUris().Count );
        }

        [TestMethod]
        public void JoinRedirectUris_TrimsDeduplicatesAndDropsEmptyEntries()
        {
            var input = new List<string> { " https://a.com ", "https://a.com", "", "https://b.com" };

            var result = AuthClient.JoinRedirectUris( input );

            Assert.AreEqual( "https://a.com,https://b.com", result );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithValidHttpAndHttpsUris_ReturnsTrue()
        {
            var input = new List<string> { "https://a.com/cb", "http://localhost:6229/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsTrue( result );
            Assert.AreEqual( string.Empty, errorMessage );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithNoUris_ReturnsFalse()
        {
            var result = AuthClient.ValidateRedirectUris( new List<string>(), out var errorMessage );

            Assert.IsFalse( result );
            Assert.IsTrue( errorMessage.Contains( "At least one" ) );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithCommaInValue_ReturnsFalse()
        {
            var input = new List<string> { "https://a.com/cb,https://b.com/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
            Assert.IsTrue( errorMessage.Contains( "comma" ) );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithNonHttpScheme_ReturnsFalse()
        {
            var input = new List<string> { "ftp://a.com/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithRelativeUri_ReturnsFalse()
        {
            var input = new List<string> { "/callback" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile / fail**

Run the `/build` skill, then the `/test` skill (runs `Rock.Tests`).
Expected: build/test FAILS — `AuthClient` has no `GetRedirectUris`, `JoinRedirectUris`, or `ValidateRedirectUris`.

- [ ] **Step 3: Add the usings to `AuthClient.cs`**

In `Rock/Model/Core/AuthClient/AuthClient.cs`, add these three usings to the existing using block (top of file, currently starts at `using System.ComponentModel.DataAnnotations;`):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
```

- [ ] **Step 4: Add the helper methods**

In `Rock/Model/Core/AuthClient/AuthClient.cs`, insert this region inside the `AuthClient` class, immediately after the `ScopeApprovalExpiration` property (after line 142, before the class's closing `}` at line 143):

```csharp
        #region Redirect URI Helpers

        /*
            6/18/26 - CLAUDE

            Redirect URIs are stored as a single comma-delimited string in the
            RedirectUri column (the format the OIDC authorization provider and
            the dynamic-registration paths already use). These helpers centralize
            the split/join/validate rules so the provider, the detail block, and
            their tests all share one implementation.

            Reason: One source of truth for multi-redirect-URI parsing and validation.
        */

        /// <summary>
        /// Gets the list of registered redirect URIs by splitting the comma-delimited
        /// <see cref="RedirectUri"/> value. Entries are trimmed, blank entries removed,
        /// and duplicates removed using an ordinal comparison. Returns an empty list
        /// when no redirect URIs are registered.
        /// </summary>
        /// <returns>The normalized list of registered redirect URIs.</returns>
        public List<string> GetRedirectUris()
        {
            if ( RedirectUri.IsNullOrWhiteSpace() )
            {
                return new List<string>();
            }

            return RedirectUri
                .Split( ',' )
                .Select( uri => uri.Trim() )
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Distinct( StringComparer.Ordinal )
                .ToList();
        }

        /// <summary>
        /// Normalizes a set of redirect URIs into the comma-delimited string used to
        /// store the <see cref="RedirectUri"/> value. Entries are trimmed, blank
        /// entries removed, and duplicates removed using an ordinal comparison.
        /// </summary>
        /// <param name="redirectUris">The redirect URIs to normalize and join.</param>
        /// <returns>The comma-delimited storage value, or an empty string when none are supplied.</returns>
        public static string JoinRedirectUris( IEnumerable<string> redirectUris )
        {
            if ( redirectUris == null )
            {
                return string.Empty;
            }

            var normalized = redirectUris
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Select( uri => uri.Trim() )
                .Distinct( StringComparer.Ordinal )
                .ToList();

            return string.Join( ",", normalized );
        }

        /// <summary>
        /// Validates a set of redirect URIs. At least one URI is required, and each
        /// must be a well-formed absolute http or https URI that does not contain a
        /// comma (the storage delimiter).
        /// </summary>
        /// <param name="redirectUris">The redirect URIs to validate.</param>
        /// <param name="errorMessage">On <c>false</c> return, the reason validation failed; otherwise empty.</param>
        /// <returns><c>true</c> when all redirect URIs are valid; otherwise <c>false</c>.</returns>
        public static bool ValidateRedirectUris( IEnumerable<string> redirectUris, out string errorMessage )
        {
            errorMessage = string.Empty;

            var uris = ( redirectUris ?? Enumerable.Empty<string>() )
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Select( uri => uri.Trim() )
                .ToList();

            if ( !uris.Any() )
            {
                errorMessage = "At least one redirect URI is required.";
                return false;
            }

            foreach ( var uri in uris )
            {
                if ( !IsValidRedirectUri( uri, out errorMessage ) )
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a single redirect URI is valid for storage: it must be
        /// non-blank, must not contain a comma (the storage delimiter), and must be a
        /// well-formed absolute http or https URI.
        /// </summary>
        /// <param name="redirectUri">The redirect URI to validate.</param>
        /// <param name="errorMessage">On <c>false</c> return, the reason the URI is invalid; otherwise empty.</param>
        /// <returns><c>true</c> when the redirect URI is valid; otherwise <c>false</c>.</returns>
        private static bool IsValidRedirectUri( string redirectUri, out string errorMessage )
        {
            errorMessage = string.Empty;

            if ( redirectUri.IsNullOrWhiteSpace() )
            {
                errorMessage = "A redirect URI cannot be blank.";
                return false;
            }

            if ( redirectUri.Contains( "," ) )
            {
                errorMessage = $"The redirect URI '{redirectUri}' cannot contain a comma. Enter each redirect URI separately.";
                return false;
            }

            if ( !Uri.TryCreate( redirectUri, UriKind.Absolute, out var uri )
                || ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps ) )
            {
                errorMessage = $"The redirect URI '{redirectUri}' must be a valid absolute http or https URL.";
                return false;
            }

            return true;
        }

        #endregion Redirect URI Helpers
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run the `/build` skill, then the `/test` skill.
Expected: build succeeds; all nine `AuthClientTests` methods PASS.

- [ ] **Step 6: Commit**

```bash
git add Rock/Model/Core/AuthClient/AuthClient.cs Rock.Tests/Model/AuthClientTests.cs
git commit -m "- Added AuthClient redirect URI helper methods with unit tests."
```

---

### Task 2: Honor multiple redirect URIs in the OIDC authorization provider

Route the authorize-endpoint validation through `GetRedirectUris()` and fix the omitted-`redirect_uri` branch so a multi-URI client is handled correctly.

**Files:**
- Modify: `Rock.Oidc/Authorization/AuthorizationProvider.cs` (the redirect-URI block in `ValidateAuthorizationRequest`, lines 285–305)

**Interfaces:**
- Consumes: `AuthClient.GetRedirectUris()` (Task 1).

- [ ] **Step 1: Replace the redirect-URI validation block**

In `Rock.Oidc/Authorization/AuthorizationProvider.cs`, replace this existing block (lines 285–305):

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
                context.Validate( authClient.RedirectUri );
            }
```

with:

```csharp
            // A client may register more than one redirect URI (stored comma-delimited).
            var registeredRedirectUris = authClient.GetRedirectUris();

            if ( context.RedirectUri.IsNotNullOrWhiteSpace() )
            {
                // Verify that the redirect URI is one of the URIs registered for
                // this client application.
                if ( !registeredRedirectUris.Contains( context.RedirectUri, StringComparer.Ordinal ) )
                {
                    context.Reject(
                        error: OpenIdConnectConstants.Errors.InvalidRequest,
                        description: "The specified 'redirect_uri' is invalid." );

                    return;
                }

                context.Validate( context.RedirectUri );
            }
            else if ( registeredRedirectUris.Count == 1 )
            {
                // Per the OIDC spec, redirect_uri may be omitted only when the client
                // has exactly one registered redirect URI.
                context.Validate( registeredRedirectUris[0] );
            }
            else
            {
                context.Reject(
                    error: OpenIdConnectConstants.Errors.InvalidRequest,
                    description: "The 'redirect_uri' parameter is required when multiple redirect URIs are registered." );

                return;
            }
```

- [ ] **Step 2: Build and confirm it compiles**

Run the `/build` skill.
Expected: build succeeds. (`GetRedirectUris` is found via Task 1; `System.Linq` is already imported by this file's existing `.Contains( ..., StringComparer.Ordinal )` usage.)

- [ ] **Step 3: Commit**

```bash
git add Rock.Oidc/Authorization/AuthorizationProvider.cs
git commit -m "- Updated OIDC authorization provider to honor multiple redirect URIs."
```

> Note: ASOS provider methods take framework context objects that are impractical to unit-test in isolation; the decision logic is covered by Task 1's `GetRedirectUris()` tests, and the end-to-end flow is verified manually in Task 5.

---

### Task 3: Edit multiple redirect URIs through the detail block (bag + C#)

Change the bag to carry a list, map it to/from the comma-delimited column in C#, and validate the raw list on save (before joining, so a comma-in-value is caught).

**Files:**
- Modify: `Rock.ViewModels/Blocks/Core/AuthClientDetail/AuthClientBag.cs:56-59`
- Modify: `Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Core/AuthClientDetail/authClientBag.d.ts:53-54`
- Modify: `Rock.Blocks/Security/Oidc/AuthClientDetail.cs` (`GetCommonEntityBag` line 186; `UpdateEntityFromBox` lines 255-256; `Save` action lines 437-457)

**Interfaces:**
- Consumes: `AuthClient.GetRedirectUris()`, `AuthClient.JoinRedirectUris(...)`, `AuthClient.ValidateRedirectUris(...)` (Task 1).
- Produces: `AuthClientBag.RedirectUris` (`List<string>`), consumed by the Vue in Task 4.

- [ ] **Step 1: Change the bag property to a list**

In `Rock.ViewModels/Blocks/Core/AuthClientDetail/AuthClientBag.cs`, replace lines 56–59:

```csharp
        /// <summary>
        /// Gets or sets the redirect URL.
        /// </summary>
        public string RedirectUri { get; set; }
```

with:

```csharp
        /// <summary>
        /// Gets or sets the redirect URIs registered for this client. A client may
        /// have more than one; an authorization request must use one of these values.
        /// </summary>
        public List<string> RedirectUris { get; set; }
```

(`using System.Collections.Generic;` is already present at line 18.)

- [ ] **Step 2: Update the generated TypeScript bag to match**

In `Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Core/AuthClientDetail/authClientBag.d.ts`, replace lines 53–54:

```typescript
    /** Gets or sets the redirect URL. */
    redirectUri?: string | null;
```

with:

```typescript
    /** Gets or sets the redirect URIs registered for this client. */
    redirectUris?: string[] | null;
```

- [ ] **Step 3: Update the read map in `GetCommonEntityBag`**

In `Rock.Blocks/Security/Oidc/AuthClientDetail.cs`, replace line 186:

```csharp
                RedirectUri = entity.RedirectUri,
```

with:

```csharp
                RedirectUris = entity.GetRedirectUris(),
```

- [ ] **Step 4: Update the save map in `UpdateEntityFromBox`**

In `Rock.Blocks/Security/Oidc/AuthClientDetail.cs`, replace lines 255–256:

```csharp
            box.IfValidProperty( nameof( box.Bag.RedirectUri ),
                () => entity.RedirectUri = box.Bag.RedirectUri );
```

with:

```csharp
            box.IfValidProperty( nameof( box.Bag.RedirectUris ),
                () => entity.RedirectUri = AuthClient.JoinRedirectUris( box.Bag.RedirectUris ) );
```

- [ ] **Step 5: Validate the redirect URIs in the `Save` action before joining**

In `Rock.Blocks/Security/Oidc/AuthClientDetail.cs` `Save` action, insert the redirect-URI validation immediately after the `TryGetEntityForEditAction` block and before the `UpdateEntityFromBox` call (between lines 445 and 447):

```csharp
            // Validate the redirect URIs against the raw bag values before they are
            // normalized and comma-joined for storage, so a value that contains the
            // comma delimiter is rejected rather than silently split into two URIs.
            if ( !AuthClient.ValidateRedirectUris( box.Bag.RedirectUris, out var redirectUriError ) )
            {
                return ActionBadRequest( redirectUriError );
            }

```

- [ ] **Step 6: Build and confirm it compiles**

Run the `/build` skill (compiles `Rock.sln`, including Rock.ViewModels and Rock.Blocks).
Expected: build succeeds. (The Obsidian TypeScript project is not part of this MSBuild; its Vue still references the old `redirectUri` and is fixed in Task 4.)

- [ ] **Step 7: Run the unit tests**

Run the `/test` skill.
Expected: `AuthClientTests` still PASS (no regressions).

- [ ] **Step 8: Commit**

```bash
git add Rock.ViewModels/Blocks/Core/AuthClientDetail/AuthClientBag.cs \
        Rock.JavaScript.Obsidian/Framework/ViewModels/Blocks/Core/AuthClientDetail/authClientBag.d.ts \
        Rock.Blocks/Security/Oidc/AuthClientDetail.cs
git commit -m "- Updated AuthClient detail block to read, save, and validate multiple redirect URIs."
```

---

### Task 4: Multi-value redirect URI UI (edit + view partials)

Swap the single textbox for the `ValueList` control and render the list in view mode.

**Files:**
- Modify: `Rock.JavaScript.Obsidian.Blocks/src/Security/Oidc/AuthClientDetail/editPanel.partial.obs`
- Modify: `Rock.JavaScript.Obsidian.Blocks/src/Security/Oidc/AuthClientDetail/viewPanel.partial.obs`

**Interfaces:**
- Consumes: `AuthClientBag.redirectUris` (`string[] | null`) from Task 3.

- [ ] **Step 1: Import the `ValueList` control in the edit partial**

In `editPanel.partial.obs`, add this import after the `TextBox` import (line 105):

```typescript
    import ValueList from "@Obsidian/Controls/valueList.obs";
```

- [ ] **Step 2: Replace the Redirect Uri textbox with `ValueList`**

In `editPanel.partial.obs`, replace the redirect URI `TextBox` (lines 54–57):

```html
                <TextBox v-model="redirectUri"
                         label="Redirect Uri"
                         help="After the person has provided their credentials, they will be redirected back to the client via the path indicated here. This URI is provided by the client system."
                         rules="required" />
```

with:

```html
                <ValueList v-model="redirectUris"
                           label="Redirect URIs"
                           help="After the person has provided their credentials, they will be redirected back to the client via one of the URIs listed here. Enter one URI per row; the authorization request must use one of these values. These URIs are provided by the client system." />
```

(Leave the `postLogoutRedirectUri` `TextBox` at lines 60–63 unchanged.)

- [ ] **Step 3: Change the `redirectUri` ref to a list ref**

In `editPanel.partial.obs`, replace line 146:

```typescript
    const redirectUri = propertyRef(props.modelValue.bag?.redirectUri ?? "", "RedirectUri");
```

with:

```typescript
    const redirectUris = propertyRef(props.modelValue.bag?.redirectUris ?? [], "RedirectUris");
```

- [ ] **Step 4: Update the `propRefs` array**

In `editPanel.partial.obs`, replace line 155:

```typescript
    const propRefs = [isActive, name, clientId, clientSecret, redirectUri, postLogoutRedirectUri, scopeApprovalExpiration];
```

with:

```typescript
    const propRefs = [isActive, name, clientId, clientSecret, redirectUris, postLogoutRedirectUri, scopeApprovalExpiration];
```

- [ ] **Step 5: Update the parental-change watch**

In `editPanel.partial.obs`, replace line 212:

```typescript
        updateRefValue(redirectUri, props.modelValue.bag?.redirectUri ?? "");
```

with:

```typescript
        updateRefValue(redirectUris, props.modelValue.bag?.redirectUris ?? []);
```

- [ ] **Step 6: Update the emit**

In `editPanel.partial.obs`, replace line 230:

```typescript
        setPropertiesBoxValue(newValue, "redirectUri", redirectUri.value);
```

with:

```typescript
        setPropertiesBoxValue(newValue, "redirectUris", redirectUris.value);
```

- [ ] **Step 7: Render the list in the view partial**

In `viewPanel.partial.obs`, replace the redirect URI block (lines 68–70):

```typescript
        if (props.modelValue?.redirectUri) {
            valueBuilder.addTextValue("Redirect Uri", props.modelValue.redirectUri);
        }
```

with:

```typescript
        if (props.modelValue?.redirectUris && props.modelValue.redirectUris.length > 0) {
            valueBuilder.addTextValue("Redirect URIs", props.modelValue.redirectUris.join("\n"));
        }
```

- [ ] **Step 8: Lint and type-check the Obsidian Blocks project**

Run: `npm run lint` in `Rock.JavaScript.Obsidian.Blocks`
Expected: no errors for `editPanel.partial.obs` or `viewPanel.partial.obs`; no remaining reference to `redirectUri` (singular) in either file.

- [ ] **Step 9: Commit**

```bash
git add Rock.JavaScript.Obsidian.Blocks/src/Security/Oidc/AuthClientDetail/editPanel.partial.obs \
        Rock.JavaScript.Obsidian.Blocks/src/Security/Oidc/AuthClientDetail/viewPanel.partial.obs
git commit -m "+ (Core) Added support for configuring multiple redirect URIs on OpenID Connect clients."
```

> The final commit carries the release-note message (`+ (Core) ...`). Earlier task commits use `-`; squash into the single release-note commit before opening the PR if a clean one-commit history is preferred.

---

### Task 5: Full verification and manual end-to-end test

**Files:** none (verification only).

- [ ] **Step 1: Full build**

Run the `/build` skill.
Expected: `Rock.sln` builds with no errors.

- [ ] **Step 2: Unit tests**

Run the `/test` skill.
Expected: all `AuthClientTests` PASS; no other tests regress.

- [ ] **Step 3: Obsidian lint**

Run: `npm run lint` in `Rock.JavaScript.Obsidian.Blocks`
Expected: clean.

- [ ] **Step 4: Manual end-to-end (from the spec's Verification Steps)**

1. In the OIDC client admin (AuthClient detail block), create/edit a client and enter **two** redirect URIs (one per row). Save. Confirm both persist and both render (newline-separated) in view mode.
2. Reopen the client: confirm both rows load into the `ValueList`.
3. Run an authorization-code flow against **each** registered URI → both succeed.
4. Run the flow with an **unregistered** `redirect_uri` → rejected ("redirect_uri is invalid").
5. Issue an authorize request that **omits** `redirect_uri` against the two-URI client → rejected ("required when multiple redirect URIs are registered"). Repeat against a one-URI client → accepted.
6. Save attempts that must be rejected with a clear message: an invalid scheme (`ftp://…`), a value containing a comma, and a relative URI. A duplicate entry must be de-duplicated silently.
7. Load a pre-existing single-URI client (created before this change): confirm it loads, edits, and authorizes unchanged.

- [ ] **Step 5: Pre-PR review**

Run `/code-review` on the branch. Address findings. Do **not** push or open a PR without explicit approval from the repo owner.

---

## Self-Review

**Spec coverage:**
- Multi-URI admin entry → Task 4 (`ValueList`). ✅
- No migration / single-URI backward compat → Tasks 1 (`GetRedirectUris` handles no-comma) + 5 step 4.7. ✅
- Absolute http/https validation → Task 1 (`IsValidRedirectUri`) + Task 3 step 5. ✅
- No literal comma in a URI → Task 1 + Task 3 step 5 (validates raw bag before join). ✅
- De-dup on save → Task 1 (`JoinRedirectUris`) + Task 3 step 4. ✅
- Omitted `redirect_uri` rule → Task 2. ✅
- No public signature break / `RedirectUri` stays string → Tasks only add members; column unchanged. ✅
- PostLogoutRedirectUri unchanged → Tasks 3/4 explicitly leave it. ✅

**Placeholder scan:** No TBD/TODO; every code step shows complete code. ✅

**Type consistency:** `GetRedirectUris()`/`JoinRedirectUris()`/`ValidateRedirectUris()` signatures match between Task 1 (definition) and Tasks 2–3 (use). Bag property `RedirectUris: List<string>` (Task 3) ↔ d.ts `redirectUris?: string[]` (Task 3) ↔ Vue `redirectUris` (Task 4). ✅
