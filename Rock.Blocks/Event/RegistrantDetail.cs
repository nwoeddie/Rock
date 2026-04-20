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
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Event.RegistrantDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Event
{
    /// <summary>
    /// Displays the details of a particular registrant and allows editing.
    /// </summary>

    [DisplayName( "Registrant Detail" )]
    [Category( "Event" )]
    [Description( "Displays the details of a particular registrant." )]
    [IconCssClass( "fa fa-user" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    [Rock.SystemGuid.EntityTypeGuid( "6e675de9-b320-4f79-9a94-6389901039f7" )]
    [Rock.SystemGuid.BlockTypeGuid( "8208ca56-7675-4741-b0d9-54a10b492011" )]
    public class RegistrantDetail : RockEntityDetailBlockType<RegistrationRegistrant, RegistrantBag>
    {
        #region Keys

        private static class PageParameterKey
        {
            public const string RegistrantId = "RegistrantId";
            public const string RegistrationId = "RegistrationId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new DetailBlockBox<RegistrantBag, RegistrantDetailOptionsBag>();
            var entity = GetInitialEntity();

            SetBoxInitialEntityState( box, entity );
            box.NavigationUrls = GetBoxNavigationUrls( entity );
            box.Options = GetBoxOptions( entity?.RegistrationId ?? 0 );

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the edit panel,
        /// including fee template definitions needed to render fee controls.
        /// The RegistrationId page parameter is always present in the URL, so we
        /// load the template directly from it rather than traversing entity navigation properties.
        /// </summary>
        private RegistrantDetailOptionsBag GetBoxOptions( int registrationId )
        {

            if ( registrationId == 0 )
            {
                return new RegistrantDetailOptionsBag { Fees = new List<RegistrationTemplateFeeBag>() };
            }

            var template = new RegistrationService( RockContext )
                .Queryable()
                .Include( r => r.RegistrationInstance.RegistrationTemplate.Fees.Select( f => f.FeeItems ) )
                .Where( r => r.Id == registrationId )
                .Select( r => r.RegistrationInstance.RegistrationTemplate )
                .FirstOrDefault();

            var fees = template?.Fees?
                .Where( f => f.IsActive )
                .OrderBy( f => f.Order )
                .Select( f => new RegistrationTemplateFeeBag
                {
                    Id = f.Id,
                    Name = f.Name,
                    FeeType = f.FeeType,
                    AllowMultiple = f.AllowMultiple,
                    DiscountApplies = f.DiscountApplies,
                    Order = f.Order,
                    Items = f.FeeItems
                        .OrderBy( i => i.Order )
                        .Select( i => new RegistrationTemplateFeeItemBag
                        {
                            Id = i.Id,
                            Name = i.Name,
                            Cost = i.Cost,
                            MaximumUsageCount = i.MaximumUsageCount,
                            Order = i.Order
                        } ).ToList()
                } ).ToList();

            return new RegistrantDetailOptionsBag
            {
                Fees = fees ?? new List<RegistrationTemplateFeeBag>()
            };
        }

        /// <summary>
        /// Validates the registrant entity for any issues not covered by model validation.
        /// </summary>
        /// <param name="entity">The entity to validate.</param>
        /// <param name="errorMessage">On <c>false</c> return, contains the error message.</param>
        /// <returns><c>true</c> if the entity is valid; otherwise <c>false</c>.</returns>
        private bool ValidateRegistrationRegistrant( RegistrationRegistrant entity, out string errorMessage )
        {
            errorMessage = null;

            return true;
        }

        /// <summary>
        /// Sets the initial entity state on the box. This block is edit-only so the
        /// entity is always placed into edit mode if the person has permission.
        /// </summary>
        private void SetBoxInitialEntityState( DetailBlockBox<RegistrantBag, RegistrantDetailOptionsBag> box, RegistrationRegistrant entity )
        {
            if ( entity == null )
            {
                box.ErrorMessage = $"The {RegistrationRegistrant.FriendlyTypeName} was not found.";
                return;
            }

            box.IsEditable = BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );

            if ( box.IsEditable )
            {
                box.Entity = GetEntityBagForEdit( entity );
            }
            else
            {
                box.ErrorMessage = EditModeMessage.NotAuthorizedToEdit( RegistrationRegistrant.FriendlyTypeName );
            }

            PrepareDetailBox( box, entity );
        }

        /// <summary>
        /// Builds the bag fields that are common to every call returning registrant data.
        /// </summary>
        private RegistrantBag GetCommonEntityBag( RegistrationRegistrant entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var registration = GetRegistration( entity );
            var instance = registration?.RegistrationInstance;
            var template = instance?.RegistrationTemplate;

            return new RegistrantBag
            {
                IdKey = entity.IdKey,
                PersonAlias = entity.PersonAlias != null
                    ? new ListItemBag
                    {
                        Value = entity.PersonAlias.IdKey,
                        Text = entity.PersonAlias.Person?.FullName
                    }
                    : null,
                Cost = entity.Cost,
                DiscountApplies = entity.DiscountApplies,
                IsOnWaitList = entity.OnWaitList,
                Fees = entity.Fees?.Select( f => new RegistrantFeeBag
                {
                    RegistrationTemplateFeeId = f.RegistrationTemplateFeeId,
                    RegistrationTemplateFeeItemId = f.RegistrationTemplateFeeItemId,
                    Quantity = f.Quantity,
                    Cost = f.Cost,
                    Option = f.Option
                } ).ToList(),
                SignatureDocumentId = entity.SignatureDocumentId,
                SignatureDocumentBinaryFileId = entity.SignatureDocument?.BinaryFileId,
                RegistrationIdKey = registration?.IdKey,
                RegistrationTemplateName = template?.Name,
                RegistrationInstanceName = instance?.Name,
                RegistrationName = registration?.ToString(),
                IsWaitListEnabled = template?.WaitListEnabled ?? false,
                SignatureDocumentTemplateName = template?.RequiredSignatureDocumentTemplate?.Name,
                SignatureDocumentTemplateBinaryFileTypeGuid = template?.RequiredSignatureDocumentTemplate?.BinaryFileType?.Guid,
                HasExistingSignatureDocument = false
            };
        }

        /// <inheritdoc/>
        protected override RegistrantBag GetEntityBagForView( RegistrationRegistrant entity )
        {
            // This block is edit-only; view mode is not used.
            return GetEntityBagForEdit( entity );
        }

        /// <inheritdoc/>
        protected override RegistrantBag GetEntityBagForEdit( RegistrationRegistrant entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetCommonEntityBag( entity );

            if ( entity.Attributes == null )
            {
                entity.LoadAttributes( RockContext );
            }

            // Only expose attributes that are registrant-attribute-sourced fields from the
            // registration template forms. Person and GroupMember attributes are edited on
            // their own detail blocks.
            var allowedAttributeKeys = GetRegistrantAttributeKeys( entity );
            bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: false );

            if ( allowedAttributeKeys.Count > 0 )
            {
                bag.Attributes = bag.Attributes?
                    .Where( kvp => allowedAttributeKeys.Contains( kvp.Key ) )
                    .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

                bag.AttributeValues = bag.AttributeValues?
                    .Where( kvp => allowedAttributeKeys.Contains( kvp.Key ) )
                    .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
            }
            else
            {
                bag.Attributes = null;
                bag.AttributeValues = null;
            }

            // Check for an existing valid signature document linked to this person but
            // not yet attached to the registrant.
            if ( entity.Id != 0 && entity.PersonAlias != null && entity.SignatureDocument == null )
            {
                var registration = GetRegistration( entity );
                var template = registration?.RegistrationInstance?.RegistrationTemplate;

                if ( template?.RequiredSignatureDocumentTemplate != null )
                {
                    var existingDoc = new RegistrationRegistrantService( RockContext )
                        .GetValidSignatureDocument( entity.PersonAlias.PersonId, template.RequiredSignatureDocumentTemplate )
                        .FirstOrDefault();

                    bag.HasExistingSignatureDocument = existingDoc != null;
                }
            }

            return bag;
        }

        /// <inheritdoc/>
        protected override bool UpdateEntityFromBox( RegistrationRegistrant entity, ValidPropertiesBox<RegistrantBag> box )
        {
            if ( box.ValidProperties == null )
            {
                return false;
            }

            // For new registrants, resolve and set RegistrationId first since other
            // operations (e.g. attribute key lookup) depend on it.
            if ( entity.Id == 0 )
            {
                var registrationIdKey = box.Bag.RegistrationIdKey.IsNotNullOrWhiteSpace()
                    ? box.Bag.RegistrationIdKey
                    : null;

                var registrationId = registrationIdKey != null
                    ? new RegistrationService( RockContext )
                        .Get( registrationIdKey, !PageCache.Layout.Site.DisablePredictableIds )
                        ?.Id
                    : PageParameter( PageParameterKey.RegistrationId ).AsIntegerOrNull();

                if ( !registrationId.HasValue || registrationId.Value == 0 )
                {
                    return false;
                }

                entity.RegistrationId = registrationId.Value;
                entity.RegistrationTemplateId = new RegistrationService( RockContext )
                    .GetSelect( entity.RegistrationId, r => r.RegistrationTemplateId ) ?? 0;
            }

            box.IfValidProperty( nameof( box.Bag.PersonAlias ), () =>
            {
                if ( box.Bag.PersonAlias?.Value.IsNotNullOrWhiteSpace() == true )
                {
                    var personAlias = new PersonAliasService( RockContext )
                        .Get( box.Bag.PersonAlias.Value.AsGuid() );

                    entity.PersonAliasId = personAlias?.Id;
                }
            } );

            box.IfValidProperty( nameof( box.Bag.Cost ),
                () => entity.Cost = box.Bag.Cost );

            box.IfValidProperty( nameof( box.Bag.DiscountApplies ),
                () => entity.DiscountApplies = box.Bag.DiscountApplies );

            box.IfValidProperty( nameof( box.Bag.IsOnWaitList ),
                () => entity.OnWaitList = box.Bag.IsOnWaitList );

            box.IfValidProperty( nameof( box.Bag.AttributeValues ), () =>
            {
                entity.LoadAttributes( RockContext );

                var allowedAttributeKeys = GetRegistrantAttributeKeys( entity );
                var filteredValues = box.Bag.AttributeValues?
                    .Where( kvp => allowedAttributeKeys.Contains( kvp.Key ) )
                    .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

                entity.SetPublicAttributeValues( filteredValues, RequestContext.CurrentPerson, enforceSecurity: false );
            } );

            return true;
        }

        /// <inheritdoc/>
        protected override RegistrationRegistrant GetInitialEntity()
        {
            var registrantParameter = PageParameter( PageParameterKey.RegistrantId );

            // New registrant - Get RegistrationId from Page Parameter to get Fee Template data
            if( registrantParameter.AsInteger() == 0 )
            {
                var registrationParameter = PageParameter( PageParameterKey.RegistrationId );

                if ( registrationParameter.AsInteger() == 0 )
                {
                    return null;
                }

                var registration = new RegistrationService( RockContext )
                    .GetQueryableByKey( registrationParameter, !PageCache.Layout.Site.DisablePredictableIds )
                    .FirstOrDefault();

                if ( registration == null )
                {
                    return null;
                }

                return new RegistrationRegistrant
                {
                    RegistrationId = registration.Id,
                    RegistrationTemplateId = registration.RegistrationInstance?.RegistrationTemplate?.Id ?? 0
                };
            }

            return new RegistrationRegistrantService( RockContext )
                .GetQueryableByKey( registrantParameter, !PageCache.Layout.Site.DisablePredictableIds )
                .Include( r => r.Registration.RegistrationInstance.RegistrationTemplate.Forms.Select( f => f.Fields ) )
                .Include( r => r.Registration.RegistrationInstance.RegistrationTemplate.RequiredSignatureDocumentTemplate.BinaryFileType )
                .Include( r => r.PersonAlias.Person )
                .Include( r => r.Fees )
                .Include( r => r.SignatureDocument )
                .FirstOrDefault();
            
        }

        /// <summary>
        /// Gets the box navigation URLs, including the parent registration detail page
        /// with the RegistrationId parameter preserved.
        /// </summary>
        private Dictionary<string, string> GetBoxNavigationUrls( RegistrationRegistrant entity )
        {
            var registrationId = entity?.RegistrationId ?? 0;

            var parentParams = registrationId > 0
                ? new Dictionary<string, string> { [PageParameterKey.RegistrationId] = registrationId.ToString() }
                : null;

            return new Dictionary<string, string>
            {
                [NavigationUrlKey.ParentPage] = this.GetParentPageUrl( parentParams )
            };
        }

        /// <inheritdoc/>
        protected override bool TryGetEntityForEditAction( string idKey, out RegistrationRegistrant entity, out BlockActionResult error )
        {
            var entityService = new RegistrationRegistrantService( RockContext );
            error = null;

            if ( idKey.IsNotNullOrWhiteSpace() )
            {
                entity = entityService.Get( idKey, !PageCache.Layout.Site.DisablePredictableIds );
            }
            else
            {
                entity = new RegistrationRegistrant();
                entityService.Add( entity );
            }

            if ( entity == null )
            {
                error = ActionBadRequest( $"{RegistrationRegistrant.FriendlyTypeName} not found." );
                return false;
            }

            if ( !BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
            {
                error = ActionBadRequest( $"Not authorized to edit {RegistrationRegistrant.FriendlyTypeName}." );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the set of attribute keys the block may display and save, limited to
        /// <see cref="RegistrationFieldSource.RegistrantAttribute"/> fields defined on the
        /// registration template forms.
        /// </summary>
        private HashSet<string> GetRegistrantAttributeKeys( RegistrationRegistrant entity )
        {
            var template = GetRegistration( entity )?.RegistrationInstance?.RegistrationTemplate;

            if ( template?.Forms == null )
            {
                return new HashSet<string>();
            }

            return template.Forms
                .SelectMany( f => f.Fields )
                .Where( f => f.FieldSource == RegistrationFieldSource.RegistrantAttribute && f.AttributeId.HasValue )
                .Select( f => AttributeCache.Get( f.AttributeId.Value )?.Key )
                .Where( k => k != null )
                .ToHashSet();
        }

        /// <summary>
        /// Returns the <see cref="Registration"/> for this registrant, loading it from the
        /// database with required navigation properties if the property is not already populated.
        /// </summary>
        private Registration GetRegistration( RegistrationRegistrant entity )
        {
            if ( entity == null )
            {
                return null;
            }

            if ( entity.Registration?.RegistrationInstance?.RegistrationTemplate != null )
            {
                return entity.Registration;
            }

            if ( entity.RegistrationId == 0 )
            {
                return null;
            }

            return new RegistrationService( RockContext )
                .Queryable()
                .Include( r => r.RegistrationInstance.RegistrationTemplate.Forms.Select( f => f.Fields ) )
                .Include( r => r.RegistrationInstance.RegistrationTemplate.Fees )
                .Include( r => r.RegistrationInstance.RegistrationTemplate.RequiredSignatureDocumentTemplate.BinaryFileType )
                .FirstOrDefault( r => r.Id == entity.RegistrationId );
        }

        #endregion Methods

        #region Block Actions

        /// <summary>
        /// Saves the registrant and redirects to the parent registration detail page.
        /// </summary>
        [BlockAction]
        public BlockActionResult Save( ValidPropertiesBox<RegistrantBag> box )
        {
            if ( !TryGetEntityForEditAction( box.Bag.IdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            var isNew = entity.Id == 0;

            var personAliasService = new PersonAliasService( RockContext );
            var registrantService = new RegistrationRegistrantService( RockContext );
            var registrantFeeService = new RegistrationRegistrantFeeService( RockContext );
            var registrationTemplateFeeService = new RegistrationTemplateFeeService( RockContext );

            // Capture pre-update state for history tracking.
            var previousRegistrantPersonIds = isNew
                ? new List<int>()
                : registrantService.Queryable()
                    .Where( r => r.RegistrationId == entity.RegistrationId && r.PersonAlias != null )
                    .Select( r => r.PersonAlias.PersonId )
                    .ToList();

            var registrantChanges = new History.HistoryChangeList();

            if ( isNew )
            {
                registrantChanges.AddChange( History.HistoryVerb.Add, History.HistoryChangeType.Record, "Registrant" );
            }

            var originalPersonAliasId = entity.PersonAliasId;
            var originalPersonName = entity.PersonAlias?.Person?.FullName ?? string.Empty;
            var originalCost = entity.Cost;
            var originalDiscountApplies = entity.DiscountApplies;

            // Pre-load attributes to capture original values before UpdateEntityFromBox
            // overwrites them in memory.
            entity.LoadAttributes( RockContext );
            var originalAttributeValues = entity.Attributes?.Keys
                .ToDictionary( key => key, key => entity.GetAttributeValue( key ) )
                ?? new Dictionary<string, string>();

            if ( !UpdateEntityFromBox( entity, box ) )
            {
                return ActionBadRequest( "Invalid data." );
            }

            if ( !ValidateRegistrationRegistrant( entity, out var validationMessage ) )
            {
                return ActionBadRequest( validationMessage );
            }

            // Resolve the updated person for history and group member handling.
            var personAlias = entity.PersonAliasId.HasValue
                ? personAliasService.Get( entity.PersonAliasId.Value )
                : null;

            var newPerson = personAlias?.Person;

            var personChanged = !originalPersonAliasId.Equals( entity.PersonAliasId );

            if ( personChanged )
            {
                History.EvaluateChange( registrantChanges, "Person", originalPersonName, newPerson?.FullName ?? string.Empty );
            }

            History.EvaluateChange( registrantChanges, "Cost", originalCost, entity.Cost );
            History.EvaluateChange( registrantChanges, "Discount Applies", originalDiscountApplies, entity.DiscountApplies );

            // ── Fees ──────────────────────────────────────────────────────────

            foreach ( var dbFee in entity.Fees.ToList() )
            {
                var uiFee = box.Bag.Fees?.FirstOrDefault( f =>
                    f.RegistrationTemplateFeeId == dbFee.RegistrationTemplateFeeId &&
                    f.RegistrationTemplateFeeItemId == dbFee.RegistrationTemplateFeeItemId &&
                    f.Quantity > 0 );

                if ( uiFee == null )
                {
                    var oldValue = $"'{dbFee.RegistrationTemplateFee?.Name}' Fee (Quantity:{dbFee.Quantity:N0}, Cost:{dbFee.Cost:C2}, Option:{dbFee.Option}";
                    registrantChanges.AddChange( History.HistoryVerb.Delete, History.HistoryChangeType.Record, "Fee" ).SetOldValue( oldValue );
                    entity.Fees.Remove( dbFee );
                    registrantFeeService.Delete( dbFee );
                }
            }

            foreach ( var uiFee in box.Bag.Fees ?? new List<RegistrantFeeBag>() )
            {
                if ( uiFee.Quantity <= 0 )
                {
                    continue;
                }

                var dbFee = entity.Fees.FirstOrDefault( f =>
                    f.RegistrationTemplateFeeId == uiFee.RegistrationTemplateFeeId &&
                    f.RegistrationTemplateFeeItemId == uiFee.RegistrationTemplateFeeItemId );

                if ( dbFee == null )
                {
                    dbFee = new RegistrationRegistrantFee
                    {
                        RegistrationTemplateFeeId = uiFee.RegistrationTemplateFeeId,
                        RegistrationTemplateFeeItemId = uiFee.RegistrationTemplateFeeItemId,
                        Option = uiFee.Option
                    };

                    entity.Fees.Add( dbFee );
                }

                var templateFee = dbFee.RegistrationTemplateFee
                    ?? registrationTemplateFeeService.Get( uiFee.RegistrationTemplateFeeId );

                string feeName = templateFee?.Name ?? "Fee";
                if ( uiFee.Option.IsNotNullOrWhiteSpace() )
                {
                    feeName = $"{feeName} ({uiFee.Option})";
                }

                if ( dbFee.Id <= 0 )
                {
                    registrantChanges.AddChange( History.HistoryVerb.Add, History.HistoryChangeType.Record, "Fee" ).SetNewValue( feeName );
                }

                History.EvaluateChange( registrantChanges, $"{feeName} Quantity", dbFee.Quantity, uiFee.Quantity );
                dbFee.Quantity = uiFee.Quantity;

                History.EvaluateChange( registrantChanges, $"{feeName} Cost", dbFee.Cost, uiFee.Cost );
                dbFee.Cost = uiFee.Cost;
            }

            // ── Signature Document ────────────────────────────────────────────

            var registration = GetRegistration( entity );
            var template = registration?.RegistrationInstance?.RegistrationTemplate;

            if ( template?.RequiredSignatureDocumentTemplate != null && entity.PersonAliasId.HasValue && newPerson != null )
            {
                HandleSignatureDocument( entity, template, newPerson.Id, box.Bag.SignatureDocumentId, box.Bag.SignatureDocumentBinaryFileId );
            }

            // ── Registration Session ──────────────────────────────────────────

            Guid? registrationSessionGuid = null;
            var registrationInstance = new RegistrationInstanceService( RockContext ).Get( registration.RegistrationInstanceId );

            if ( registrationInstance?.TimeoutIsEnabled == true )
            {
                // Reserve a spot when adding a new registrant (not to the wait list) or
                // when moving an existing registrant off the wait list.
                bool isMovingOffWaitList = !isNew && !entity.OnWaitList;
                bool needsCapacityCheck = ( isNew && !entity.OnWaitList ) || isMovingOffWaitList;

                if ( needsCapacityCheck )
                {
                    var registrationSession = CreateRegistrationSession( registration );
                    if ( registrationSession == null )
                    {
                        return ActionBadRequest( "Registration is full." );
                    }

                    registrationSessionGuid = registrationSession.Guid;
                }
            }

            // ── Save ──────────────────────────────────────────────────────────

            string registrantName = newPerson?.FullName ?? "Unknown";

            try
            {
                RockContext.WrapTransaction( () =>
                {
                    RockContext.SaveChanges();

                    // Reload attributes from DB now that the entity is saved so we get
                    // attribute values tied to the correct entity ID.
                    entity.LoadAttributes( RockContext );

                    var allowedAttributeKeys = GetRegistrantAttributeKeys( entity );
                    foreach ( var key in allowedAttributeKeys )
                    {
                        var attribute = entity.Attributes?.GetValueOrNull( key );
                        if ( attribute == null )
                        {
                            continue;
                        }

                        string originalValue = originalAttributeValues.GetValueOrNull( key ) ?? string.Empty;
                        string newValue = box.Bag.AttributeValues?.GetValueOrNull( key ) ?? string.Empty;

                        if ( ( originalValue ?? string.Empty ).Trim() != ( newValue ?? string.Empty ).Trim() )
                        {
                            string formattedOriginal = originalValue.IsNotNullOrWhiteSpace()
                                ? attribute.FieldType.Field.FormatValue( null, originalValue, attribute.QualifierValues, false )
                                : string.Empty;

                            string formattedNew = newValue.IsNotNullOrWhiteSpace()
                                ? attribute.FieldType.Field.FormatValue( null, newValue, attribute.QualifierValues, false )
                                : string.Empty;

                            History.EvaluateChange( registrantChanges, attribute.Name, formattedOriginal, formattedNew );
                        }

                        entity.SetAttributeValue( key, newValue );
                    }

                    entity.SaveAttributeValues( RockContext );
                } );

                if ( ( isNew || personChanged ) && template?.GroupTypeId.HasValue == true && newPerson != null )
                {
                    HandleGroupMember( entity, registration, template, newPerson.Id, registrantChanges );
                }

                if ( registration.FirstName.IsNotNullOrWhiteSpace() && registration.LastName.IsNotNullOrWhiteSpace() )
                {
                    registration.SavePersonNotesAndHistory(
                        registration.FirstName,
                        registration.LastName,
                        RequestContext.CurrentPerson?.PrimaryAliasId,
                        previousRegistrantPersonIds );
                }

                HistoryService.SaveChanges(
                    RockContext,
                    typeof( Registration ),
                    Rock.SystemGuid.Category.HISTORY_EVENT_REGISTRATION.AsGuid(),
                    entity.RegistrationId,
                    registrantChanges,
                    "Registrant: " + registrantName,
                    null,
                    null );
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
                return ActionBadRequest( ex.Message );
            }
            finally
            {
                if ( registrationSessionGuid.HasValue )
                {
                    RegistrationSessionService.CloseAndRemoveSession( registrationSessionGuid.Value );
                }
            }

            return ActionOk( GetBoxNavigationUrls( entity )[NavigationUrlKey.ParentPage] );
        }

        #endregion Block Actions

        #region Private Helpers

        /// <summary>
        /// Handles all signature document logic for the registrant: attaches an existing document,
        /// creates a new one from an uploaded file, or finds a valid existing document for the person
        /// when no file is provided.
        /// </summary>
        private void HandleSignatureDocument( RegistrationRegistrant entity, RegistrationTemplate template, int personId, int? existingDocumentId, int? newBinaryFileId )
        {
            var documentService = new SignatureDocumentService( RockContext );
            var binaryFileService = new BinaryFileService( RockContext );
            SignatureDocument document = null;

            if ( existingDocumentId.HasValue )
            {
                document = documentService.Get( existingDocumentId.Value );
                if ( entity.SignatureDocument == null )
                {
                    entity.SignatureDocument = document;
                }
            }

            if ( document == null && newBinaryFileId.HasValue )
            {
                document = CreateSignatureDocument( documentService, entity, template, personId, newBinaryFileId );
            }
            else if ( document == null )
            {
                var existingId = new RegistrationRegistrantService( RockContext )
                    .GetValidSignatureDocument( personId, template.RequiredSignatureDocumentTemplate )
                    .Select( d => ( int? ) d.Id )
                    .FirstOrDefault();

                if ( existingId.HasValue )
                {
                    entity.SignatureDocumentId = existingId;
                }
            }

            if ( document != null && newBinaryFileId.HasValue && document.BinaryFileId != newBinaryFileId.Value )
            {
                var registrantsUsing = new RegistrationRegistrantService( RockContext )
                    .GetRegistrantsUsingSignatureDocument( document.Id );

                if ( registrantsUsing.Count() > 1 )
                {
                    document = CreateSignatureDocument( documentService, entity, template, personId, newBinaryFileId );
                }
                else
                {
                    int? origBinaryFileId = document.BinaryFileId;
                    if ( origBinaryFileId.HasValue )
                    {
                        var oldFile = binaryFileService.Get( origBinaryFileId.Value );
                        if ( oldFile != null && !oldFile.IsTemporary )
                        {
                            oldFile.IsTemporary = true;
                        }
                    }

                    document.BinaryFileId = newBinaryFileId;
                }
            }

            if ( document?.BinaryFileId.HasValue == true )
            {
                var binaryFile = binaryFileService.Get( document.BinaryFileId.Value );
                if ( binaryFile != null && binaryFile.IsTemporary )
                {
                    binaryFile.IsTemporary = false;
                }
            }
        }

        /// <summary>
        /// Creates a new <see cref="SignatureDocument"/> for the registrant using the uploaded binary file.
        /// </summary>
        private SignatureDocument CreateSignatureDocument( SignatureDocumentService documentService, RegistrationRegistrant entity, RegistrationTemplate template, int personId, int? binaryFileId )
        {
            var registration = GetRegistration( entity );
            var person = new PersonService( RockContext ).Get( personId );

            var document = new SignatureDocument
            {
                SignatureDocumentTemplateId = template.RequiredSignatureDocumentTemplate.Id,
                AppliesToPersonAliasId = entity.PersonAliasId.Value,
                AssignedToPersonAliasId = entity.PersonAliasId.Value,
                Name = $"{person?.FullName.ReplaceSpecialCharacters( " " )} ({registration?.RegistrationInstance?.Name ?? template.Name})",
                Status = SignatureDocumentStatus.Signed,
                LastStatusDate = RockDateTime.Now,
                SignedDateTime = RockDateTime.Now,
                BinaryFileId = binaryFileId
            };

            documentService.Add( document );
            entity.SignatureDocument = document;
            return document;
        }

        /// <summary>
        /// Adds or updates the group member record for a new or re-assigned registrant when
        /// the registration template is linked to a group type.
        /// </summary>
        private void HandleGroupMember( RegistrationRegistrant entity, Registration registration, RegistrationTemplate template, int personId, History.HistoryChangeList registrantChanges )
        {
            using ( var newRockContext = new RockContext() )
            {
                var reloadedRegistrant = new RegistrationRegistrantService( newRockContext )
                    .Queryable()
                    .Include( r => r.Registration.Group.GroupType )
                    .Include( r => r.GroupMember )
                    .FirstOrDefault( r => r.Id == entity.Id );

                if ( reloadedRegistrant?.Registration?.Group?.GroupTypeId != template.GroupTypeId.Value )
                {
                    return;
                }

                int? groupRoleId = template.GroupMemberRoleId
                    ?? reloadedRegistrant.Registration.Group.GroupType.DefaultGroupRoleId;

                if ( !groupRoleId.HasValue )
                {
                    return;
                }

                var group = reloadedRegistrant.Registration.Group;
                var groupMemberService = new GroupMemberService( newRockContext );

                var groupMember = groupMemberService.Queryable().FirstOrDefault( m =>
                    m.GroupId == group.Id &&
                    m.PersonId == personId &&
                    m.GroupRoleId == groupRoleId.Value );

                if ( groupMember == null )
                {
                    groupMember = new GroupMember
                    {
                        GroupId = group.Id,
                        PersonId = personId,
                        GroupRoleId = groupRoleId.Value,
                        GroupMemberStatus = template.GroupMemberStatus
                    };

                    groupMemberService.Add( groupMember );
                    registrantChanges.AddChange( History.HistoryVerb.Add, History.HistoryChangeType.Record, $"Registrant to {group.Name} group" );
                }
                else
                {
                    registrantChanges.AddChange( History.HistoryVerb.Modify, History.HistoryChangeType.Record, $"Registrant to existing person in {group.Name} group" );
                }

                groupMember.GroupMemberStatus = template.GroupMemberStatus;

                if ( reloadedRegistrant.GroupMemberId.HasValue && reloadedRegistrant.GroupMemberId.Value != groupMember.Id )
                {
                    groupMemberService.Delete( reloadedRegistrant.GroupMember );
                    newRockContext.SaveChanges();
                    registrantChanges.AddChange( History.HistoryVerb.Delete, History.HistoryChangeType.Record, $"Registrant to previous person in {group.Name} group" );
                }

                reloadedRegistrant.GroupMemberId = groupMember.Id;
                newRockContext.SaveChanges();
            }
        }

        /// <summary>
        /// Creates a transient <see cref="RegistrationSession"/> to reserve a spot during save.
        /// Returns <c>null</c> if the registration is full.
        /// </summary>
        private RegistrationSession CreateRegistrationSession( Registration registration )
        {
            var session = RegistrationSessionService.CreateOrUpdateSession(
                Guid.Empty,
                () => new RegistrationSession
                {
                    Guid = Guid.NewGuid(),
                    RegistrationInstanceId = registration.RegistrationInstanceId,
                    RegistrationData = string.Empty,
                    SessionStartDateTime = RockDateTime.Now,
                    RegistrationCount = 1,
                    RegistrationId = registration.Id,
                    SessionStatus = SessionStatus.Transient
                },
                null,
                out string errorMessage );

            return errorMessage.IsNotNullOrWhiteSpace() ? null : session;
        }

        #endregion Private Helpers
    }
}
