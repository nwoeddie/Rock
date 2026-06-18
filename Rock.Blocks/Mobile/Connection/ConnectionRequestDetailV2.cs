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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Common.Mobile.Blocks.Connection.ConnectionRequestDetailV2;
using Rock.Common.Mobile.ViewModel;
using Rock.Data;
using Rock.Enums.Connection;
using Rock.Mobile;
using Rock.Model;
using Rock.Security;
using Rock.Utility;
using Rock.Web.Cache;

using MobileConnectionState = Rock.Common.Mobile.Enums.ConnectionState;
using MobileDueStatus = Rock.Common.Mobile.Enums.DueStatus;
using MobileGroupMemberStatus = Rock.Common.Mobile.Blocks.Connection.ConnectionRequestDetailV2.GroupMemberStatus;
using MobileMeetsGroupRequirement = Rock.Common.Mobile.Blocks.Connection.ConnectionRequestDetailV2.MeetsGroupRequirement;

using ConnectionState = Rock.Model.ConnectionState;
using GroupMemberStatus = Rock.Model.GroupMemberStatus;
using MeetsGroupRequirement = Rock.Model.MeetsGroupRequirement;

namespace Rock.Blocks.Mobile.Connection
{
    /// <summary>
    /// Displays a single connection request for viewing and editing: the
    /// requester header, status / state, connector, campus, request source,
    /// placement group, comments, custom attributes, manual workflows,
    /// celebration, reminders and the activity count, with per-field edits,
    /// targeted status / state / connector quick-actions, and a Connect
    /// action. Adapted from the web Connections Hub docked request panel.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Connection Request Detail V2" )]
    [Category( "Mobile > Connection" )]
    [Description( "Displays a single connection request for viewing and editing." )]
    [IconCssClass( "ti ti-id" )]
    [SupportedSiteTypes( Model.SiteType.Mobile )]

    #region Block Attributes

    [LinkedPage( "Person Profile Page",
        Description = "Page to link to when the requester is tapped. The requester's PersonGuid is passed.",
        IsRequired = false,
        Key = AttributeKey.PersonProfilePage,
        Order = 0 )]

    [LinkedPage( "Group Detail Page",
        Description = "Page to link to when the placement group is tapped. The group's Guid is passed.",
        IsRequired = false,
        Key = AttributeKey.GroupDetailPage,
        Order = 1 )]

    [LinkedPage( "Workflow Page",
        Description = "Page to link to when a launched manual workflow needs an interactive entry form. The workflow Guid is passed.",
        IsRequired = false,
        Key = AttributeKey.WorkflowPage,
        Order = 2 )]

