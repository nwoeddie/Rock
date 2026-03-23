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
using System.Linq;

using AngleSharp.Dom;

using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Utility;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Finance.TransactionDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Finance
{
    /// <summary>
    /// Displays the details of a particular financial transaction.
    /// </summary>

    [DisplayName( "Transaction Detail" )]
    [Category( "Finance" )]
    [Description( "Displays the details of a particular financial transaction." )]
    [IconCssClass( "fa fa-question" )]
    // [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes
    [LinkedPage( "Scheduled Transaction Detail",
        Description = "The page that will show the scheduled transaction details.",
        Key = AttributeKey.ScheduledTransactionDetail )]
    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "31989b35-624b-4529-bc55-bb251e7fb1da" )]
    [Rock.SystemGuid.BlockTypeGuid( "f6f7a34c-c5d7-4cf9-a325-baf5300bce91" )]
    public class TransactionDetail : RockEntityDetailBlockType<FinancialTransaction, TransactionBag>
    {
        #region Keys

        private static class PageParameterKey
        {
            public const string TransactionId = "TransactionId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        private static class AttributeKey
        {
            public const string ScheduledTransactionDetail = "ScheduledTransactionDetail";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new DetailBlockBox<TransactionBag, TransactionDetailOptionsBag>();

            SetBoxInitialEntityState( box );

            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions( box.IsEditable );

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the view
        /// or edit the entity.
        /// </summary>
        /// <param name="isEditable"><c>true</c> if the entity is editable; otherwise <c>false</c>.</param>
        /// <returns>The options that provide additional details to the block.</returns>
        private TransactionDetailOptionsBag GetBoxOptions( bool isEditable )
        {
            var options = new TransactionDetailOptionsBag();

            return options;
        }

        /// <summary>
        /// Validates the Transaction for any final information that might not be
        /// valid after storing all the data from the client.
        /// </summary>
        /// <param name="financialTransaction">The Transaction to be validated.</param>
        /// <param name="errorMessage">On <c>false</c> return, contains the error message.</param>
        /// <returns><c>true</c> if the Transaction is valid, <c>false</c> otherwise.</returns>
        private bool ValidateTransaction( FinancialTransaction financialTransaction, out string errorMessage )
        {
            errorMessage = null;

            return true;
        }

        /// <summary>
        /// Sets the initial entity state of the box. Populates the Entity or
        /// ErrorMessage properties depending on the entity and permissions.
        /// </summary>
        /// <param name="box">The box to be populated.</param>
        private void SetBoxInitialEntityState( DetailBlockBox<TransactionBag, TransactionDetailOptionsBag> box )
        {
            var entity = GetInitialEntity();

            if ( entity == null )
            {
                box.ErrorMessage = $"The {FinancialTransaction.FriendlyTypeName} was not found.";
                return;
            }

            var isViewable = BlockCache.IsAuthorized( Authorization.VIEW, RequestContext.CurrentPerson );
            box.IsEditable = BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );

            if ( entity.Id != 0 )
            {
                // Existing entity was found, prepare for view mode by default.
                if ( isViewable )
                {
                    box.Entity = GetEntityBagForView( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToView( FinancialTransaction.FriendlyTypeName );
                }
            }
            else
            {
                // New entity is being created, prepare for edit mode by default.
                if ( box.IsEditable )
                {
                    box.Entity = GetEntityBagForEdit( entity );
                }
                else
                {
                    box.ErrorMessage = EditModeMessage.NotAuthorizedToEdit( FinancialTransaction.FriendlyTypeName );
                }
            }

            // Check if allowed to edit based on batch status if user has permissions to edit.
            if ( box.IsEditable)
            {
                box.Options.CanEdit = entity?.Batch == null
                    || ( entity.Batch.Status != BatchStatus.Closed && !entity.Batch.IsAutomated );
            }

            PrepareDetailBox( box, entity );
        }

        /// <summary>
        /// Gets the entity bag that is common between both view and edit modes.
        /// </summary>
        /// <param name="entity">The entity to be represented as a bag.</param>
        /// <returns>A <see cref="TransactionBag"/> that represents the entity.</returns>
        private TransactionBag GetCommonEntityBag( FinancialTransaction entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var foreignCurrencyDisplay = GetForeignCurrencyDisplay( entity.ForeignCurrencyCodeValueId, out var foreignCurrencySymbol );
            var totalForeignCurrencyAmount = entity.TransactionDetails?.Sum( d => d.ForeignCurrencyAmount ?? 0m );

            var person = entity.AuthorizedPersonAlias?.Person;

            return new TransactionBag
            {
                IdKey = entity.IdKey,

                AuthorizedPerson = GetAuthorizedPersonBag( person ),
                AuthorizedPersonAliasId = entity.AuthorizedPersonAliasId,

                Batch = entity.Batch?.ToListItemBag(),
                BatchId = entity.BatchId,

                FinancialGateway = entity.FinancialGateway?.ToListItemBag(),
                FinancialGatewayId = entity.FinancialGatewayId,

                PaymentDetail = GetPaymentDetailBag( entity.FinancialPaymentDetail ),

                ForeignCurrencyCodeValueId = entity.ForeignCurrencyCodeValueId,
                ForeignCurrencyDisplay = foreignCurrencyDisplay,
                ForeignCurrencySymbol = foreignCurrencySymbol,

                Images = entity.Images?
                    .OrderBy( i => i.Order )
                    .Select( ( i, index ) => new TransactionImageBag
                    {
                        BinaryFileId = i.BinaryFileId,
                        Order = i.Order,
                        IsPrimary = index == 0,
                        ImageUrl = FileUrlHelper.GetImageUrl( i.BinaryFileId ),
                        ThumbnailUrl = FileUrlHelper.GetImageUrl( i.BinaryFileId )
                    } )
                    .ToList(),

                NonCashAssetType = entity.NonCashAssetTypeValue?.ToListItemBag(),
                NonCashAssetTypeValueId = entity.NonCashAssetTypeValueId,

                ProcessedByPersonAlias = entity.ProcessedByPersonAlias?.ToListItemBag(),

                RefundDetails = GetRefundDetailBag( entity.RefundDetails ),

                ScheduledTransaction = GetScheduledTransactionBag( entity.ScheduledTransaction ),
                ScheduledTransactionId = entity.ScheduledTransactionId,

                ShowAsAnonymous = entity.ShowAsAnonymous,

                SourceType = entity.SourceTypeValue?.ToListItemBag(),
                SourceTypeValueId = entity.SourceTypeValueId,

                Status = entity.Status,
                StatusMessage = entity.StatusMessage,
                Summary = entity.Summary,
                TransactionCode = entity.TransactionCode,
                TransactionDateTime = entity.TransactionDateTime,

                TransactionDetails = GetTransactionLineItemBags( entity ),

                TransactionType = entity.TransactionTypeValue?.ToListItemBag(),
                TransactionTypeValueId = entity.TransactionTypeValueId,

                TotalAmount = entity.TotalAmount,
                TotalFeeAmount = entity.TotalFeeAmount,
                TotalFeeCoverageAmount = entity.TotalFeeCoverageAmount,
                TotalForeignCurrencyAmount = totalForeignCurrencyAmount
            };
        }

        /// <inheritdoc/>
        protected override TransactionBag GetEntityBagForView( FinancialTransaction entity )
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

            bag.LoadAttributesAndValuesForPublicView( entity, RequestContext.CurrentPerson, enforceSecurity: false );

            return bag;
        }

        //// <inheritdoc/>
        protected override TransactionBag GetEntityBagForEdit( FinancialTransaction entity )
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

            bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: false );

            return bag;
        }

        /// <inheritdoc/>
        protected override bool UpdateEntityFromBox( FinancialTransaction entity, ValidPropertiesBox<TransactionBag> box )
        {
            if ( box.ValidProperties == null )
            {
                return false;
            }

            //box.IfValidProperty( nameof( box.Bag.AuthorizedPersonAlias ),
            //    () => entity.AuthorizedPersonAliasId = box.Bag.AuthorizedPersonAlias.GetEntityId<PersonAlias>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.AuthorizedPersonAliasId ),
            //    () => entity.AuthorizedPersonAliasId = box.Bag.AuthorizedPersonAliasId );

            //box.IfValidProperty( nameof( box.Bag.Batch ),
            //    () => entity.BatchId = box.Bag.Batch.GetEntityId<Batch>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.BatchId ),
            //    () => entity.BatchId = box.Bag.BatchId );

            //box.IfValidProperty( nameof( box.Bag.CheckMicrEncrypted ),
            //    () => entity.CheckMicrEncrypted = box.Bag.CheckMicrEncrypted );

            //box.IfValidProperty( nameof( box.Bag.CheckMicrHash ),
            //    () => entity.CheckMicrHash = box.Bag.CheckMicrHash );

            //box.IfValidProperty( nameof( box.Bag.CheckMicrParts ),
            //    () => entity.CheckMicrParts = box.Bag.CheckMicrParts );

            //box.IfValidProperty( nameof( box.Bag.Gateway ),
            //    () => entity.GatewayId = box.Bag.Gateway.GetEntityId<Gateway>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.GatewayId ),
            //    () => entity.GatewayId = box.Bag.GatewayId );

            //box.IfValidProperty( nameof( box.Bag.PaymentDetail ),
            //    () => entity.PaymentDetailId = box.Bag.PaymentDetail.GetEntityId<PaymentDetail>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.PaymentDetailId ),
            //    () => entity.PaymentDetailId = box.Bag.PaymentDetailId );

            //box.IfValidProperty( nameof( box.Bag.ForeignCurrencyCodeValueId ),
            //    () => entity.ForeignCurrencyCodeValueId = box.Bag.ForeignCurrencyCodeValueId );

            //box.IfValidProperty( nameof( box.Bag.FutureProcessingDateTime ),
            //    () => entity.FutureProcessingDateTime = box.Bag.FutureProcessingDateTime );

            //box.IfValidProperty( nameof( box.Bag.Images ),
            //    () => entity.Images = box.Bag./* TODO: Unknown property type 'ICollection<TransactionImage>' for conversion to bag. */ );

            //box.IfValidProperty( nameof( box.Bag.IsReconciled ),
            //    () => entity.IsReconciled = box.Bag.IsReconciled );

            //box.IfValidProperty( nameof( box.Bag.IsSettled ),
            //    () => entity.IsSettled = box.Bag.IsSettled );

            //box.IfValidProperty( nameof( box.Bag.MICRStatus ),
            //    () => entity.MICRStatus = box.Bag.MICRStatus );

            //box.IfValidProperty( nameof( box.Bag.NonCashAssetTypeValue ),
            //    () => entity.NonCashAssetTypeValueId = box.Bag.NonCashAssetTypeValue.GetEntityId<DefinedValue>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.NonCashAssetTypeValueId ),
            //    () => entity.NonCashAssetTypeValueId = box.Bag.NonCashAssetTypeValueId );

            //box.IfValidProperty( nameof( box.Bag.ProcessedByPersonAlias ),
            //    () => entity.ProcessedByPersonAliasId = box.Bag.ProcessedByPersonAlias.GetEntityId<PersonAlias>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.RefundDetails ),
            //    () => entity.RefundDetailsId = box.Bag.RefundDetails.GetEntityId<TransactionRefund>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.ScheduledTransaction ),
            //    () => entity.ScheduledTransactionId = box.Bag.ScheduledTransaction.GetEntityId<ScheduledTransaction>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.ScheduledTransactionId ),
            //    () => entity.ScheduledTransactionId = box.Bag.ScheduledTransactionId );

            //box.IfValidProperty( nameof( box.Bag.SettledDate ),
            //    () => entity.SettledDate = box.Bag.SettledDate );

            //box.IfValidProperty( nameof( box.Bag.SettledDateKey ),
            //    () => entity.SettledDateKey = box.Bag.SettledDateKey );

            //box.IfValidProperty( nameof( box.Bag.SettledGroupId ),
            //    () => entity.SettledGroupId = box.Bag.SettledGroupId );

            //box.IfValidProperty( nameof( box.Bag.ShowAsAnonymous ),
            //    () => entity.ShowAsAnonymous = box.Bag.ShowAsAnonymous );

            //box.IfValidProperty( nameof( box.Bag.SourceTypeValue ),
            //    () => entity.SourceTypeValueId = box.Bag.SourceTypeValue.GetEntityId<DefinedValue>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.SourceTypeValueId ),
            //    () => entity.SourceTypeValueId = box.Bag.SourceTypeValueId );

            //box.IfValidProperty( nameof( box.Bag.Status ),
            //    () => entity.Status = box.Bag.Status );

            //box.IfValidProperty( nameof( box.Bag.StatusMessage ),
            //    () => entity.StatusMessage = box.Bag.StatusMessage );

            //box.IfValidProperty( nameof( box.Bag.Summary ),
            //    () => entity.Summary = box.Bag.Summary );

            //box.IfValidProperty( nameof( box.Bag.SundayDate ),
            //    () => entity.SundayDate = box.Bag.SundayDate );

            //box.IfValidProperty( nameof( box.Bag.TransactionCode ),
            //    () => entity.TransactionCode = box.Bag.TransactionCode );

            //box.IfValidProperty( nameof( box.Bag.TransactionDateKey ),
            //    () => entity.TransactionDateKey = box.Bag.TransactionDateKey );

            //box.IfValidProperty( nameof( box.Bag.TransactionDateTime ),
            //    () => entity.TransactionDateTime = box.Bag.TransactionDateTime );

            //box.IfValidProperty( nameof( box.Bag.TransactionDetails ),
            //    () => entity.TransactionDetails = box.Bag./* TODO: Unknown property type 'ICollection<TransactionDetail>' for conversion to bag. */ );

            //box.IfValidProperty( nameof( box.Bag.TransactionTypeValue ),
            //    () => entity.TransactionTypeValueId = box.Bag.TransactionTypeValue.GetEntityId<DefinedValue>( RockContext ) );

            //box.IfValidProperty( nameof( box.Bag.TransactionTypeValueId ),
            //    () => entity.TransactionTypeValueId = box.Bag.TransactionTypeValueId );

            //box.IfValidProperty( nameof( box.Bag.AttributeValues ),
            //    () =>
            //    {
            //        entity.LoadAttributes( RockContext );

            //        entity.SetPublicAttributeValues( box.Bag.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: false );
            //    } );

            return true;
        }

        /// <inheritdoc/>
        protected override FinancialTransaction GetInitialEntity()
        {
            var idKey = PageParameter( PageParameterKey.TransactionId );
            var id = idKey.AsInteger();

            if ( idKey.IsNullOrWhiteSpace() )
            {
                return new FinancialTransaction
                {
                    FinancialPaymentDetail = new FinancialPaymentDetail()
                };
            }

            return new FinancialTransactionService( RockContext )
                .Queryable()
                .Where( t => t.Id == id )
                .Select( t => t )
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.ParentPage] = this.GetParentPageUrl()
            };
        }

        /// <inheritdoc/>
        protected override bool TryGetEntityForEditAction( string idKey, out FinancialTransaction entity, out BlockActionResult error )
        {
            var entityService = new FinancialTransactionService( RockContext );
            error = null;

            // Determine if we are editing an existing entity or creating a new one.
            if ( idKey.IsNotNullOrWhiteSpace() )
            {
                // If editing an existing entity then load it and make sure it
                // was found and can still be edited.
                entity = entityService.Get( idKey, !PageCache.Layout.Site.DisablePredictableIds );
            }
            else
            {
                // Create a new entity.
                entity = new FinancialTransaction();
                entityService.Add( entity );
            }

            if ( entity == null )
            {
                error = ActionBadRequest( $"{FinancialTransaction.FriendlyTypeName} not found." );
                return false;
            }

            if ( !BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
            {
                error = ActionBadRequest( $"Not authorized to edit {FinancialTransaction.FriendlyTypeName}." );
                return false;
            }

            return true;
        }


        private PaymentDetailBag GetPaymentDetailBag( FinancialPaymentDetail paymentDetail )
        {
            if ( paymentDetail == null )
            {
                return new PaymentDetailBag();
            }

            return new PaymentDetailBag
            {
                CurrencyType = paymentDetail.CurrencyTypeValue?.ToListItemBag(),
                CurrencyTypeValueId = paymentDetail.CurrencyTypeValueId,
                CreditCardType = paymentDetail.CreditCardTypeValue?.ToListItemBag(),
                CreditCardTypeValueId = paymentDetail.CreditCardTypeValueId,
                NameOnCard = paymentDetail.NameOnCard,
                AccountNumberMasked = paymentDetail.AccountNumberMasked,
                ExpirationDate = paymentDetail.ExpirationDate
            };
        }

        private RefundDetailBag GetRefundDetailBag( FinancialTransactionRefund refundDetails )
        {
            if ( refundDetails == null )
            {
                return null;
            }

            return new RefundDetailBag
            {
                OriginalTransactionId = refundDetails.OriginalTransactionId,
                RefundReason = refundDetails.RefundReasonValue?.ToListItemBag(),
                RefundReasonValueId = refundDetails.RefundReasonValueId,
                RefundReasonSummary = refundDetails.RefundReasonSummary
            };
        }

        private ScheduledTransactionBag GetScheduledTransactionBag( FinancialScheduledTransaction scheduledTransaction )
        {
            if ( scheduledTransaction == null )
            {
                return null;
            }

            return new ScheduledTransactionBag
            {
                Id = scheduledTransaction.Id,
                DisplayText = scheduledTransaction.GatewayScheduleId.IsNotNullOrWhiteSpace()
                    ? scheduledTransaction.GatewayScheduleId
                    : scheduledTransaction.Id.ToString(),
                Url = this.GetLinkedPageUrl( AttributeKey.ScheduledTransactionDetail, "ScheduledTransactionId", scheduledTransaction.Id.ToString() )
            };
        }

        private List<TransactionLineItemBag> GetTransactionLineItemBags( FinancialTransaction entity )
        {
            if ( entity?.TransactionDetails == null )
            {
                return new List<TransactionLineItemBag>();
            }

            return entity.TransactionDetails
                .Select( d => new TransactionLineItemBag
                {
                    IdKey = d.IdKey,
                    Guid = d.Guid,
                    Account = d.Account?.ToListItemBag(),
                    AccountId = d.AccountId,
                    Amount = d.Amount,
                    AmountMinusFeeCoverageAmount = d.FeeCoverageAmount.HasValue
                        ? d.Amount - d.FeeCoverageAmount.Value
                        : d.Amount,
                    FeeAmount = d.FeeAmount,
                    FeeCoverageAmount = d.FeeCoverageAmount,
                    ForeignCurrencyAmount = d.ForeignCurrencyAmount,
                    Summary = d.Summary,
                    TransactionId = d.TransactionId,
                    EntityId = d.EntityId,
                    EntityTypeId = d.EntityTypeId,
                    EntityType = d.EntityType?.ToListItemBag(),

                    // For initial population only. You can refine later.
                    CanEdit = true,
                    CanDelete = !d.EntityTypeId.HasValue,
                    IsTotalRow = false
                } )
                .ToList();
        }

        private string GetForeignCurrencyDisplay( int? foreignCurrencyCodeValueId, out string symbol )
        {
            symbol = string.Empty;

            if ( !foreignCurrencyCodeValueId.HasValue )
            {
                return string.Empty;
            }

            var definedValue = DefinedValueCache.Get( foreignCurrencyCodeValueId.Value );
            if ( definedValue == null )
            {
                return string.Empty;
            }

            symbol = definedValue.GetAttributeValue( "Symbol" );
            return $"{definedValue.Value} {symbol}".Trim();
        }

        private AuthorizedPersonBag GetAuthorizedPersonBag( Person person )
        {
            if ( person == null )
            {
                return null;
            }

            var addresses = new List<AddressBag>();

            var groupLocations = person.PrimaryFamily?.GroupLocations;

            if ( groupLocations != null )
            {
                foreach ( var gl in groupLocations )
                {
                    if ( gl.Location == null )
                    {
                        continue;
                    }

                    var type = gl.GroupLocationTypeValue?.Value ?? "Other";

                    addresses.Add( new AddressBag
                    {
                        Type = type,
                        FormattedAddress = gl.Location.ToString(),
                        IsPrimary = gl.IsMailingLocation
                    } );
                }
            }

            return new AuthorizedPersonBag
            {
                IdKey = person.IdKey,
                Name = person.FullName,
                Email = person.Email,
                PhotoUrl = person.PhotoUrl,
                Addresses = addresses,
                PrimaryAddressType = addresses.FirstOrDefault( a => a.IsPrimary )?.Type
            };
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the box that will contain all the information needed to begin
        /// the edit operation.
        /// </summary>
        /// <param name="key">The identifier of the entity to be edited.</param>
        /// <returns>A box that contains the entity and any other information required.</returns>
        [BlockAction]
        public BlockActionResult Edit( string key )
        {
            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<TransactionBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Saves the entity contained in the box.
        /// </summary>
        /// <param name="box">The box that contains all the information required to save.</param>
        /// <returns>A new entity bag to be used when returning to view mode, or the URL to redirect to after creating a new entity.</returns>
        [BlockAction]
        public BlockActionResult Save( ValidPropertiesBox<TransactionBag> box )
        {
            var entityService = new FinancialTransactionService( RockContext );

            if ( !TryGetEntityForEditAction( box.Bag.IdKey, out var entity, out var actionError ) )
            {
                return actionError;
            }

            // Update the entity instance from the information in the bag.
            if ( !UpdateEntityFromBox( entity, box ) )
            {
                return ActionBadRequest( "Invalid data." );
            }

            // Ensure everything is valid before saving.
            if ( !ValidateTransaction( entity, out var validationMessage ) )
            {
                return ActionBadRequest( validationMessage );
            }

            var isNew = entity.Id == 0;

            RockContext.WrapTransaction( () =>
            {
                RockContext.SaveChanges();
                entity.SaveAttributeValues( RockContext );
            } );

            if ( isNew )
            {
                return ActionContent( System.Net.HttpStatusCode.Created, this.GetCurrentPageUrl( new Dictionary<string, string>
                {
                    [PageParameterKey.TransactionId] = entity.IdKey
                } ) );
            }

            // Ensure navigation properties will work now.
            entity = entityService.Get( entity.Id );
            entity.LoadAttributes( RockContext );

            var bag = GetEntityBagForEdit( entity );

            return ActionOk( new ValidPropertiesBox<TransactionBag>
            {
                Bag = bag,
                ValidProperties = bag.GetType().GetProperties().Select( p => p.Name ).ToList()
            } );
        }

        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="key">The identifier of the entity to be deleted.</param>
        /// <returns>A string that contains the URL to be redirected to on success.</returns>
        [BlockAction]
        public BlockActionResult Delete( string key )
        {
            var entityService = new FinancialTransactionService( RockContext );

            if ( !TryGetEntityForEditAction( key, out var entity, out var actionError ) )
            {
                return actionError;
            }

            if ( !entityService.CanDelete( entity, out var errorMessage ) )
            {
                return ActionBadRequest( errorMessage );
            }

            entityService.Delete( entity );
            RockContext.SaveChanges();

            return ActionOk( this.GetParentPageUrl() );
        }

        #endregion
    }
}
