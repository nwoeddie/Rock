# Parity Map — ScheduledTransactionListLiquid

**Source:** `RockWeb/Blocks/Finance/ScheduledTransactionListLiquid.ascx[.cs]` (517 lines)
**Classification:** Custom (`RockBlockType`)
**Summary:** Lists the current person's (and their related businesses') active `FinancialScheduledTransaction` records. Each row's visual is produced by a configurable Lava template (`Template` attribute, default `{% include '~~/Assets/Lava/ScheduledTransactionListLiquid.lava' %}`). Each row has Edit / Transfer / Delete actions rendered outside the Lava template. Delete inactivates via the gateway; it does not actually delete the row.

---

## Per-trace architectural summary

- **Trace 1 (Methods M1-M10).** `OnInit` / `OnLoad` / `Block_BlockUpdated` collapse into `GetObsidianBlockInitialization` + `onConfigurationValuesChanged(useReloadBlock())`. `rptScheduledTransactions_ItemDataBound` becomes a private `BuildItemBag(...)` invoked from `BuildBag()`. `bbtnDelete_Click` + `mdDeleteTransaction_SaveClick` collapse into a single `[BlockAction] CancelScheduledTransaction(string idKey)` triggered by an Obsidian `Modal` confirm. `btnEdit_Click` disappears (per-row `EditUrl` is precomputed in the bag). `lbAddScheduledTransaction_Click` disappears (Add button becomes a `NavigationUrlKey.EntryPage` URL). `ShowContent` becomes `BuildBag()`. `TransactionTypesFilter` becomes a local computed inside `BuildBag()` rather than an always-recomputed property.
- **Trace 2 (Queries Q1-Q16).** Main query keeps `Where(s => givingIds.Contains(s.AuthorizedPersonAlias.Person.GivingId) && s.IsActive)` but picks up the missing `.Include`s (`FinancialGateway`, `FinancialPaymentDetail.{CurrencyType,CreditCardType}Value`, `AuthorizedPersonAlias.Person`, `TransactionFrequencyValue`). The lazy `transactionSchedule.Transactions.Max(...)` is replaced by one batched `FinancialTransactionService.Queryable().Where(...).GroupBy(t => t.ScheduledTransactionId.Value).Select(...)`. `GetStatus(qry, true)` preserved. Optional gateway filter + transaction-type filter preserved verbatim. Delete path keeps `service.Get(id)` → `gateway.LoadAttributes` → `service.Cancel(...)` → try `service.GetStatus(single, ...)` → `SaveChanges`.
- **Trace 3 (Security S1).** Implicit: anonymous viewer renders nothing (server returns empty bag). No `IsAuthorized` calls in original — none added. The Cancel block action trusts the client-provided idKey, matching the WebForms hidden-field round-trip; ownership is not re-verified server-side. (Intentional — families share schedules; e.g., a spouse may need to cancel one the other set up.)
- **Trace 4 (Navigation N1-N10).** `[LinkedPage]` attributes keep their keys verbatim. Add button uses `NavigationUrls[NavigationUrlKey.EntryPage]`. Each item bag's `EditUrl` is precomputed in C# using the same Hosted/classic/Transfer routing logic as the WebForms. Page parameters `ScheduledTransactionGuid` and `transfer=true` preserved.
- **Trace 5 (State P1-P3).** WebForms hidden fields (`hfScheduledTransactionId`, `hfTransfer`, `hfBbtnDeleteId`) become item-bag fields + a Vue `pendingIdKey` ref. No `PersonPreference`, no `ViewState`.
- **Trace 6 (UI U1-U12).** Wrapper `div.scheduledtransaction-list`, per-row `div.scheduledtransaction-item`, action group, alert-info empty state, Modal confirm — all replicated. `lDebug` literal dropped (it was never written to). Edit-button accessibility attributes (`accesskey="m"`, `data-shortcut-key="e"`, `title="Alt+e"`) preserved.
- **Trace 7 (Second sweep).** Preserved nuances: event-registration transactions disable Edit; the Cancel-not-delete engineering note is forwarded into the Obsidian block; `CardIsExpired` / `Status` Lava merge fields keep semantics; pluralization (`TransactionLabel.Pluralize().ToLower()`) preserved for empty-state message; `TransactionTypes` filter preserves the "include null TransactionTypeValueId" behavior.

---

