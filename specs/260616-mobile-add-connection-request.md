---
author: Panha Sim
date_created: 2026-06-16
summary: >-
  Backend (Rock RMS server) spec for the new mobile Add Connection Request V2
  block: the server-side mobile RockBlockType that backs the native Add Request
  cover sheet launched from the Connection Opportunity Detail block. Surfaces the
  cascading dropdown options (types, opportunities-for-type, sources-for-type,
  statuses-for-opportunity, connectors-for-opportunity-and-campus), validates and
  saves a new ConnectionRequest, returns the new IdKey, and delivers the
  Post Save Action navigation setting that the shell honors after a successful
  save. Replaces (alongside) the existing AddConnectionRequest block (1380115A),
  per the locked V2 convention. The mobile shell UI is specified separately in
  the RM repo.
contributors: []
---

# Add Connection Request V2 (Mobile) Backend

## Summary

This is the backend half of the new mobile **Add Connection Request V2** block, the sixth block in the Connections revamp port (after the [Connection Type List V2](260608-mobile-connection-type-list.md), [Connection Opportunity List V2](260608-mobile-connection-type-detail.md), [Connection Opportunity Detail](260609-mobile-connection-opportunity-detail.md), [My Connection Requests](260609-mobile-my-connection-requests.md), and [Connection Request Detail](260610-mobile-connection-request-detail.md)). It is the screen a connector lands on after tapping the floating **"Add Connection Request"** button on the Connection Opportunity Detail block: a cover-sheet form that lets them create a new `ConnectionRequest`. This spec covers only the server-side code in the Develop repo: a new mobile `RockBlockType` that returns the cascading option lists (types, opportunities, sources, statuses, connectors), validates and saves the new request, returns the new request's IdKey, and delivers a `Post Save Action` (`MobileNavigationActionField`) setting the shell honors after save. The mobile shell (the cover sheet, the form fields, validation UI, persistence) is specified separately in [the mobile shell spec](../../RM/specs/260616-mobile-add-connection-request-shell.md) in the RM repo. The two halves share one `BlockTypeGuid`.

Per the locked V2 convention for this initiative, the new block is named **`AddConnectionRequestV2`** (mirroring the existing native mobile `AddConnectionRequest` block at `1380115A-...`, which is left intact for backward compatibility). The display name carries the "V2" suffix in line with blocks 1 and 2 (`Connection Type List V2`, `Connection Opportunity List V2`).

## Motivation

- The new [Connection Opportunity Detail](260609-mobile-connection-opportunity-detail.md) screen surfaces a floating "Add Connection Request" button that needs a destination. Reusing the existing native mobile `AddConnectionRequest` block (`1380115A`) is rejected for the same reason the rest of this initiative used new blocks: it predates the revamp, ships with `HeaderTemplate` / form Lava-attribute customizations, and depends on legacy contracts (page parameters keyed off `Guid`s, not IdKeys; older `LaunchPacket`-shaped configuration).
- The web Connections Hub uses a focused [Add Connection Request modal](../Rock.JavaScript.Obsidian.Blocks/src/Engagement/ConnectionsHub/addConnectionRequestModal.partial.obs) (Obsidian) backed by `ConnectionsHub.GetConnectionRequestForAddOptions` / `SaveConnectionRequest`. Mobile v1 ports that focused flow as its own block.
- Backward compatibility is a hard rule, so the new block ships alongside the existing `AddConnectionRequest` rather than replacing it.

## Scope