    [LinkedPage( "Reminder Page",
        Description = "Page that hosts the Reminder block, opened in a cover sheet by the Reminder quick-action. When empty, the Reminder card is hidden.",
        IsRequired = false,
        Key = AttributeKey.ReminderPage,
        Order = 3 )]

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "8B53B246-526F-4B3E-AF5B-4C36763E9DC9" )]
    [Rock.SystemGuid.BlockTypeGuid( "74DDC1A2-2025-4072-8F47-DF7A5A76CF83" )]
    public class ConnectionRequestDetailV2 : RockBlockType
    {
        #region Keys

        /// <summary>
        /// The block setting attribute keys for this block.
        /// </summary>
        private static class AttributeKey
        {
            public const string PersonProfilePage = "PersonProfilePage";
            public const string GroupDetailPage = "GroupDetailPage";
            public const string WorkflowPage = "WorkflowPage";
            public const string ReminderPage = "ReminderPage";
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override Version RequiredMobileVersion => new Version( 1, 20 );

        #endregion

        #region RockBlockType Implementation

        /// <inheritdoc/>
        public override object GetMobileConfigurationValues()
        {
            var campuses = CampusCache.All( false )
                .OrderBy( c => c.Order )
                .ThenBy( c => c.Name )
                .Select( c => new ListItemViewModel
                {
                    Value = c.Guid.ToString(),
                    Text = c.Name
                } )
                .ToList();

            var reminderPageGuid = GetAttributeValue( AttributeKey.ReminderPage ).AsGuidOrNull();

            return new Rock.Common.Mobile.Blocks.Connection.ConnectionRequestDetailV2.Configuration
            {
                Campuses = campuses,
                PersonProfilePageGuid = GetAttributeValue( AttributeKey.PersonProfilePage ).AsGuidOrNull(),
                GroupDetailPageGuid = GetAttributeValue( AttributeKey.GroupDetailPage ).AsGuidOrNull(),
                WorkflowPageGuid = GetAttributeValue( AttributeKey.WorkflowPage ).AsGuidOrNull(),
                ReminderPageGuid = reminderPageGuid,
                AreRemindersConfigured = reminderPageGuid.HasValue
            };
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Loads the full display detail for one connection request, plus the
        /// celebration text and the current person's edit permission.
        /// </summary>
        /// <param name="request">The request carrying the connection request IdKey.</param>
        /// <returns>The request display detail.</returns>
        [BlockAction]
        public BlockActionResult GetRequestDetail( GetRequestDetailRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            // Read auth is a single VIEW check on the request entity. Rock's
            // auth inheritance honors ConnectionType.EnableRequestSecurity for
            // free (request-level when on, opportunity/type inheritance when
            // off), so no explicit branching on the flag is needed here.
            if ( !connectionRequest.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson ) )
            {
                return ActionForbidden( "You are not authorized to view this connection request." );
            }

            if ( !connectionRequest.ConnectionOpportunity.IsActive || !connectionRequest.ConnectionOpportunity.ConnectionType.IsActive )
            {
                return ActionBadRequest( $"The specified {ConnectionOpportunity.FriendlyTypeName} is not active." );
            }

            connectionRequest.LoadAttributes( RockContext );

            return ActionOk( BuildDetailResponse( connectionRequest ) );
        }

        /// <summary>
        /// Lazily loads the edit option lists for a connection request, fetched
        /// when the first field editor opens.
        /// </summary>
        /// <param name="request">The request carrying the connection request IdKey.</param>
        /// <returns>The edit option lists and the type's edit flags.</returns>
        [BlockAction]
        public BlockActionResult GetEditOptions( GetEditOptionsRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !connectionRequest.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson ) )
            {
                return ActionForbidden( "You are not authorized to view this connection request." );
            }

            var connectionType = connectionRequest.ConnectionOpportunity.ConnectionType;
            var canEdit = CanEditConnectionRequest( connectionRequest, out _ );

            var options = new GetEditOptionsResponseBag
            {
                CanEdit = canEdit,
                Statuses = connectionType.ConnectionStatuses
                    .OrderBy( s => s.Order )
                    .ThenBy( s => s.Name )
                    .Select( s => new ConnectionStatusItemBag
                    {
                        Value = s.Guid,
                        Name = s.Name,
                        Color = s.HighlightColor,
                        Order = s.Order,
                        IsNoteRequiredOnCompletion = s.IsNoteRequiredOnCompletion,
                        IsDefault = s.IsDefault,
                        IsDisabled = false
                    } )
                    .ToList(),
                States = GetSelectableStates( connectionType ),
                Connectors = GetAvailableConnectors( connectionRequest ),
                PlacementGroups = GetRequestPlacementGroups( connectionRequest ),
                RequestSources = connectionType.ConnectionTypeSources
                    .OrderBy( s => s.Name )
                    .Select( s => new ListItemViewModel
                    {
                        Value = s.Guid.ToString(),
                        Text = s.Name
                    } )
                    .ToList(),
                AreCelebrationsEnabled = connectionType.EnabledFeatures.HasFlag( EnabledFeatureFlags.Celebration ),
                IsFutureFollowUpEnabled = connectionType.EnableFutureFollowup,
                RequiresPlacementGroupToConnect = connectionType.RequiresPlacementGroupToConnect,
                IsSequentialStatusMode = false
            };

            return ActionOk( options );
        }

        /// <summary>
        /// Saves the editable fields of a connection request that do not have a
        /// dedicated quick-action (campus, request source, comments, placement
        /// group, custom attributes). Carries the full field set; unedited
        /// fields pass through their current values.
        /// </summary>
        /// <param name="request">The save payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult SaveRequest( SaveConnectionRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !CanEditConnectionRequest( connectionRequest, out var error ) )
            {
                return error;
            }

            connectionRequest.LoadAttributes( RockContext );

            var connectionStatusService = new ConnectionStatusService( RockContext );
            var personAliasService = new PersonAliasService( RockContext );
            var connectionType = connectionRequest.ConnectionOpportunity.ConnectionType;
            var originalConnectorPersonAliasId = connectionRequest.ConnectorPersonAliasId;

            // Basic values.
            connectionRequest.Comments = request.Comments;

            // Status (must belong to the request's type). A note is required
            // when leaving a status that requires one on completion (web
            // parity with ChangeRequestStatus, replicated so a bulk save
            // cannot bypass the guard).
            var status = connectionStatusService.Queryable()
                .FirstOrDefault( s => s.ConnectionTypeId == connectionType.Id && s.Guid == request.StatusGuid );

            if ( status == null )
            {
                return ActionBadRequest( "Invalid connection status." );
            }

            var isStatusChanging = connectionRequest.ConnectionStatusId != status.Id;

            if ( isStatusChanging
                && connectionRequest.ConnectionStatus != null
                && connectionRequest.ConnectionStatus.IsNoteRequiredOnCompletion
                && request.StatusHistoryNote.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "A note is required." );
            }

            connectionRequest.ConnectionStatusId = status.Id;

            if ( isStatusChanging && request.StatusHistoryNote.IsNotNullOrWhiteSpace() )
            {
                connectionRequest.ConnectionStatusHistoryNote = request.StatusHistoryNote;
            }

            // State + follow-up date. Completion (Connected) is handled by the
            // UpdateState action, not here; the per-field State editor never
            // sends Connected through SaveRequest.
            connectionRequest.ConnectionState = ( ConnectionState ) ( int ) request.State;

            if ( connectionRequest.ConnectionState == ConnectionState.FutureFollowUp )
            {
                if ( !request.FollowUpDate.HasValue )
                {
                    return ActionBadRequest( "A Follow-Up Date is required." );
                }

                connectionRequest.FollowupDate = request.FollowUpDate.Value.DateTime;
            }
            else
            {
                connectionRequest.FollowupDate = null;
            }

            // Campus.
            if ( request.CampusGuid.HasValue )
            {
                var campusId = CampusCache.GetId( request.CampusGuid.Value );

                if ( !campusId.HasValue )
                {
                    return ActionBadRequest( "Invalid campus." );
                }

                connectionRequest.CampusId = campusId;
            }
            else
            {
                connectionRequest.CampusId = null;
            }

            // Request source (a ConnectionTypeSource row of the request's type).
            if ( request.RequestSourceGuid.HasValue )
            {
                var sourceId = connectionType.ConnectionTypeSources
                    .Where( s => s.Guid == request.RequestSourceGuid.Value )
                    .Select( s => ( int? ) s.Id )
                    .FirstOrDefault();

                if ( !sourceId.HasValue )
                {
                    return ActionBadRequest( "Invalid request source." );
                }

                connectionRequest.ConnectionTypeSourceId = sourceId;
            }
            else
            {
                connectionRequest.ConnectionTypeSourceId = null;
            }

            // Connector (validated against the available connectors).
            if ( request.ConnectorPersonGuid.HasValue )
            {
                var isValidConnector = GetAvailableConnectors( connectionRequest )
                    .Any( c => c.PersonGuid == request.ConnectorPersonGuid.Value );

                var connectorPersonAliasId = personAliasService.GetPrimaryAliasId( request.ConnectorPersonGuid.Value );

                if ( !isValidConnector || !connectorPersonAliasId.HasValue )
                {
                    return ActionBadRequest( "Invalid connector." );
                }

                connectionRequest.ConnectorPersonAliasId = connectorPersonAliasId;
            }
            else
            {
                connectionRequest.ConnectorPersonAliasId = null;
            }

            // Placement group (group + role + status must all be present or all
            // absent).
            var setPlacementError = ApplyPlacementGroup( connectionRequest, request );

            if ( setPlacementError != null )
            {
                return setPlacementError;
            }

            // Custom attribute values.
            if ( request.AttributeValues != null )
            {
                connectionRequest.SetPublicAttributeValues( request.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: true );
            }

            // Record the connector-assigned activity when the connector changed.
            if ( originalConnectorPersonAliasId != connectionRequest.ConnectorPersonAliasId )
            {
                var activity = CreateAssignedActivity( connectionRequest, RockContext );

                if ( activity != null )
                {
                    new ConnectionRequestActivityService( RockContext ).Add( activity );
                }
            }

            RockContext.WrapTransaction( () =>
            {
                RockContext.SaveChanges();
                connectionRequest.SaveAttributeValues( RockContext );
            } );

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        /// <summary>
        /// Targeted status change (the status pill quick-action). Validates the
        /// status belongs to the request's type and enforces note-required-on-
        /// completion. Ports the web ChangeRequestStatus.
        /// </summary>
        /// <param name="request">The status-change payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult ChangeStatus( ChangeStatusRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !CanEditConnectionRequest( connectionRequest, out var error ) )
            {
                return error;
            }

            var newStatus = new ConnectionStatusService( RockContext ).Queryable()
                .FirstOrDefault( s => s.ConnectionTypeId == connectionRequest.ConnectionOpportunity.ConnectionTypeId
                    && s.Guid == request.ConnectionStatusGuid );

            if ( newStatus == null )
            {
                return ActionBadRequest( "Invalid connection status." );
            }

            // The web enforces the note requirement against the CURRENT status
            // before the change is applied.
            if ( connectionRequest.ConnectionStatus != null
                && connectionRequest.ConnectionStatus.IsNoteRequiredOnCompletion
                && request.Note.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "A note is required." );
            }

            connectionRequest.ConnectionStatusId = newStatus.Id;
            connectionRequest.ConnectionStatusHistoryNote = request.Note;

            RockContext.SaveChanges();

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        /// <summary>
        /// Targeted state update (the State row quick-action and the footer
        /// Connect button). Requires a follow-up date for Future Follow Up and
        /// runs the completion handling when transitioning to Connected. Ports
        /// the web UpdateRequestStates.
        /// </summary>
        /// <param name="request">The state-update payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult UpdateState( UpdateStateRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !CanEditConnectionRequest( connectionRequest, out var error ) )
            {
                return error;
            }

            var newState = ( ConnectionState ) ( int ) request.ConnectionState;

            if ( newState == ConnectionState.FutureFollowUp && !request.FollowUpDate.HasValue )
            {
                return ActionBadRequest( "A Follow-Up Date is required." );
            }

            // Completing a request is a state change to Connected, with the
            // placement-group and manual-requirement handling.
            if ( newState == ConnectionState.Connected )
            {
                var connectionType = connectionRequest.ConnectionOpportunity.ConnectionType;

                if ( connectionType.RequiresPlacementGroupToConnect && !connectionRequest.AssignedGroupId.HasValue )
                {
                    return ActionBadRequest( "This connection type requires a placement group before the request can be connected." );
                }

                var manualRequirementsMet = ( request.GroupMemberRequirements ?? new List<GroupMemberRequirementBag>() )
                    .ToDictionary(
                        r => r.GroupRequirementGuid,
                        r => r.GroupMemberRequirementState == MobileMeetsGroupRequirement.Meets
                            || r.GroupMemberRequirementState == MobileMeetsGroupRequirement.MeetsWithWarning );

                if ( !TryMarkRequestConnected( connectionRequest, manualRequirementsMet, RequestContext.CurrentPerson, RockContext, out var connectError ) )
                {
                    return ActionBadRequest( connectError );
                }

                return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
            }

            connectionRequest.ConnectionState = newState;

            if ( newState == ConnectionState.FutureFollowUp )
            {
                connectionRequest.FollowupDate = request.FollowUpDate.Value.DateTime;
            }
            else
            {
                connectionRequest.FollowupDate = null;
            }

            RockContext.SaveChanges();

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        /// <summary>
        /// Targeted connector reassignment (the Connector row quick-action).
        /// </summary>
        /// <param name="request">The reassign payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult ReassignConnector( ReassignConnectorRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !CanEditConnectionRequest( connectionRequest, out var error ) )
            {
                return error;
            }

            var originalConnectorPersonAliasId = connectionRequest.ConnectorPersonAliasId;

            if ( request.ConnectorPersonGuid.HasValue )
            {
                var isValidConnector = GetAvailableConnectors( connectionRequest )
                    .Any( c => c.PersonGuid == request.ConnectorPersonGuid.Value );

                var connectorPersonAliasId = new PersonAliasService( RockContext ).GetPrimaryAliasId( request.ConnectorPersonGuid.Value );

                if ( !isValidConnector || !connectorPersonAliasId.HasValue )
                {
                    return ActionBadRequest( "Invalid connector." );
                }

                connectionRequest.ConnectorPersonAliasId = connectorPersonAliasId;
            }
            else
            {
                connectionRequest.ConnectorPersonAliasId = null;
            }

            if ( originalConnectorPersonAliasId != connectionRequest.ConnectorPersonAliasId )
            {
                var activity = CreateAssignedActivity( connectionRequest, RockContext );

                if ( activity != null )
                {
                    new ConnectionRequestActivityService( RockContext ).Add( activity );
                }
            }

            RockContext.SaveChanges();

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        /// <summary>
        /// Launches a manual connection workflow for the request and reports
        /// whether it needs an interactive entry form.
        /// </summary>
        /// <param name="request">The launch payload.</param>
        /// <returns>The launch result.</returns>
        [BlockAction]
        public BlockActionResult LaunchWorkflow( LaunchWorkflowRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();
            var connectionWorkflow = new ConnectionWorkflowService( RockContext ).Get( request.ConnectionWorkflowGuid );

            if ( connectionRequest == null || connectionWorkflow == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !connectionRequest.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson ) )
            {
                return ActionForbidden( "You are not authorized to view this connection request." );
            }

            // Verify the workflow is one the person may launch for this request.
            var workflows = GetConnectionOpportunityManualWorkflowTypes( connectionRequest.ConnectionOpportunity, RequestContext.CurrentPerson );

            if ( !workflows.Any( w => w.Guid == connectionWorkflow.Guid ) )
            {
                return ActionBadRequest( "That workflow is not available for this request." );
            }

            var result = LaunchConnectionRequestWorkflow( connectionRequest, connectionWorkflow, RequestContext.CurrentPerson, RockContext );

            return ActionOk( result );
        }

        /// <summary>
        /// Creates, updates, or clears the celebration note for the request.
        /// Ports the web UpsertCelebrationText.
        /// </summary>
        /// <param name="request">The celebration payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult UpsertCelebration( UpsertCelebrationRequestBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !CanEditConnectionRequest( connectionRequest, out var error ) )
            {
                return error;
            }

            var celebrationNoteType = NoteTypeCache.Get( Rock.SystemGuid.NoteType.CELEBRATION_NOTE.AsGuid() );

            if ( celebrationNoteType == null )
            {
                return ActionBadRequest( "The celebration note type is not configured." );
            }

            var noteService = new NoteService( RockContext );
            var existingNote = noteService.Queryable()
                .FirstOrDefault( n => n.NoteTypeId == celebrationNoteType.Id && n.EntityId == connectionRequest.Id );

            var text = request.CelebrationText?.Trim();

            if ( text.IsNullOrWhiteSpace() )
            {
                // Clearing removes the note so the badge disappears in the list
                // blocks.
                if ( existingNote != null )
                {
                    noteService.Delete( existingNote );
                    RockContext.SaveChanges();
                }
            }
            else if ( existingNote != null )
            {
                if ( existingNote.Text != text )
                {
                    existingNote.Text = text;
                    RockContext.SaveChanges();
                }
            }
            else
            {
                noteService.Add( new Note
                {
                    NoteTypeId = celebrationNoteType.Id,
                    EntityId = connectionRequest.Id,
                    Text = text,
                    CreatedByPersonAliasId = RequestContext.CurrentPerson?.PrimaryAliasId
                } );
                RockContext.SaveChanges();
            }

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        /// <summary>
        /// Adds or edits a connection request note (a CONNECTION_REQUEST_NOTE).
        /// Ports the web SaveNote. Requires VIEW on the request and EDIT on the
        /// note.
        /// </summary>
        /// <param name="request">The note payload.</param>
        /// <returns>The refreshed request detail.</returns>
        [BlockAction]
        public BlockActionResult SaveNote( ConnectionRequestNoteBag request )
        {
            if ( request == null || request.ConnectionRequestIdKey.IsNullOrWhiteSpace() )
            {
                return ActionBadRequest( "Missing request options." );
            }

            var connectionRequest = GetRequestQueryable( request.ConnectionRequestIdKey ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return ActionBadRequest( $"{ConnectionRequest.FriendlyTypeName} not found." );
            }

            if ( !connectionRequest.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson ) )
            {
                return ActionForbidden( "You are not authorized to view this connection request." );
            }

            var noteType = NoteTypeCache.Get( Rock.SystemGuid.NoteType.CONNECTION_REQUEST_NOTE.AsGuid() );

            if ( noteType == null )
            {
                return ActionBadRequest( "The connection request note type is not configured." );
            }

            var noteService = new NoteService( RockContext );
            Note note;

            if ( request.NoteIdKey.IsNullOrWhiteSpace() )
            {
                note = new Note
                {
                    NoteTypeId = noteType.Id,
                    EntityId = connectionRequest.Id,
                    CreatedByPersonAliasId = RequestContext.CurrentPerson?.PrimaryAliasId
                };

                if ( !note.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
                {
                    return ActionForbidden( "You are not authorized to add a note." );
                }

                noteService.Add( note );
            }
            else
            {
                note = noteService.Get( request.NoteIdKey, !PageCache.Layout.Site.DisablePredictableIds );

                if ( note == null )
                {
                    return ActionBadRequest( $"{Note.FriendlyTypeName} not found." );
                }

                if ( !note.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
                {
                    return ActionForbidden( "You are not authorized to edit this note." );
                }
            }

            note.Text = request.NoteText;

            RockContext.SaveChanges();

            return ActionOk( BuildRefreshedResponse( connectionRequest.Id ) );
        }

        #endregion

        #region Private Methods - Resolution and Auth

        /// <summary>
        /// Gets the queryable for a single connection request by IdKey (also
        /// accepts a Guid or integer Id), with the navigation properties the
        /// load and edit paths need.
        /// </summary>
        /// <param name="idKey">The connection request IdKey.</param>
        /// <returns>A queryable that resolves to the single request.</returns>
        private IQueryable<ConnectionRequest> GetRequestQueryable( string idKey )
        {
            return new ConnectionRequestService( RockContext )
                .GetQueryableByKey( idKey, !PageCache.Layout.Site.DisablePredictableIds )
                .Include( r => r.PersonAlias.Person )
                .Include( r => r.ConnectorPersonAlias.Person )
                .Include( r => r.ConnectionOpportunity.ConnectionType.ConnectionStatuses )
                .Include( r => r.ConnectionOpportunity.ConnectionType.ConnectionTypeSources )
                .Include( r => r.ConnectionStatus )
                .Include( r => r.Campus )
                .Include( r => r.ConnectionTypeSource )
                .Include( r => r.AssignedGroup.GroupType );
        }

        /// <summary>
        /// Determines whether the current person may edit the request, porting
        /// the web Connections Hub <c>CanEditSpecifiedConnectionRequest</c>: an
        /// EnableRequestSecurity-aware EDIT check plus the connector and
        /// connector-group fallback grant (with the campus rule). Sets
        /// <paramref name="error"/> to an <see cref="ActionForbidden"/> result
        /// when the person may not edit.
        /// </summary>
        /// <param name="connectionRequest">The request being authorized.</param>
        /// <param name="error">On return, the forbidden result when not authorized.</param>
        /// <returns><c>true</c> when the current person may edit the request.</returns>
        private bool CanEditConnectionRequest( ConnectionRequest connectionRequest, out BlockActionResult error )
        {
            error = null;

            var currentPerson = RequestContext.CurrentPerson;
            var opportunity = connectionRequest.ConnectionOpportunity;
            var enableRequestSecurity = opportunity?.ConnectionType?.EnableRequestSecurity == true;

            bool canEdit;

            if ( enableRequestSecurity )
            {
                canEdit = connectionRequest.IsAuthorized( Authorization.EDIT, currentPerson );
            }
            else
            {
                canEdit = opportunity.IsAuthorized( Authorization.EDIT, currentPerson );

                // Fall back to the connector / connector-group grant.
                if ( !canEdit && currentPerson != null )
                {
                    if ( connectionRequest.ConnectorPersonAlias != null
                        && connectionRequest.ConnectorPersonAlias.PersonId == currentPerson.Id )
                    {
                        canEdit = true;
                    }
                    else
                    {
                        var connectorGroups = new ConnectionOpportunityConnectorGroupService( RockContext ).Queryable()
                            .AsNoTracking()
                            .Where( g => g.ConnectionOpportunityId == connectionRequest.ConnectionOpportunityId
                                && g.ConnectorGroup != null
                                && g.ConnectorGroup.IsActive
                                && !g.ConnectorGroup.IsArchived
                                && g.ConnectorGroup.Members.Any( m => m.PersonId == currentPerson.Id
                                    && m.GroupMemberStatus == GroupMemberStatus.Active
                                    && !m.IsArchived ) )
                            .Select( g => new { g.CampusId } )
                            .ToList();

                        if ( connectorGroups.Any() )
                        {
                            var activeCampusCount = CampusCache.All().Count( c => c.IsActive ?? true );

                            canEdit = activeCampusCount == 1
                                || connectorGroups.Any( g => !g.CampusId.HasValue )
                                || !connectionRequest.CampusId.HasValue
                                || connectorGroups.Any( g => g.CampusId == connectionRequest.CampusId.Value );
                        }
                    }
                }
            }

            if ( !canEdit )
            {
                error = ActionForbidden( $"You are not authorized to edit this {ConnectionRequest.FriendlyTypeName}." );
            }

            return canEdit;
        }

        #endregion

        #region Private Methods - Response Building

        /// <summary>
        /// Re-resolves a request by id (with fresh navigation properties) and
        /// builds its display response. Used after a mutation so the returned
        /// detail reflects the saved values.
        /// </summary>
        /// <param name="connectionRequestId">The request identifier.</param>
        /// <returns>The refreshed display response.</returns>
        private GetRequestDetailResponseBag BuildRefreshedResponse( int connectionRequestId )
        {
            var connectionRequest = GetRequestQueryable( connectionRequestId.ToString() ).FirstOrDefault();

            if ( connectionRequest == null )
            {
                return null;
            }

            connectionRequest.LoadAttributes( RockContext );

            return BuildDetailResponse( connectionRequest );
        }

        /// <summary>
        /// Builds the full display response for a loaded request (attributes
        /// must already be loaded).
        /// </summary>
        /// <param name="connectionRequest">The loaded request.</param>
        /// <returns>The display response.</returns>
        private GetRequestDetailResponseBag BuildDetailResponse( ConnectionRequest connectionRequest )
        {
            var currentPerson = RequestContext.CurrentPerson;
            var requester = connectionRequest.PersonAlias?.Person;
            var connectionType = connectionRequest.ConnectionOpportunity.ConnectionType;
            var canEdit = CanEditConnectionRequest( connectionRequest, out _ );

            // Resolve the assigned role's Guid (when placed) so the per-field
            // SaveRequest can pass through the current placement.
            Guid? assignedRoleGuid = null;

            if ( connectionRequest.AssignedGroupMemberRoleId.HasValue )
            {
                assignedRoleGuid = new GroupTypeRoleService( RockContext ).Queryable()
                    .Where( r => r.Id == connectionRequest.AssignedGroupMemberRoleId.Value )
                    .Select( r => ( Guid? ) r.Guid )
                    .FirstOrDefault();
            }

            var bag = new ConnectionRequestDetailBag
            {
                ConnectionRequestIdKey = connectionRequest.IdKey,

                RequesterFullName = requester?.FullName,
                RequesterPhotoUrl = requester != null && requester.PhotoId.HasValue
                    ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( requester.PhotoId.Value ) )
                    : null,
                RequesterPersonGuid = requester?.Guid ?? Guid.Empty,
                RequesterPersonAliasGuid = connectionRequest.PersonAlias?.Guid ?? Guid.Empty,
                PersonConnectionStatusName = requester?.ConnectionStatusValue?.Value,
                Gender = requester != null && requester.Gender != Gender.Unknown ? requester.Gender.ToString() : null,
                PhoneNumber = requester?.GetPhoneNumber( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid() )?.NumberFormatted,
                Email = requester?.Email,

                ConnectionOpportunityName = connectionRequest.ConnectionOpportunity.Name,
                ConnectionOpportunityIconCssClass = connectionRequest.ConnectionOpportunity.IconCssClass,
                ConnectionTypeName = connectionType.Name,

                ConnectionState = ( MobileConnectionState ) ( int ) connectionRequest.ConnectionState,
                FollowUpDate = connectionRequest.FollowupDate?.ToRockDateTimeOffset(),
                StatusName = connectionRequest.ConnectionStatus?.Name,
                StatusColor = connectionRequest.ConnectionStatus?.HighlightColor,
                StatusGuid = connectionRequest.ConnectionStatus?.Guid ?? Guid.Empty,
                CampusName = connectionRequest.Campus?.Name,
                CampusGuid = connectionRequest.Campus?.Guid,
                ConnectorName = connectionRequest.ConnectorPersonAlias?.Person?.FullName,
                ConnectorPersonGuid = connectionRequest.ConnectorPersonAlias?.Person?.Guid,
                RequestSourceName = connectionRequest.ConnectionTypeSource?.Name,
                RequestSourceGuid = connectionRequest.ConnectionTypeSource?.Guid,
                DueStatus = GetRequestDueStatus( connectionRequest ),
                CreatedDateTime = connectionRequest.CreatedDateTime?.ToRockDateTimeOffset(),
                DueDate = connectionRequest.DueDate?.ToRockDateTimeOffset(),
                Comments = connectionRequest.Comments?.ConvertMarkdownToHtml().StripHtml(),

                PlacementGroup = BuildPlacementGroupSummary( connectionRequest ),
                PlacementGroupGuid = connectionRequest.AssignedGroup?.Guid,
                PlacementGroupRoleGuid = assignedRoleGuid,
                PlacementGroupMemberStatus = connectionRequest.AssignedGroupMemberStatus.HasValue
                    ? ( MobileGroupMemberStatus ) ( int ) connectionRequest.AssignedGroupMemberStatus.Value
                    : ( MobileGroupMemberStatus? ) null,
                PlacementGroupRequirements = canEdit
                    ? GetPlacementGroupRequirements( connectionRequest, RockContext )
                    : new List<GroupMemberRequirementBag>(),

                Attributes = GetClientAttributeValuesForView( connectionRequest ),
                EditableAttributeKeys = canEdit
                    ? connectionRequest.GetPublicAttributesForEdit( RequestContext.CurrentPerson, enforceSecurity: true ).Keys.ToList()
                    : new List<string>(),
                ManualWorkflows = GetConnectionOpportunityManualWorkflowTypes( connectionRequest.ConnectionOpportunity, currentPerson )
                    .Where( w => w.ManualTriggerFilterConnectionStatusId == null
                        || w.ManualTriggerFilterConnectionStatusId == connectionRequest.ConnectionStatusId )
                    .Select( w => new ManualWorkflowItemBag
                    {
                        Value = w.Guid,
                        Name = w.WorkflowType.Name,
                        IconCssClass = w.WorkflowType.IconCssClass
                    } )
                    .ToList(),

                ActivityCount = new ConnectionRequestActivityService( RockContext ).Queryable()
                    .Count( a => a.ConnectionRequestId == connectionRequest.Id ),
                ReminderCount = GetReminderCount( connectionRequest ),
                AreRemindersEnabled = connectionType.EnabledFeatures.HasFlag( EnabledFeatureFlags.Reminder ),
                CanEditConnectionRequestNote = CanEditConnectionRequestNote( connectionRequest )
            };

            return new GetRequestDetailResponseBag
            {
                Request = bag,
                CelebrationText = GetCelebrationText( connectionRequest.Id ),
                CanEdit = canEdit
            };
        }

        /// <summary>
        /// Builds the assigned placement group summary, or <c>null</c> when no
        /// group is assigned.
        /// </summary>
        /// <param name="connectionRequest">The request.</param>
        /// <returns>The placement group summary, or <c>null</c>.</returns>
        private PlacementGroupSummaryBag BuildPlacementGroupSummary( ConnectionRequest connectionRequest )
        {
            if ( connectionRequest.AssignedGroup == null )
            {
                return null;
            }

            string roleName = null;

            if ( connectionRequest.AssignedGroupMemberRoleId.HasValue )
            {
                roleName = new GroupTypeRoleService( RockContext ).Queryable()
                    .Where( r => r.Id == connectionRequest.AssignedGroupMemberRoleId.Value )
                    .Select( r => r.Name )
                    .FirstOrDefault();
            }

            return new PlacementGroupSummaryBag
            {
                GroupGuid = connectionRequest.AssignedGroup.Guid,
                Name = connectionRequest.AssignedGroup.Name,
                IconCssClass = connectionRequest.AssignedGroup.GroupType?.IconCssClass,
                RoleName = roleName,
                StatusName = connectionRequest.AssignedGroupMemberStatus?.ToString()
            };
        }

        /// <summary>
        /// Gets the current person's count of incomplete, past-due reminders on
        /// the request entity.
        /// </summary>
        /// <param name="connectionRequest">The request.</param>
        /// <returns>The reminder count.</returns>
        private int GetReminderCount( ConnectionRequest connectionRequest )
        {
            var currentPerson = RequestContext.CurrentPerson;

            if ( currentPerson == null )
            {
                return 0;
            }

            var now = RockDateTime.Now;

            // Web parity: reminders are tracked against the requester's
            // PersonAlias, so the count matches reminders whose EntityId is the
            // request's PersonAliasId (ConnectionsHub.GetConnectionRequestDetails).
            return new ReminderService( RockContext ).Queryable()
                .Count( r => !r.IsComplete
                    && r.ReminderDate < now
                    && r.PersonAlias.PersonId == currentPerson.Id
                    && r.EntityId == connectionRequest.PersonAliasId );
        }

        /// <summary>
        /// Determines whether the current person may add a connection request
        /// note, mirroring the web's temp-note EDIT check.
        /// </summary>
        /// <param name="connectionRequest">The request.</param>
        /// <returns><c>true</c> when a note may be added.</returns>
        private bool CanEditConnectionRequestNote( ConnectionRequest connectionRequest )
        {
            var noteType = NoteTypeCache.Get( Rock.SystemGuid.NoteType.CONNECTION_REQUEST_NOTE.AsGuid() );

            if ( noteType == null )
            {
                return false;
            }

            var tempNote = new Note
            {
                NoteTypeId = noteType.Id,
                EntityId = connectionRequest.Id
            };

            return tempNote.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );
        }

        /// <summary>
        /// Gets the non-empty celebration note text for the request, or
        /// <c>null</c> when none exists.
        /// </summary>
        /// <param name="connectionRequestId">The request identifier.</param>
        /// <returns>The celebration text, or <c>null</c>.</returns>
        private string GetCelebrationText( int connectionRequestId )
        {
            var celebrationNoteType = NoteTypeCache.Get( Rock.SystemGuid.NoteType.CELEBRATION_NOTE.AsGuid() );

            if ( celebrationNoteType == null )
            {
                return null;
            }

            return new NoteService( RockContext ).Queryable()
                .AsNoTracking()
                .Where( n => n.NoteTypeId == celebrationNoteType.Id
                    && n.EntityId == connectionRequestId
                    && n.Text != null
                    && n.Text != "" )
                .Select( n => n.Text )
                .FirstOrDefault();
        }

        /// <summary>
        /// Maps the request's custom attributes (for view) into the mobile
        /// editable attribute view model.
        /// </summary>
        /// <param name="entity">The attribute-bearing entity.</param>
        /// <returns>The ordered attribute values for display.</returns>
        private List<ClientEditableAttributeValueViewModel> GetClientAttributeValuesForView( Rock.Attribute.IHasAttributes entity )
        {
            var attributes = entity.GetPublicAttributesForView( RequestContext.CurrentPerson )
                .ToDictionary( kvp => kvp.Key, kvp => new ClientEditableAttributeValueViewModel
                {
                    AttributeGuid = kvp.Value.AttributeGuid,
                    Categories = kvp.Value.Categories?
                        .Select( c => new ClientAttributeValueCategoryViewModel
                        {
                            Guid = c.Guid,
                            Name = c.Name,
                            Order = c.Order
                        } )
                        .ToList(),
                    ConfigurationValues = kvp.Value.ConfigurationValues,
                    Description = kvp.Value.Description,
                    FieldTypeGuid = kvp.Value.FieldTypeGuid,
                    IsRequired = kvp.Value.IsRequired,
                    Key = kvp.Value.Key,
                    Name = kvp.Value.Name,
                    Order = kvp.Value.Order,
                    Value = "",
                    TextValue = entity.GetAttributeTextValue( kvp.Value.Key )
                } );

            // Seed Value from the public EDIT values, not the view values. The
            // shell editor feeds Value straight into the mobile FieldType's
            // SetEditValue, which expects the edit-value shape (File / Image /
            // DefinedValue carry a {value,text} JSON for edit but a plain
            // friendly string for view, so the view value would fail to parse).
            // Read-only display uses TextValue, so non-editable attributes do
            // not need a Value. Mirrors the v1 block's GetPublicEditableAttributeValues.
            foreach ( var kvp in entity.GetPublicAttributeValuesForEdit( RequestContext.CurrentPerson, enforceSecurity: true ) )
            {
                if ( attributes.TryGetValue( kvp.Key, out var attribute ) )
                {
                    attribute.Value = kvp.Value;
                }
            }

            return attributes.Values.OrderBy( a => a.Order ).ToList();
        }

        /// <summary>
        /// Computes the request's due status using the same model as the web
        /// Connections Hub (an Inactive request, or one with no due date, is
        /// always On Track).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The mobile due status.</returns>
        private static MobileDueStatus GetRequestDueStatus( ConnectionRequest request )
        {
            var now = RockDateTime.Now.Date;
            var dueDate = request.DueDate;
            var connectionState = request.ConnectionState;
            var completedDateTime = request.ConnectedDateTime;
            var dueSoonDate = request.DueSoonDate;

            if ( !dueDate.HasValue || connectionState == ConnectionState.Inactive )
            {
                return MobileDueStatus.DueLater;
            }

            var due = dueDate.Value.Date;

            if ( connectionState == ConnectionState.Connected )
            {
                if ( !completedDateTime.HasValue )
                {
                    return MobileDueStatus.DueLater;
                }

                return completedDateTime.Value.Date > due ? MobileDueStatus.Overdue : MobileDueStatus.DueLater;
            }

            if ( now > due )
            {
                return MobileDueStatus.Overdue;
            }

            if ( dueSoonDate.HasValue && now >= dueSoonDate.Value.Date )
            {
                return MobileDueStatus.DueSoon;
            }

            return MobileDueStatus.DueLater;
        }

        #endregion

        #region Private Methods - Edit Options

        /// <summary>
        /// Gets the selectable connection states for the type, honoring whether
        /// Future Follow Up is enabled. Connected is never offered (completion
        /// is the Connect action). Each item's value is the integer state.
        /// </summary>
        /// <param name="connectionType">The connection type.</param>
        /// <returns>The selectable states as list items.</returns>
        private static List<ListItemViewModel> GetSelectableStates( ConnectionType connectionType )
        {
            var states = new List<ListItemViewModel>
            {
                new ListItemViewModel { Value = ( ( int ) ConnectionState.Active ).ToString(), Text = "Active" },
                new ListItemViewModel { Value = ( ( int ) ConnectionState.Inactive ).ToString(), Text = "Inactive" }
            };

            if ( connectionType.EnableFutureFollowup )
            {
                states.Add( new ListItemViewModel { Value = ( ( int ) ConnectionState.FutureFollowUp ).ToString(), Text = "Future Follow Up" } );
            }

            return states;
        }

        /// <summary>
        /// Gets the connectors available for assignment to the request, mirrors
        /// the existing block (all opportunity connectors plus the current
        /// connector and the current person).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The connectors available for selection.</returns>
        private List<ConnectorItemBag> GetAvailableConnectors( ConnectionRequest request )
        {
            var connectorGroupService = new ConnectionOpportunityConnectorGroupService( RockContext );
            var personAliasService = new PersonAliasService( RockContext );
            var opportunityId = request.ConnectionOpportunityId;

            var connectorList = connectorGroupService.Queryable()
                .Where( a => a.ConnectionOpportunityId == opportunityId )
                .SelectMany( g => g.ConnectorGroup.Members )
                .Where( m => m.GroupMemberStatus == GroupMemberStatus.Active )
                .Select( m => new
                {
                    m.Person.Guid,
                    m.Person.NickName,
                    m.Person.LastName,
                    m.Person.PhotoId,
                    CampusGuid = ( Guid? ) m.Group.Campus.Guid
                } )
                .ToList();

            var additionalPersonAliasIds = new List<int>();

            if ( request.ConnectorPersonAliasId.HasValue )
            {
                additionalPersonAliasIds.Add( request.ConnectorPersonAliasId.Value );
            }

            if ( RequestContext.CurrentPerson?.PrimaryAliasId != null )
            {
                additionalPersonAliasIds.Add( RequestContext.CurrentPerson.PrimaryAliasId.Value );
            }

            if ( additionalPersonAliasIds.Any() )
            {
                var additionalPeople = personAliasService.Queryable()
                    .Where( pa => additionalPersonAliasIds.Contains( pa.Id ) )
                    .Select( pa => new
                    {
                        pa.Person.Guid,
                        pa.Person.NickName,
                        pa.Person.LastName,
                        pa.Person.PhotoId,
                        CampusGuid = ( Guid? ) null
                    } )
                    .ToList();

                connectorList.AddRange( additionalPeople );
            }

            return connectorList
                .GroupBy( c => new { c.Guid, c.CampusGuid } )
                .Select( g => g.First() )
                .OrderBy( c => c.LastName )
                .ThenBy( c => c.NickName )
                .Select( c => new ConnectorItemBag
                {
                    PersonGuid = c.Guid,
                    FullName = $"{c.NickName} {c.LastName}".Trim(),
                    PhotoUrl = c.PhotoId.HasValue
                        ? MobileHelper.BuildPublicApplicationRootUrl( FileUrlHelper.GetImageUrl( c.PhotoId.Value ) )
                        : null,
                    CampusGuid = c.CampusGuid
                } )
                .ToList();
        }

        /// <summary>
        /// Gets the placement groups available for the request's opportunity,
        /// with their roles and per-role statuses. Adapted from the existing
        /// block's GetRequestPlacementGroups.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The available placement groups.</returns>
        private List<PlacementGroupItemBag> GetRequestPlacementGroups( ConnectionRequest request )
        {
            var connectionOpportunityGroupConfigQuery = new ConnectionOpportunityGroupConfigService( RockContext ).Queryable()
                .Where( c => c.ConnectionOpportunityId == request.ConnectionOpportunityId );

            var availablePlacementGroups = GetAvailablePlacementGroupsQuery( request.ConnectionOpportunityId, request.AssignedGroupId, RockContext )
                .Select( g => new
                {
                    g.Id,
                    g.Guid,
                    g.Name,
                    CampusGuid = ( Guid? ) g.Campus.Guid,
                    CampusName = g.Campus.Name,
                    Configs = connectionOpportunityGroupConfigQuery
                        .Where( c => c.GroupTypeId == g.GroupTypeId )
                        .Select( c => new
                        {
                            c.GroupMemberRole.Guid,
                            c.GroupMemberRole.Name,
                            Status = c.GroupMemberStatus
                        } )
                        .ToList()
                } )
                .ToList();

            return availablePlacementGroups
                .Select( g => new PlacementGroupItemBag
                {
                    Value = g.Guid,
                    Name = $"{g.Name} ({( g.CampusName.IsNotNullOrWhiteSpace() ? g.CampusName : "No Campus" )})",
                    CampusGuid = g.CampusGuid,
                    Roles = g.Configs
                        .GroupBy( c => new { c.Guid, c.Name } )
                        .Select( cgrp => new PlacementGroupRoleItemBag
                        {
                            Value = cgrp.Key.Guid,
                            Name = cgrp.Key.Name,
                            Statuses = cgrp.Select( s => ( MobileGroupMemberStatus ) ( int ) s.Status ).ToList()
                        } )
                        .ToList()
                } )
                .ToList();
        }

        /// <summary>
        /// Gets a query of all groups that are possible placement groups for
        /// the request's opportunity. Adapted from the existing block.
        /// </summary>
        /// <param name="connectionOpportunityId">The opportunity identifier.</param>
        /// <param name="additionalGroupId">An optional currently-assigned group to include.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <returns>A queryable of the placement groups.</returns>
        private static IQueryable<Rock.Model.Group> GetAvailablePlacementGroupsQuery( int connectionOpportunityId, int? additionalGroupId, RockContext rockContext )
        {
            var opportunityService = new ConnectionOpportunityService( rockContext );
            var groupService = new GroupService( rockContext );

            var specificConfigQuery = opportunityService.Queryable()
                .AsNoTracking()
                .Where( o => o.Id == connectionOpportunityId )
                .SelectMany( o => o.ConnectionOpportunityGroups )
                .Select( cog => cog.Group );

            var allGroupsOfTypeQuery = opportunityService.Queryable()
                .AsNoTracking()
                .Where( o => o.Id == connectionOpportunityId )
                .SelectMany( o => o.ConnectionOpportunityGroupConfigs )
                .Where( gc => gc.UseAllGroupsOfType )
                .SelectMany( gc => gc.GroupType.Groups );

            var allGroupsQuery = specificConfigQuery.Union( allGroupsOfTypeQuery );

            if ( additionalGroupId.HasValue )
            {
                var additionalGroupQuery = groupService.Queryable()
                    .AsNoTracking()
                    .Where( g => g.Id == additionalGroupId );

                allGroupsQuery = allGroupsQuery.Union( additionalGroupQuery );
            }

            return allGroupsQuery
                .Where( g => g.IsActive && !g.IsArchived )
                .Distinct();
        }

        #endregion

        #region Private Methods - Placement, Workflows, Requirements

        /// <summary>
        /// Applies the placement group fields (group + role + status + member
        /// attribute values) to the request, or clears them when no group is
        /// provided. Returns an error result when the data is invalid.
        /// Adapted from the existing block's UpdateRequest.
        /// </summary>
        /// <param name="request">The request being saved.</param>
        /// <param name="saveBag">The save payload.</param>
        /// <returns>An error result, or <c>null</c> on success.</returns>
        private BlockActionResult ApplyPlacementGroup( ConnectionRequest request, SaveConnectionRequestBag saveBag )
        {
            var hasAnyPlacementValue = saveBag.PlacementGroupGuid.HasValue
                || saveBag.PlacementGroupRoleGuid.HasValue
                || saveBag.PlacementGroupMemberStatus.HasValue;

            if ( !hasAnyPlacementValue )
            {
                request.AssignedGroupId = null;
                request.AssignedGroupMemberRoleId = null;
                request.AssignedGroupMemberStatus = null;
                request.AssignedGroupMemberAttributeValues = null;

                return null;
            }

            // All three are required together.
            if ( !saveBag.PlacementGroupGuid.HasValue || !saveBag.PlacementGroupRoleGuid.HasValue || !saveBag.PlacementGroupMemberStatus.HasValue )
            {
                return ActionBadRequest( "A placement group requires a group, a role and a status." );
            }

            var validPlacementGroups = GetRequestPlacementGroups( request );
            var placementGroup = validPlacementGroups.FirstOrDefault( g => g.Value == saveBag.PlacementGroupGuid.Value );
            var placementRole = placementGroup?.Roles.FirstOrDefault( r => r.Value == saveBag.PlacementGroupRoleGuid.Value );

            if ( placementGroup == null || placementRole == null || !placementRole.Statuses.Contains( saveBag.PlacementGroupMemberStatus.Value ) )
            {
                return ActionBadRequest( "Invalid placement group selection." );
            }

            var groupId = new GroupService( RockContext ).GetId( placementGroup.Value );
            var roleId = new GroupTypeRoleService( RockContext ).GetId( placementRole.Value );

            if ( !groupId.HasValue || !roleId.HasValue )
            {
                return ActionBadRequest( "Invalid placement group selection." );
            }

            request.AssignedGroupId = groupId;
            request.AssignedGroupMemberRoleId = roleId;
            request.AssignedGroupMemberStatus = ( GroupMemberStatus ) ( int ) saveBag.PlacementGroupMemberStatus.Value;

            if ( saveBag.PlacementGroupMemberAttributeValues != null )
            {
                var groupMember = new GroupMember
                {
                    GroupId = request.AssignedGroupId.Value,
                    GroupRoleId = request.AssignedGroupMemberRoleId.Value
                };

                groupMember.LoadAttributes( RockContext );

                var memberAttributeValues = new Dictionary<string, string>();

                foreach ( var memberValue in saveBag.PlacementGroupMemberAttributeValues )
                {
                    if ( !groupMember.Attributes.TryGetValue( memberValue.Key, out var attribute )
                        || !attribute.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson ) )
                    {
                        return ActionBadRequest( "Invalid placement group member attribute." );
                    }

                    memberAttributeValues.Add( memberValue.Key, PublicAttributeHelper.GetPrivateValue( attribute, memberValue.Value ) );
                }

                request.AssignedGroupMemberAttributeValues = memberAttributeValues.ToJson();
            }
            else
            {
                request.AssignedGroupMemberAttributeValues = null;
            }

            return null;
        }

        /// <summary>
        /// Gets the manually triggered workflows for the opportunity that the
        /// current person may launch. Replicated from the existing block.
        /// </summary>
        /// <param name="connectionOpportunity">The opportunity.</param>
        /// <param name="currentPerson">The current person.</param>
        /// <returns>The manual workflows.</returns>
        private static IEnumerable<ConnectionWorkflow> GetConnectionOpportunityManualWorkflowTypes( ConnectionOpportunity connectionOpportunity, Person currentPerson )
        {
            return connectionOpportunity.ConnectionWorkflows
                .Union( connectionOpportunity.ConnectionType.ConnectionWorkflows )
                .Where( w => w.TriggerType == ConnectionWorkflowTriggerType.Manual
                    && w.WorkflowType != null
                    && ( w.WorkflowType.IsActive ?? true ) )
                .OrderBy( w => w.WorkflowType.Name )
                .Distinct()
                .Where( w => w.WorkflowType.IsAuthorized( Authorization.VIEW, currentPerson ) );
        }

        /// <summary>
        /// Launches a connection workflow for the request and returns the
        /// result. Replicated from the existing block.
        /// </summary>
        /// <param name="connectionRequest">The request the workflow runs against.</param>
        /// <param name="connectionWorkflow">The connection workflow to launch.</param>
        /// <param name="currentPerson">The current person.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <returns>The launch result.</returns>
        private static LaunchWorkflowResponseBag LaunchConnectionRequestWorkflow( ConnectionRequest connectionRequest, ConnectionWorkflow connectionWorkflow, Person currentPerson, RockContext rockContext )
        {
            var workflowService = new WorkflowService( rockContext );
            var workflowType = connectionWorkflow.WorkflowTypeCache;

            if ( workflowType == null || workflowType.IsActive == false )
            {
                return new LaunchWorkflowResponseBag
                {
                    WorkflowTypeGuid = workflowType?.Guid,
                    Errors = new List<string> { "Workflow was not found or is not active." }
                };
            }

            var workflow = Rock.Model.Workflow.Activate( workflowType, connectionWorkflow.WorkflowType.WorkTerm, rockContext );

            if ( !workflowService.Process( workflow, connectionRequest, out var workflowErrors ) )
            {
                return new LaunchWorkflowResponseBag
                {
                    WorkflowTypeGuid = workflowType.Guid,
                    WorkflowGuid = workflow.Guid,
                    Errors = workflowErrors?.ToList()
                };
            }

            if ( workflow.Id != 0 )
            {
                new ConnectionRequestWorkflowService( rockContext ).Add( new ConnectionRequestWorkflow
                {
                    ConnectionRequestId = connectionRequest.Id,
                    WorkflowId = workflow.Id,
                    ConnectionWorkflowId = connectionWorkflow.Id,
                    TriggerType = connectionWorkflow.TriggerType,
                    TriggerQualifier = connectionWorkflow.QualifierValue
                } );

                rockContext.SaveChanges();
            }

            return new LaunchWorkflowResponseBag
            {
                WorkflowTypeGuid = workflowType.Guid,
                WorkflowGuid = workflow.Guid,
                HasActiveEntryForm = workflow.HasActiveEntryForm( currentPerson ),
                Message = $"A '{workflowType.Name}' workflow was started."
            };
        }

        /// <summary>
        /// Creates the connector-assigned activity for the request, or
        /// <c>null</c> when there is no connector. Replicated from the existing
        /// block.
        /// </summary>
        /// <param name="connectionRequest">The request.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <returns>The activity to add, or <c>null</c>.</returns>
        private static ConnectionRequestActivity CreateAssignedActivity( ConnectionRequest connectionRequest, RockContext rockContext )
        {
            if ( !connectionRequest.ConnectorPersonAliasId.HasValue )
            {
                return null;
            }

            var guid = Rock.SystemGuid.ConnectionActivityType.ASSIGNED.AsGuid();
            var assignedActivityId = new ConnectionActivityTypeService( rockContext ).Queryable()
                .Where( t => t.Guid == guid )
                .Select( t => t.Id )
                .FirstOrDefault();

            if ( assignedActivityId == 0 )
            {
                return null;
            }

            return new ConnectionRequestActivity
            {
                ConnectionRequestId = connectionRequest.Id,
                ConnectionOpportunityId = connectionRequest.ConnectionOpportunityId,
                ConnectionActivityTypeId = assignedActivityId,
                ConnectorPersonAliasId = connectionRequest.ConnectorPersonAliasId
            };
        }

        /// <summary>
        /// Gets the placement-group member requirements for the assigned group,
        /// mapped into the mobile bag. Adapted from the existing block.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <returns>The member requirements (empty when no group is assigned).</returns>
        private static List<GroupMemberRequirementBag> GetPlacementGroupRequirements( ConnectionRequest request, RockContext rockContext )
        {
            var requirementResults = GetGroupRequirementStatuses( request, rockContext );
            var requirements = new List<GroupMemberRequirementBag>();

            foreach ( var requirementResult in requirementResults )
            {
                var isManual = requirementResult.GroupRequirement.GroupRequirementType.RequirementCheckType == RequirementCheckType.Manual;
                var label = isManual
                    ? requirementResult.GroupRequirement.GroupRequirementType.CheckboxLabel.IfEmpty( requirementResult.GroupRequirement.GroupRequirementType.Name )
                    : requirementResult.GroupRequirement.GroupRequirementType.Name;

                requirements.Add( new GroupMemberRequirementBag
                {
                    GroupRequirementGuid = requirementResult.GroupRequirement.Guid,
                    Label = label,
                    IsManual = isManual,
                    MustMeetRequirementToAddMember = requirementResult.GroupRequirement.MustMeetRequirementToAddMember,
                    GroupMemberRequirementState = ( MobileMeetsGroupRequirement ) ( int ) requirementResult.MeetsGroupRequirement
                } );
            }

            return requirements;
        }

        /// <summary>
        /// Gets the group requirement statuses for the request's assigned group.
        /// Replicated from the existing block.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <returns>The requirement statuses (excludes NotApplicable).</returns>
        private static List<PersonGroupRequirementStatus> GetGroupRequirementStatuses( ConnectionRequest request, RockContext rockContext )
        {
            if ( request == null || !request.AssignedGroupId.HasValue || request.PersonAlias == null )
            {
                return new List<PersonGroupRequirementStatus>();
            }

            var group = new GroupService( rockContext ).GetNoTracking( request.AssignedGroupId.Value );

            if ( group == null )
            {
                return new List<PersonGroupRequirementStatus>();
            }

            var requirementResults = group.PersonMeetsGroupRequirements( rockContext, request.PersonAlias.PersonId, request.AssignedGroupMemberRoleId );

            if ( requirementResults == null )
            {
                return new List<PersonGroupRequirementStatus>();
            }

            return requirementResults
                .Where( r => r.MeetsGroupRequirement != MeetsGroupRequirement.NotApplicable )
                .ToList();
        }

        /// <summary>
        /// Attempts to mark the request connected, performing the group
        /// placement and manual-requirement checks. Replicated from the
        /// existing block's TryMarkRequestConnected. Calls SaveChanges on
        /// success.
        /// </summary>
        /// <param name="connectionRequest">The request to connect.</param>
        /// <param name="manualRequirementsMet">The manual requirement acknowledgements keyed by requirement Guid.</param>
        /// <param name="currentPerson">The current person.</param>
        /// <param name="rockContext">The Rock database context.</param>
        /// <param name="errorMessage">On return, any error message.</param>
        /// <returns><c>true</c> when the request was connected.</returns>
        private static bool TryMarkRequestConnected( ConnectionRequest connectionRequest, Dictionary<Guid, bool> manualRequirementsMet, Person currentPerson, RockContext rockContext, out string errorMessage )
        {
            var groupMemberService = new GroupMemberService( rockContext );
            var connectionActivityTypeService = new ConnectionActivityTypeService( rockContext );
            var connectionRequestActivityService = new ConnectionRequestActivityService( rockContext );

            if ( connectionRequest?.PersonAlias == null || connectionRequest.ConnectionOpportunity == null )
            {
                errorMessage = "Connection request is not in a valid state.";
                return false;
            }

            if ( connectionRequest.ConnectionState == ConnectionState.Connected )
            {
                errorMessage = null;
                return true;
            }

            GroupMember groupMember = null;
            var hasGroupAssignment = connectionRequest.AssignedGroupId.HasValue
                && connectionRequest.AssignedGroupMemberRoleId.HasValue
                && connectionRequest.AssignedGroupMemberStatus.HasValue
                && connectionRequest.AssignedGroup != null;

            if ( hasGroupAssignment )
            {
                var group = connectionRequest.AssignedGroup;

                groupMember = groupMemberService.GetByGroupIdAndPersonIdAndGroupRoleId(
                    connectionRequest.AssignedGroupId.Value,
                    connectionRequest.PersonAlias.PersonId,
                    connectionRequest.AssignedGroupMemberRoleId.Value );

                if ( groupMember == null )
                {
                    groupMember = new GroupMember
                    {
                        PersonId = connectionRequest.PersonAlias.PersonId,
                        GroupId = connectionRequest.AssignedGroupId.Value,
                        GroupRoleId = connectionRequest.AssignedGroupMemberRoleId.Value,
                        GroupMemberStatus = connectionRequest.AssignedGroupMemberStatus.Value
                    };

                    var manualGroupRequirements = group.GetGroupRequirements( rockContext )
                        .Where( r => r.GroupRequirementType.RequirementCheckType == RequirementCheckType.Manual )
                        .ToList();

                    foreach ( var requirement in manualGroupRequirements )
                    {
                        var meetsRequirement = manualRequirementsMet.GetValueOrDefault( requirement.Guid, false );

                        if ( !meetsRequirement && requirement.MustMeetRequirementToAddMember )
                        {
                            errorMessage = "Group Requirements have not been met. Please verify all of the requirements.";
                            return false;
                        }

                        if ( meetsRequirement )
                        {
                            groupMember.GroupMemberRequirements.Add( new GroupMemberRequirement
                            {
                                GroupRequirementId = requirement.Id,
                                RequirementMetDateTime = RockDateTime.Now,
                                LastRequirementCheckDateTime = RockDateTime.Now
                            } );
                        }
                    }

                    groupMemberService.Add( groupMember );

                    if ( connectionRequest.AssignedGroupMemberAttributeValues.IsNotNullOrWhiteSpace() )
                    {
                        var savedValues = connectionRequest.AssignedGroupMemberAttributeValues.FromJsonOrNull<Dictionary<string, string>>();

                        if ( savedValues != null )
                        {
                            groupMember.LoadAttributes();

                            foreach ( var item in savedValues )
                            {
                                groupMember.SetAttributeValue( item.Key, item.Value );
                            }
                        }
                    }
                }
            }

            var connectedGuid = Rock.SystemGuid.ConnectionActivityType.CONNECTED.AsGuid();
            var connectedActivityId = connectionActivityTypeService.Queryable()
                .Where( t => t.Guid == connectedGuid )
                .Select( t => t.Id )
                .FirstOrDefault();

            if ( connectedActivityId > 0 )
            {
                connectionRequestActivityService.Add( new ConnectionRequestActivity
                {
                    ConnectionRequestId = connectionRequest.Id,
                    ConnectionOpportunityId = connectionRequest.ConnectionOpportunityId,
                    ConnectionActivityTypeId = connectedActivityId,
                    ConnectorPersonAliasId = currentPerson.PrimaryAliasId
                } );
            }

            connectionRequest.ConnectionState = ConnectionState.Connected;

            rockContext.WrapTransaction( () =>
            {
                rockContext.SaveChanges();

                if ( groupMember != null && connectionRequest.AssignedGroupMemberAttributeValues.IsNotNullOrWhiteSpace() )
                {
                    groupMember.SaveAttributeValues( rockContext );
                }
            } );

            errorMessage = null;
            return true;
        }

        #endregion
    }
}