## Trace 1: Methods, event handlers, properties

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| M1 | `OnInit` (lines 159-166): wires `BlockUpdated`, calls `AddConfigurationUpdateTrigger(upnlContent)`. | | |
| M2 | `OnLoad` (lines 172-185): sets Add button text + visibility, captures `_transferToGatewayGuid`, calls `ShowContent()` on first load. | | |
| M3 | `Block_BlockUpdated` (lines 198-201): re-renders content. | | |
| M4 | `rptScheduledTransactions_ItemDataBound` (lines 208-332): builds merge-field dictionary per row, evaluates edit-button visibility / Transfer toggle, runs Lava template, sets row HTML. | | |
| M5 | `bbtnDelete_Click` (lines 339-345): captures button client ID into hidden field, opens confirm modal. | | |
| M6 | `mdDeleteTransaction_SaveClick` (lines 347-402): hides modal, resolves the row via hidden fields, loads gateway attrs, calls `service.Cancel`, tries `service.GetStatus`, saves changes, swaps row HTML to alert, hides buttons. | | |
| M7 | `btnEdit_Click` (lines 409-450): re-fetches transaction with fresh RockContext, routes to ScheduledTransactionEntryPage with `transfer=true` (when Transfer applies), else to hosted or classic edit page based on gateway mode. | | |
| M8 | `lbAddScheduledTransaction_Click` (lines 457-460): navigates to ScheduledTransactionEntryPage. | | |
| M9 | `ShowContent` (lines 469-513): builds giving-id list (person + businesses), runs main query with includes + filters, calls `GetStatus(qry, true)`, materializes, data-binds repeater, shows empty-state alert if zero. | | |
| M10 | `TransactionTypesFilter` property (line 149): returns the configured transaction-type defined-value guids, filtered to those that still exist in cache. | | |

---

## Trace 2: Data queries

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| Q1 | `personService.GetBusinesses(CurrentPerson.Id).Select(g => g.GivingId).ToList()` (line 479): fetch giving IDs of related businesses. | | |
| Q2 | Main `transactionService.Queryable().Include(TransactionTypeValue).Include(ScheduledTransactionDetails.Select(s => s.Account)).Where(givingIds.Contains(...) && IsActive)` (lines 484-487). UNDER-INCLUDES — see Q7-Q11. | | |
| Q3 | Optional `Where(s => s.FinancialGateway.Guid == gatewayFilterGuid)` (line 493). | | |
| Q4 | TransactionType filter: `Where(s => !s.TransactionTypeValueId.HasValue || TransactionTypesFilter.Contains(s.TransactionTypeValue.Guid))` (line 497). Intentionally includes null TransactionTypeValueId schedules. | | |
| Q5 | `transactionService.GetStatus(schedules, true)` (line 500): round-trips to gateway per active schedule to refresh `NextPaymentDate` / `IsActive`. | | |
| Q6 | **Lazy N+1:** `transactionSchedule.Transactions.Max(t => t.TransactionDateTime)` (line 273). One DB hit per row. To be replaced by one batched `GroupBy`. | | |
| Q7 | **Lazy load:** `transactionSchedule.FinancialGateway.Guid` (line 228). Eligible to be `.Include`d. | | |
| Q8 | **Lazy load:** `transactionSchedule.FinancialGateway.GetGatewayComponent()` (lines 239, 441). Component resolution — not a query per se but needs the FinancialGateway loaded. | | |
| Q9 | **Lazy load:** `transactionSchedule.AuthorizedPersonAlias.Person.FullName` (line 285). | | |
| Q10 | **Lazy load:** `transactionSchedule.FinancialPaymentDetail.{CurrencyTypeValue, CreditCardTypeValue, AccountNumberMasked, ExpirationDate, CardExpirationDate}` (lines 286-294). | | |
| Q11 | **Lazy load:** `transactionSchedule.TransactionFrequencyValue.{Value, Description}` (lines 290-291). | | |
| Q12 | Delete path: `fstService.Get(scheduledTransactionId.AsInteger())` (line 373). | | |
| Q13 | Delete path: `currentTransaction.FinancialGateway.LoadAttributes(rockContext)` (line 376). | | |
| Q14 | Delete path: `fstService.Cancel(currentTransaction, out errorMessage)` (line 380) — gateway round-trip. | | |
| Q15 | Delete path: `fstService.GetStatus(currentTransaction, out errorMessage)` (line 384) — refresh single status post-cancel, wrapped in try/catch/ignore. | | |
| Q16 | `btnEdit_Click`: `new FinancialScheduledTransactionService(new RockContext()).Get(scheduledTransactionId)` (line 422) — creates a fresh context. To be removed (URL precomputed at init). | | |

---

## Trace 3: Security checks

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| S1 | `if (CurrentPerson != null)` (line 472): implicit auth — anonymous viewers see nothing. No explicit `IsUserAuthorized` / entity-level `IsAuthorized` calls anywhere. | | |

---