- In scope:
  - Cascading option queries: types the user can see, opportunities for a chosen type, request sources for a chosen type, available statuses for a chosen opportunity, available connectors for a chosen opportunity (campus-scoped when a campus is set).
  - Initial-load context: when launched with a `ConnectionOpportunity` IdKey page parameter, return the opportunity's Type **already resolved and locked** (the shell shows them read-only); when launched without context, return only the types and let the shell drive the cascade from there.
  - Validation + persistence: create a new `ConnectionRequest`, set fields, save, return the new IdKey. Validate: requester required; opportunity required; status required (must belong to the opportunity's type); state required (default Active); when state is Future Follow-Up, a follow-up date is required.
  - Security: VIEW on the chosen `ConnectionType` to even see it in the cascade; **EDIT** on the type to save (matches the web's `IsAuthorized(EDIT)` gate in `ConnectionsHub.SaveConnectionRequest`).
  - The `Post Save Action` (`MobileNavigationActionField`) block setting, delivered through `Configuration` and surfaced verbatim on save so the shell can pop / push / pop-and-push the new request's IdKey per the admin's configuration. Matches [AddContact.cs](../Rock/Blocks/Types/Mobile/Engagement/AddContact.cs).
- Out of scope (this spec):
  - All mobile UI, which lives in the shell spec.
  - The Connections Hub "Add" features not in the mockup: editable custom attributes on the Add form (deferred to [Connection Request Detail](260610-mobile-connection-request-detail.md)); group placement; manual workflow launch; activity creation; create-new-person inline from the Requester picker; request security editing.
  - The Connection Opportunity Detail block's floating Add button itself (lives in that block — see [260609-mobile-connection-opportunity-detail.md](260609-mobile-connection-opportunity-detail.md)).
  - Re-styling the existing `AddConnectionRequest` block — left intact.

## Requirements

### Functional (server)

- The block MUST accept an **optional** `ConnectionOpportunity` page parameter (IdKey). When present, the block MUST resolve the opportunity (`ConnectionOpportunityCache.Get( idKey, ... )` overload), authorize the current person against the opportunity's `ConnectionType` (`VIEW` to see the form; `EDIT` to save), and surface the opportunity (and its Type) as the form's locked initial selection (the shell renders both read-only). A missing / inactive opportunity → `ActionBadRequest`. Unauthorized → `ActionUnauthorized`.
- When launched **without** a `ConnectionOpportunity` page parameter, the block MUST surface the list of types the current person has `VIEW` on, with no opportunity / source / status / connector pre-selection (the shell drives the full cascade).
- The block MUST expose a `GetOptions` block action that returns the option set for the current cascade selection. The request bag carries the current `TypeIdKey?`, `OpportunityIdKey?`, and `CampusGuid?`; the response carries everything the shell needs to populate dropdowns:

  | Returned field | Source / behavior |
  |---|---|
  | `Types` | All `ConnectionTypeCache.All()` the user has `VIEW` on, ordered by `Order` / `Name`, mapped to `{ Guid, Name, IconCssClass }`. Always returned. |
  | `Opportunities` | Empty unless `TypeIdKey` is set; then the type's `ConnectionOpportunities` the user has `VIEW` on, active only, ordered, mapped to `{ Guid, Name, IconCssClass }`. |
  | `RequestSources` | Empty unless `TypeIdKey` is set; then `ConnectionType.ConnectionTypeSources` (active, ordered), mapped to `{ Guid, Name }`. ([web parity](../Rock.Blocks/Engagement/ConnectionsHub.cs) — `ConnectionTypeOptionsBag.requestSourceItems`.) |
  | `AvailableStatuses` | Empty unless `OpportunityIdKey` is set; then the opportunity's type's `ConnectionStatuses` (active, ordered by `Order`), mapped to `{ Guid, Name, Color }` (`Color` from `HighlightColor`). |
  | `AvailableConnectors` | Empty unless `OpportunityIdKey` is set; then the opportunity's available connectors per the web's rule (people in any `ConnectionOpportunityConnectorGroup` of the chosen opportunity, filtered by `CampusGuid` when set — null-campus groups remain available regardless of `CampusGuid`). Mapped to `{ PersonAliasIdKey, FullName, PhotoUrl, CampusName? }`. Reuses the existing helper `ConnectionOpportunity.GetConnectorPeople( campusId )` if present, otherwise replicates its query against `ConnectionOpportunityConnectorGroups`. |
  | `Campuses` | `CampusCache.All()`, active only, ordered, mapped to `ListItemViewModel`. Always returned (the shell caches it after the first call). |
  | `DefaultState` | The type's `DefaultConnectionState` (or `Active` when unset). Returned only when `TypeIdKey` is set so the shell can prefill State to a sensible default on type selection. |
  | `DefaultStatusGuid` | The opportunity's type's default status (`ConnectionStatuses.FirstOrDefault( s => s.IsDefault )?.Guid`). Returned only when `OpportunityIdKey` is set so the shell can prefill Status. |
  | `LockedContext` | `{ TypeIdKey, OpportunityIdKey }` — populated **only** on the very first `GetOptions` call (offset / cascade-init) when the block has an incoming `ConnectionOpportunity` page parameter. The shell uses this to know which fields to render read-only. Empty `{}` otherwise. |

  The action MUST be safe to call repeatedly as the user cascades through the form; subsequent calls do not return `LockedContext`.

- The block MUST expose a `SaveConnectionRequest` block action that takes a save bag and creates the request:

  ```csharp
  public class SaveConnectionRequestBag
  {
      public string RequesterPersonAliasIdKey { get; set; }       // required
      public string OpportunityIdKey { get; set; }                // required (Type derives from Opportunity)
      public string ConnectorPersonAliasIdKey { get; set; }       // optional (null = no connector)
      public Guid? ConnectionTypeSourceGuid { get; set; }         // optional
      public ConnectionState State { get; set; }                  // required, default Active
      public DateTime? FollowupDate { get; set; }                 // required when State == FutureFollowUp
      public Guid? CampusGuid { get; set; }                       // optional
      public Guid StatusGuid { get; set; }                        // required, must belong to the opportunity's type
      public string Comments { get; set; }                        // optional
  }
  ```

  Validation MUST be performed server-side (even if the shell mirrors the same rules): unknown / inactive opportunity → `ActionBadRequest`; `EDIT` auth on the type → required, else `ActionUnauthorized`; requester resolves → required; status belongs to the opportunity's type → required; state is one of `Active` / `Inactive` / `FutureFollowUp` (Connected is not creatable here, matches web `ConnectionsHub.SaveConnectionRequest`); `State == FutureFollowUp` → `FollowupDate.HasValue`; `ConnectionTypeSourceGuid` if provided belongs to the type. On success the action returns `{ ConnectionRequestIdKey, PostSaveAction }` where `PostSaveAction` is the same `MobileNavigationActionViewModel` already delivered through `Configuration` (returning it again on save lets the shell honor it without re-reading configuration, and gives a future hook to vary post-save behavior).

- The block MUST deliver the `Post Save Action` (`MobileNavigationActionField`) block setting via `GetMobileConfigurationValues()` as a deserialized `MobileNavigationActionViewModel`. Default value: `MobileNavigationActionFieldAttribute.PopSinglePageValue` (close the Add cover sheet on save and return to the caller — block 3 then refreshes on resume). Pattern matches [AddContact.cs:44](../Rock/Blocks/Types/Mobile/Engagement/AddContact.cs).

### Non-functional / conventions (server)

- A single new `BlockTypeGuid` MUST be embedded identically as a string literal in both repos.
- Server block: `RockBlockType`, `[SupportedSiteTypes( Model.SiteType.Mobile )]`, under `Develop/Rock.Blocks/Mobile/Connection/` (namespace `Rock.Blocks.Mobile.Connection`), with new `EntityTypeGuid` and `BlockTypeGuid` inline string literals (no SystemGuid constants — matching how blocks 1 and 2 shipped, per the scaffolder convention).
- The block declares `RequiredMobileVersion => new Version( 1, 20 )` (mobile shell v20). The feature ships in Rock core v20.
- Leave the existing server mobile block (`1380115A-...`) intact.
- Follow [Develop/CLAUDE.md](../CLAUDE.md): `RockContext` lifetime, `RockDateTime`, no `System.Web` in shared code, avoid `Guid` in `.Where()` when an `Id` from a cached item is available. Attributes declared per [block-architecture.md](../.claude/rules/block-architecture.md).

## Design

### Server block identity and placement

| Piece | Path | Notes |
|---|---|---|
| New server block | `Develop/Rock.Blocks/Mobile/Connection/AddConnectionRequestV2.cs` | Class `AddConnectionRequestV2`, `[DisplayName("Add Connection Request V2")]`, new `EntityTypeGuid` + `BlockTypeGuid` inline string literals. |
| Old server block | `Develop/Rock/Blocks/Types/Mobile/Connection/AddConnectionRequest.cs` (`1380115A-...`) | Untouched. |

The user-facing `DisplayName` carries the V2 suffix (`Add Connection Request V2`), parallel to `Connection Type List V2` and `Connection Opportunity List V2`. The same `BlockTypeGuid` literal is shared with the mobile block (see shell spec).

### Block settings (attributes)

For v1 the block exposes one setting. Declared per [block-architecture.md](../.claude/rules/block-architecture.md): each `FieldAttribute` assigned vertically by property, with the keys in a nested `AttributeKey` static class.

| Setting | Field type | Key | Default | Purpose |
|---|---|---|---|---|
| Post Save Action | `MobileNavigationActionField` | `PostSaveAction` | `MobileNavigationActionFieldAttribute.PopSinglePageValue` | The navigation action the shell performs after a successful save. Default pops the Add cover sheet and returns to the caller (Connection Opportunity Detail), which then refreshes. Admins may override to also push the new request's Connection Request Detail page using the returned IdKey as a parameter. Pattern matches [AddContact.cs:44](../Rock/Blocks/Types/Mobile/Engagement/AddContact.cs). |

The page parameter `ConnectionOpportunity` (PascalCase IdKey, optional) is **not** a block setting — it is read from the request's incoming page parameters as the locked-context hint.

### Data flow

```mermaid
sequenceDiagram
    participant M as Mobile block (shell spec)
    participant S as Server mobile block (this spec)
    participant DB as RockContext

    Note over M: On launch (with optional ConnectionOpportunity page param)
    M->>S: BlockAction GetOptions( bag { TypeIdKey?, OpportunityIdKey?, CampusGuid? } )
    Note right of S: resolve opportunity (when LockedContext is being seeded),<br/>authorize VIEW on type
    S->>DB: Type list / opportunity list / sources / statuses / connectors as needed
    DB-->>S: rows
    S-->>M: GetOptionsResponseBag { Types, Opportunities, RequestSources, AvailableStatuses, AvailableConnectors, Campuses, DefaultState?, DefaultStatusGuid?, LockedContext? }

    Note over M: User changes Type / Opportunity / Campus → another GetOptions call

    Note over M: User taps Save
    M->>S: BlockAction SaveConnectionRequest( saveBag )
    Note right of S: validate; authorize EDIT on type;<br/>create ConnectionRequest; save
    S->>DB: INSERT ConnectionRequest
    DB-->>S: new Id
    S-->>M: SaveConnectionRequestResponseBag { ConnectionRequestIdKey, PostSaveAction }
```

### Cascade and option queries

The block uses `ConnectionTypeCache` / `ConnectionOpportunityCache` throughout — no per-call DB hits for the type / opportunity / status definitions. Indicative `GetOptions` body:

```csharp
public BlockActionResult GetOptions( GetOptionsRequestBag request )
{
    var currentPerson = GetCurrentPerson();
    var disablePredictableIds = !PageCache.Layout.Site.DisablePredictableIds;
    var response = new GetOptionsResponseBag();

    // Resolve incoming locked context (the very first call with an incoming page param).
    var incomingOpportunityIdKey = PageParameter( PageParameterKey.ConnectionOpportunity );
    var seedLockedContext = !string.IsNullOrEmpty( incomingOpportunityIdKey )
        && string.IsNullOrEmpty( request.OpportunityIdKey );

    if ( seedLockedContext )
    {
        var lockedOpp = ConnectionOpportunityCache.Get( incomingOpportunityIdKey, disablePredictableIds );
        if ( lockedOpp == null || !lockedOpp.IsActive ) return ActionBadRequest( "Opportunity not found." );
        if ( !lockedOpp.ConnectionType.IsAuthorized( Authorization.VIEW, currentPerson ) ) return ActionUnauthorized();

        request.TypeIdKey = lockedOpp.ConnectionType.IdKey;
        request.OpportunityIdKey = lockedOpp.IdKey;
        response.LockedContext = new LockedContextBag { TypeIdKey = request.TypeIdKey, OpportunityIdKey = request.OpportunityIdKey };
    }

    // Types the user can see (always).
    response.Types = ConnectionTypeCache.All()
        .Where( t => t.IsActive && t.IsAuthorized( Authorization.VIEW, currentPerson ) )
        .OrderBy( t => t.Order ).ThenBy( t => t.Name )
        .Select( t => new ConnectionTypeOptionBag { Guid = t.Guid, Name = t.Name, IconCssClass = t.IconCssClass } )
        .ToList();

    response.Campuses = CampusCache.All()
        .Where( c => c.IsActive == true )
        .OrderBy( c => c.Order ).ThenBy( c => c.Name )
        .Select( c => new ListItemViewModel { Value = c.Guid.ToString(), Text = c.Name } )
        .ToList();

    // Type-scoped: opportunities + sources + default state.
    ConnectionTypeCache type = null;
    if ( !string.IsNullOrEmpty( request.TypeIdKey ) )
    {
        type = ConnectionTypeCache.Get( request.TypeIdKey, disablePredictableIds );
        if ( type == null || !type.IsAuthorized( Authorization.VIEW, currentPerson ) ) return ActionUnauthorized();

        response.Opportunities = type.ConnectionOpportunities
            .Where( o => o.IsActive && o.IsAuthorized( Authorization.VIEW, currentPerson ) )
            .OrderBy( o => o.Order ).ThenBy( o => o.Name )
            .Select( o => new ConnectionOpportunityOptionBag { Guid = o.Guid, Name = o.Name, IconCssClass = o.IconCssClass } )
            .ToList();

        // ConnectionTypeSource entries belong to the Type (not the opportunity).
        response.RequestSources = type.ConnectionTypeSources?
            .Where( s => s.IsActive )
            .OrderBy( s => s.Order ).ThenBy( s => s.Name )
            .Select( s => new ListItemViewModel { Value = s.Guid.ToString(), Text = s.Name } )
            .ToList() ?? new List<ListItemViewModel>();

        response.DefaultState = type.DefaultConnectionState; // ConnectionState? (nullable in some configurations)
    }

    // Opportunity-scoped: statuses + connectors + default status.
    ConnectionOpportunityCache opportunity = null;
    if ( !string.IsNullOrEmpty( request.OpportunityIdKey ) )
    {
        opportunity = ConnectionOpportunityCache.Get( request.OpportunityIdKey, disablePredictableIds );
        if ( opportunity == null || !opportunity.IsActive ) return ActionBadRequest( "Opportunity not found." );
        if ( !opportunity.ConnectionType.IsAuthorized( Authorization.VIEW, currentPerson ) ) return ActionUnauthorized();

        response.AvailableStatuses = opportunity.ConnectionType.ConnectionStatuses
            .Where( s => s.IsActive )
            .OrderBy( s => s.Order )
            .Select( s => new ConnectionStatusItemBag { Name = s.Name, Value = s.Guid, Color = s.HighlightColor } )
            .ToList();

        response.DefaultStatusGuid = opportunity.ConnectionType.ConnectionStatuses
            .FirstOrDefault( s => s.IsDefault )?.Guid;

        // Available connectors: web parity — members of the opportunity's connector groups, filtered by campus.
        var campusId = request.CampusGuid.HasValue ? CampusCache.GetId( request.CampusGuid.Value ) : null;
        response.AvailableConnectors = GetAvailableConnectorBags( opportunity, campusId );
    }

    return ActionOk( response );
}
```

`GetAvailableConnectorBags` walks `ConnectionOpportunity.ConnectionOpportunityConnectorGroups` (the existing entity), filters groups by the requested campus (null-campus groups are always available — web parity, [ConnectionsHub.cs `GetConnectorOptions`](../Rock.Blocks/Engagement/ConnectionsHub.cs)), enumerates active group members, projects each as `{ PersonAliasIdKey, FullName, PhotoUrl, CampusName }`, distincts by `PersonAliasId`, and orders by `LastName / NickName`. The query SHOULD be a single LINQ statement with batched person/photo data; never one query per group member.

### Save

```csharp
public BlockActionResult SaveConnectionRequest( SaveConnectionRequestBag bag )
{
    var currentPerson = GetCurrentPerson();
    var disablePredictableIds = !PageCache.Layout.Site.DisablePredictableIds;

    // Resolve + authorize.
    var opportunityCache = ConnectionOpportunityCache.Get( bag.OpportunityIdKey, disablePredictableIds );
    if ( opportunityCache == null || !opportunityCache.IsActive ) return ActionBadRequest( "Opportunity not found." );
    if ( !opportunityCache.ConnectionType.IsAuthorized( Authorization.EDIT, currentPerson ) ) return ActionUnauthorized();

    // Validate.
    if ( bag.RequesterPersonAliasIdKey.IsNullOrWhiteSpace() ) return ActionBadRequest( "Requester is required." );
    if ( bag.StatusGuid == Guid.Empty ) return ActionBadRequest( "Status is required." );
    var status = opportunityCache.ConnectionType.ConnectionStatuses.FirstOrDefault( s => s.Guid == bag.StatusGuid );
    if ( status == null ) return ActionBadRequest( "Status does not belong to this opportunity's type." );
    if ( bag.State == ConnectionState.Connected ) return ActionBadRequest( "New requests cannot be created in the Connected state." );
    if ( bag.State == ConnectionState.FutureFollowUp && !bag.FollowupDate.HasValue ) return ActionBadRequest( "A follow-up date is required for Future Follow-Up requests." );

    using ( var rockContext = new RockContext() )
    {
        var personAliasService = new PersonAliasService( rockContext );
        var requesterAliasId = personAliasService.GetId( IdHasher.Instance.GetId( bag.RequesterPersonAliasIdKey ) ?? 0 );
        if ( !requesterAliasId.HasValue ) return ActionBadRequest( "Requester not found." );

        int? connectorAliasId = null;
        if ( !bag.ConnectorPersonAliasIdKey.IsNullOrWhiteSpace() )
        {
            connectorAliasId = personAliasService.GetId( IdHasher.Instance.GetId( bag.ConnectorPersonAliasIdKey ) ?? 0 );
        }

        int? sourceId = null;
        if ( bag.ConnectionTypeSourceGuid.HasValue )
        {
            sourceId = opportunityCache.ConnectionType.ConnectionTypeSources?
                .FirstOrDefault( s => s.Guid == bag.ConnectionTypeSourceGuid.Value )?.Id;
        }

        var request = new ConnectionRequest
        {
            ConnectionOpportunityId = opportunityCache.Id,
            PersonAliasId = requesterAliasId.Value,
            ConnectorPersonAliasId = connectorAliasId,
            ConnectionTypeSourceId = sourceId,
            ConnectionState = bag.State,
            FollowupDate = bag.State == ConnectionState.FutureFollowUp ? bag.FollowupDate : null,
            CampusId = bag.CampusGuid.HasValue ? CampusCache.GetId( bag.CampusGuid.Value ) : null,
            ConnectionStatusId = status.Id,
            Comments = bag.Comments
        };

        new ConnectionRequestService( rockContext ).Add( request );
        rockContext.SaveChanges();

        return ActionOk( new SaveConnectionRequestResponseBag
        {
            ConnectionRequestIdKey = request.IdKey,
            PostSaveAction = GetAttributeValue( AttributeKey.PostSaveAction ).FromJsonOrNull<MobileNavigationActionViewModel>() ?? new MobileNavigationActionViewModel()
        } );
    }
}
```

The save block deliberately does **not** create system activity records, fire workflows, launch reminders, or set placement groups. Those belong to the [Connection Request Detail](260610-mobile-connection-request-detail.md) edit flow.

### Contract returned

The bags and enums live in `Rock.Common.Mobile` (RM repo) and are defined in full in the [shell spec](../../RM/specs/260616-mobile-add-connection-request-shell.md). The server references the built `Rock.Common.Mobile.dll` in `RockWeb/Bin`. Property names MUST match the mobile definitions exactly. What the server populates:

- `GetOptionsRequestBag` { `TypeIdKey?`, `OpportunityIdKey?`, `CampusGuid?` }.
- `GetOptionsResponseBag` { `List<ConnectionTypeOptionBag> Types`, `List<ConnectionOpportunityOptionBag> Opportunities`, `List<ListItemViewModel> RequestSources`, `List<ConnectionStatusItemBag> AvailableStatuses`, `List<ConnectorOptionBag> AvailableConnectors`, `List<ListItemViewModel> Campuses`, `ConnectionState? DefaultState`, `Guid? DefaultStatusGuid`, `LockedContextBag? LockedContext` }.
- `ConnectionTypeOptionBag` { `Guid`, `Name`, `IconCssClass` }.
- `ConnectionOpportunityOptionBag` { `Guid`, `Name`, `IconCssClass` }.
- `ConnectionStatusItemBag` — **reused** from the Connection Opportunity Detail spec (`Rock.Common.Mobile/Blocks/Connection/ConnectionOpportunityDetail/`); same `{ Name, Value (Guid), Color }` shape. Not redefined here.
- `ConnectorOptionBag` { `PersonAliasIdKey`, `FullName`, `PhotoUrl`, `CampusName` }.
- `LockedContextBag` { `TypeIdKey`, `OpportunityIdKey` }.
- `SaveConnectionRequestBag` (shown above).
- `SaveConnectionRequestResponseBag` { `ConnectionRequestIdKey`, `MobileNavigationActionViewModel PostSaveAction` }.
- `Configuration` (returned from `GetMobileConfigurationValues`) { `MobileNavigationActionViewModel PostSaveAction` }.

`ConnectionState` already exists in `Rock.Common.Mobile/Enums/` and is reused.

### Security

- `VIEW` on the chosen `ConnectionType` to surface its opportunities / sources / statuses / connectors during the cascade (server-side filter inside `GetOptions`).
- `EDIT` on the chosen `ConnectionType` to save (`SaveConnectionRequest`). The web's `ConnectionsHub.SaveConnectionRequest` does the same — `EDIT` on the type, not on the opportunity.
- The block does **not** consider `ConnectionType.EnableRequestSecurity` (per-request auth) on add — a request that does not yet exist has nothing to authorize against. The per-request gate, if turned on, takes effect on subsequent reads/edits via the Connection Request Detail block.

## Open Questions (backend)

1. **Future Follow-Up date — required on add?** (Confirm.) The web Add modal shows a follow-up date picker only when State = `FutureFollowUp` and requires it; the mobile mockup does not show the picker, but creating a Future-Follow-Up request without a date leaves `FollowupDate` null, which the rest of Rock (including this initiative's Due-status math) treats as malformed. This spec **defaults to web parity**: when State = `FutureFollowUp`, a follow-up date is required and the shell renders a conditional date picker. Flag if the preference is to drop Future Follow-Up from the State dropdown on Add entirely (and force users to set it later via the detail screen).
2. **Connector "None"** — the web allows saving a request with no connector. This spec also allows null; confirm.
3. **Create-new-person inline from the Requester picker.** Web's modal can spawn a "new person" sub-form. Mobile v1 is PersonPicker-only (must pick an existing person). Confirm deferral.
4. **Block name suffix.** Default decided: `AddConnectionRequestV2` + display name `Add Connection Request V2` (matches blocks 1 and 2). Confirm at implementation start (the V2 convention is locked but the exact class name is settled per block).

**Resolved 2026-06-16 (with Panha):**
- **Launched-from-block-3 context:** when the incoming `ConnectionOpportunity` page parameter is set, Type and Opportunity are **prefilled and locked** (read-only). The shell still surfaces the chosen Type icon + name and Opportunity name so users can see what they're adding a request to.
- **Connector availability:** follows web parity — members of the opportunity's `ConnectionOpportunityConnectorGroups`, filtered by the chosen Campus (groups with null campus are always available).
- **Request Source:** `ConnectionRequest.ConnectionTypeSourceId` is FK to **`ConnectionTypeSource`** (a per-`ConnectionType` model — not a `DefinedValue`). Sources cascade off the selected Type and are rendered as a simple dropdown labeled "Request Source", matching the web.
- **Post-save navigation:** controlled by a `MobileNavigationActionField` block setting named "Post Save Action", default `PopSinglePageValue` (close the cover sheet and return to block 3). Matches [AddContact.cs](../Rock/Blocks/Types/Mobile/Engagement/AddContact.cs); the new request's IdKey is returned alongside so the action can push to the Connection Request Detail page if the admin configured it that way.

## Considered but Rejected (backend)

### Revamp the existing `AddConnectionRequest` block in place
Rejected (product decision). The existing block predates the revamp and ships with Lava-attribute customizations; rewriting it would break deployments. Per the locked V2 convention, this initiative ships new blocks alongside the old ones. Same call as blocks 1, 2, 3, and 5.

### Hardcode post-save behavior (close + reload)
Rejected. Some deployments will want users to land on the new request's detail screen immediately; others will want to keep adding requests in a row. The `MobileNavigationActionField` setting (same pattern as `AddContact`) covers both without forking the block.

### Single `GetForm( opportunityIdKey )` call that returns the full option set eagerly
Rejected. A connector with access to many types/opportunities/connectors would pay for the entire matrix on every load. Lazy cascading (`GetOptions` per selection change) is the shell pattern of choice for mobile forms — same shape `AddContact` uses for its destination loading.

### Surface custom attribute editing on Add
Rejected for v1 — out of scope (the mockup does not include custom attributes). Custom attributes are editable on the Connection Request Detail screen (block 5) after the request exists. Adding them here would couple the Add flow to the attribute-editor patterns (`ClientEditableAttributeValueViewModel` / `AttributeValueEditor`) and bloat the form.

### Create-new-person inline from the Requester picker
Rejected for v1 — out of scope. The PersonPicker requires an existing person; partners that need to create-then-add can do so from the Person profile entry and come back. Re-evaluate if the friction shows up in usage.

### Launch workflows, place groups, or write activities on save
Rejected for v1. The web Add modal also doesn't surface these — they live on the docked panel / detail screen. Block 5 (Connection Request Detail) owns them.

## Related

- Web Add modal (reference for fields + cascade): [Develop/Rock.JavaScript.Obsidian.Blocks/src/Engagement/ConnectionsHub/addConnectionRequestModal.partial.obs](../Rock.JavaScript.Obsidian.Blocks/src/Engagement/ConnectionsHub/addConnectionRequestModal.partial.obs).
- Web server actions (reference for save semantics): [Develop/Rock.Blocks/Engagement/ConnectionsHub.cs](../Rock.Blocks/Engagement/ConnectionsHub.cs) — `GetConnectionRequestForAddOptions`, `SaveConnectionRequest`.
- MobileNavigationAction pattern: [Develop/Rock/Blocks/Types/Mobile/Engagement/AddContact.cs](../Rock/Blocks/Types/Mobile/Engagement/AddContact.cs) (the `PostSave` setting / `MobileNavigationActionViewModel` flow).
- Old mobile server block (left intact): [Develop/Rock/Blocks/Types/Mobile/Connection/AddConnectionRequest.cs](../Rock/Blocks/Types/Mobile/Connection/AddConnectionRequest.cs).
- Sibling backend specs: [Connection Opportunity Detail](260609-mobile-connection-opportunity-detail.md) (the caller — its `Add Page` linked-page setting points at this block), [Connection Request Detail](260610-mobile-connection-request-detail.md) (the post-save target when admins configure the Post Save Action to push detail).
- Mobile shell spec: [../../RM/specs/260616-mobile-add-connection-request-shell.md](../../RM/specs/260616-mobile-add-connection-request-shell.md).