## Trace 4: Navigation and page parameters

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| N1 | `[LinkedPage] ScheduledTransactionEntryPage` (lines 51-56). Used by Add and Transfer routing. | | |
| N2 | `[LinkedPage] ScheduledTransactionEditPage` (lines 87-91). | | |
| N3 | `[LinkedPage] ScheduledTransactionEditPageHosted` (lines 93-98). | | |
| N4 | `LinkedPageRoute(AttributeKey.ScheduledTransactionEditPage*)` (lines 318-322): passed as `EditPage` merge field for Lava. | | |
| N5 | `NavigateToLinkedPage(AttributeKey.ScheduledTransactionEntryPage, qryParams)` with `transfer=true` (line 436). | | |
| N6 | `NavigateToLinkedPage(AttributeKey.ScheduledTransactionEditPageHosted, qryParams)` (line 443). | | |
| N7 | `NavigateToLinkedPage(AttributeKey.ScheduledTransactionEditPage, qryParams)` (line 447). | | |
| N8 | `NavigateToLinkedPage(AttributeKey.ScheduledTransactionEntryPage)` from Add button (line 459). | | |
| N9 | `PageParameter "ScheduledTransactionGuid"` (lines 429-433). | | |
| N10 | `PageParameter "transfer" = "true"` (line 435). | | |

---

## Trace 5: User preferences and state

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| P1 | `hfBbtnDeleteId` hidden field: tracks which row's Delete button opened the modal. Drops — Vue ref `pendingIdKey` instead. | | |
| P2 | `hfScheduledTransactionId` per row: row's transaction ID for round-trip. Drops — `idKey` on item bag instead. | | |
| P3 | `hfTransfer` per row: marks Transfer routing. Drops — `isTransfer` boolean (or just `editUrl` resolved at init) on item bag instead. | | |

---

## Trace 6: UI behaviors

| # | WebForms Method/Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| U1 | `<div class="scheduledtransaction-list">` wrapper (line 6 of .ascx). | | |
| U2 | `<div class="scheduledtransaction-item">` per-row wrapper (line 9 of .ascx). | | |
| U3 | Lava-rendered per-row content via `<asp:Literal ID="lLavaContent">`. | | |
| U4 | `<div class="actions">` containing Edit + Delete (line 13 of .ascx). | | |
| U5 | Edit button: `data-shortcut-key="e"`, `AccessKey="m"`, `ToolTip="Alt+e"`, text "Edit" or `TransferButtonText` when Transfer applies. | | |
| U6 | Delete button uses `Rock:BootstrapButton` (loading state on click). | | |
| U7 | ModalDialog "Are you sure?" with "Delete" save text (line 21 of .ascx). | | |
| U8 | `pnlNoScheduledTransactions` (alert-info) with "No {label}s currently exist." (lines 29-31 of .ascx). | | |
| U9 | `lbAddScheduledTransaction` primary button (line 33 of .ascx) — text "Create New {TransactionLabel}". | | |
| U10 | Edit-button visibility logic (lines 238-261): hidden for event-registration transactions OR when the corresponding edit page is empty. | | |
| U11 | Per-row delete-success / delete-error swap (lines 392-397): replaces row's `lLavaContent` with an alert div and hides both action buttons. | | |
| U12 | `lDebug` literal (line 35 of .ascx) — never visibly written to. Drops. | | |

---

## Trace 7: Second sweep

| # | Behavior | Obsidian Equivalent (planned) | Verdict (planned) |
|---|---|---|---|
| T7-1 | Default Lava template reference: `{% include '~~/Assets/Lava/ScheduledTransactionListLiquid.lava' %}`. Resolves per-theme. | | |
| T7-2 | Event-registration transactions: `useHostedGatewayEditPage` is bypassed; Edit button hidden unconditionally for these. | | |
| T7-3 | Transfer toggle is a binder-side mutation in WebForms (Lava template never sees a Transfer flag). In Obsidian, surfaces as `isTransfer` on the item bag for routing only. | | |
| T7-4 | `Block_BlockUpdated` + `AddConfigurationUpdateTrigger(upnlContent)` together repaint on config change — equivalent to `onConfigurationValuesChanged(useReloadBlock())`. | | |
| T7-5 | Cancel-not-delete engineering note (lines 349-360). Forward into the new block — same business rule applies. | | |
| T7-6 | Success-alert wording: `<div class='alert alert-success'>Your scheduled {label} has been deleted.</div>` (note: "deleted" wording despite Cancel). Preserve verbatim. | | |
| T7-7 | Error-alert wording: `<div class='alert alert-danger'>An error occurred while deleting your scheduled transaction. Message: {errorMessage}</div>`. Preserve verbatim. | | |
| T7-8 | Empty-state message uses `TransactionLabel.Pluralize().ToLower()` (line 510). Preserve. | | |
| T7-9 | Lava merge-field `CurrencyType` carries the DefinedValue's `Value` (display name) — Lava does case-sensitive comparison `!= "Credit Card"`. Don't change to Guid. | | |
| T7-10 | `FinancialPaymentDetail.CardExpirationDate < RockDateTime.Now` for `CardIsExpired` (line 294). `FinancialPaymentDetail` is accessed without null-check — a row with no payment detail would NRE. Worth guarding in the Obsidian rewrite. | | |
| T7-11 | `TransactionTypesFilter` property (line 149) is a getter computed on every access. Materialize once at the top of `BuildBag()` to avoid repeat work. | | |
