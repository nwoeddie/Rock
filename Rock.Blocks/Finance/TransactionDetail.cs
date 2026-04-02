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
using System.Transactions;

using AngleSharp.Dom;

using dotless.Core.Parser.Functions;

using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Utility;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Event.EventItemDetail;
using Rock.ViewModels.Blocks.Finance.BenevolenceRequestDetail;
using Rock.ViewModels.Blocks.Finance.TransactionDetail;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

using static System.Data.Entity.Infrastructure.Design.Executor;
using static Humanizer.In;

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

    [DefinedValueField(
        "Location Types",
        Description = "The location types to display for the authorized person. If none are selected, all addresses will be shown.",
        Key = AttributeKey.LocationTypes,
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.GROUP_LOCATION_TYPE,
        AllowMultiple = true,
        IsRequired = false,
        Order = 5 )]

    [BooleanField( "Enable Foreign Currency",
        Description = "Shows the transaction's currency code field if enabled.",
        DefaultBooleanValue = false,
        Order = 6,
        Key = AttributeKey.EnableForeignCurrency )]

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
            public const string LocationTypes = "LocationTypes";
            public const string EnableForeignCurrency = "EnableForeignCurrency";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new DetailBlockBox<TransactionBag, TransactionDetailOptionsBag>();


            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions( box.IsEditable );
            SetBoxInitialEntityState( box );
            box.Options.ShowForeignCurrencyFields = GetAttributeValue( AttributeKey.EnableForeignCurrency ).AsBoolean();
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
            RockContext.SqlLogging( true );
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
            RockContext.SqlLogging( false );
        }

        private TransactionBag GetBaseTransactionBag( FinancialTransaction transaction )
        {
            var creditCardGuid = Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.AsGuid();

            // Only query for the aggregate sums — everything else comes from the already-loaded entity
            var aggregates = new FinancialTransactionDetailService( RockContext )
                .Queryable()
                .Where( d => d.TransactionId == transaction.Id )
                .GroupBy( d => d.TransactionId )
                .Select( g => new
                {
                    TotalAmount = g.Sum( d => ( decimal? ) d.Amount ) ?? 0m,
                    TotalFeeAmount = g.Sum( d => ( decimal? ) d.FeeAmount ) ?? 0m,
                    TotalFeeCoverageAmount = g.Sum( d => ( decimal? ) d.FeeCoverageAmount ) ?? 0m
                } )
                .FirstOrDefault();

            return new TransactionBag
            {
                Id = transaction.Id,
                BatchId = transaction.BatchId,
                ScheduledTransactionId = transaction.ScheduledTransactionId,
                AuthorizedPersonAliasId = transaction.AuthorizedPersonAliasId,
                ShowAsAnonymous = transaction.ShowAsAnonymous,
                TransactionDateTime = transaction.TransactionDateTime,
                TransactionTypeValueId = transaction.TransactionTypeValueId,
                SourceTypeValueId = transaction.SourceTypeValueId,
                TransactionCode = transaction.TransactionCode,
                Summary = transaction.Summary,
                FinancialGatewayId = transaction.FinancialGatewayId,
                FinancialGateway = transaction.FinancialGateway?.Name,
                NonCashAssetTypeValueId = transaction.NonCashAssetTypeValueId,
                ForeignCurrencyCodeValueId = transaction.ForeignCurrencyCodeValueId,

                TotalAmount = aggregates?.TotalAmount ?? 0m,
                TotalFeeAmount = aggregates?.TotalFeeAmount ?? 0m,
                TotalFeeCoverageAmount = aggregates?.TotalFeeCoverageAmount ?? 0m,

                Batch = transaction.Batch == null
                    ? null
                    : new ListItemBag
                    {
                        Value = transaction.Batch.Name
                    },

                ProcessedByPersonAlias = transaction.ProcessedByPersonAlias?.Person == null
                    ? null
                    : new ListItemBag
                    {
                        Value = transaction.ProcessedByPersonAlias.Person.NickName + " " +
                                transaction.ProcessedByPersonAlias.Person.LastName
                    },

                PaymentDetail = GetPaymentDetailBag( transaction.FinancialPaymentDetail, creditCardGuid ),
                RefundDetails = GetRefundDetailBag( transaction.RefundDetails ),
                AuthorizedPerson = GetAuthorizedPersonBag( transaction.AuthorizedPersonAlias?.Person )
            };
        }

        /// <inheritdoc/>
        protected override TransactionBag GetEntityBagForView( FinancialTransaction entity )
        {
            if ( entity == null )
            {
                return null;
            }

            var bag = GetBaseTransactionBag( entity );

            if ( bag == null )
            {
                return null;
            }

            if( bag.SourceTypeValueId != null)
            {
                bag.SourceType = DefinedValueCache.GetName( bag.SourceTypeValueId.Value );
            }

            bag.TransactionType = DefinedValueCache.GetName( bag.TransactionTypeValueId );

            bag.IdKey = bag.Id.AsIdKey();

            bag.TransactionDetails = GetTransactionLineItemBags( entity.Id );
            bag.Images = GetImageBags( entity.Id );
            bag.Updates = GetTransactionUpdates( entity );
            bag.RelatedTransactions = GetRelatedTransactions( entity );
            bag.Refunds = GetRefundOverview( entity.Id );

            var foreignCurrencyDisplay = GetForeignCurrencyDisplay( bag.ForeignCurrencyCodeValueId, out var foreignCurrencySymbol );
            bag.ForeignCurrencyDisplay = foreignCurrencyDisplay;
            bag.ForeignCurrencySymbol = foreignCurrencySymbol;

            entity.LoadAttributes( RockContext );
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

            var bag = entity.Id == 0
                ? new TransactionBag()
                : GetBaseTransactionBag( entity );

            if ( entity.Id != 0 )
            {
                bag.TransactionDetails = GetTransactionLineItemBags( entity.Id );
                bag.Images = GetImageBags( entity.Id );
            }

            entity.LoadAttributes( RockContext );
            bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: false );

            return bag;
        }

        /// <inheritdoc/>

        /// <inheritdoc/>
        protected override FinancialTransaction GetInitialEntity()
        {
            var id = PageParameter( PageParameterKey.TransactionId ).AsInteger();

            if ( id == 0 )
            {
                return new FinancialTransaction
                {
                    FinancialPaymentDetail = new FinancialPaymentDetail()
                };
            }

            return new FinancialTransactionService( RockContext )
                .Queryable()
                .Include( t => t.Batch )
                .Include( t => t.FinancialPaymentDetail )
                .Include( t => t.RefundDetails )
                .Include( t => t.ProcessedByPersonAlias.Person )
                .Include( t => t.AuthorizedPersonAlias.Person )
                .Include(t => t.FinancialGateway)
                .Where( t => t.Id == id )
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

        /// <summary>
        ///  Gets Details of the refund transaction
        /// </summary>
        /// <param name="refundDetails"></param>
        /// <returns></returns>
        private RefundDetailBag GetRefundDetailBag( FinancialTransactionRefund refundDetails )
        {
            if ( refundDetails == null )
            {
                return null;
            }

            var refundReason = refundDetails.RefundReasonValueId.HasValue
                ? DefinedValueCache.GetName( refundDetails.RefundReasonValueId.Value )
                : null;

            return new RefundDetailBag
            {
                OriginalTransactionId = refundDetails.OriginalTransactionId,
                RefundReasonValueId = refundDetails.RefundReasonValueId,
                RefundReason = refundReason,
                RefundReasonSummary = refundDetails.RefundReasonSummary
            };
        }

        private List<RefundTransactionBag> GetRefundOverview( int originalTransactionId )
        {
            return new FinancialTransactionRefundService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( r => r.OriginalTransactionId == originalTransactionId && r.FinancialTransaction != null )
                .OrderBy( r => r.FinancialTransaction.TransactionDateTime )
                .Select( r => new RefundTransactionBag
                {
                    Id = r.FinancialTransaction.Id,
                    Date = r.FinancialTransaction.TransactionDateTime,
                    TransactionCode = r.FinancialTransaction.TransactionCode,
                    Reason = r.RefundReasonValue.Value,
                    Amount = r.FinancialTransaction.TransactionDetails.Sum( d => d.Amount )
                } )
                .ToList();
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

        private List<TransactionLineItemBag> GetTransactionLineItemBags( int transactionId )
        {
            return new FinancialTransactionDetailService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( d => d.TransactionId == transactionId )
                .Select( d => new TransactionLineItemBag
                {
                    Guid = d.Guid,
                    Account = d.Account == null ? null : new ListItemBag
                    {
                        Value = d.Account.Name
                    },
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
                    EntityType = d.EntityType == null ? null : new ListItemBag
                    {
                        Value = d.EntityType.Name
                    },
                    CanEdit = true,
                    CanDelete = !d.EntityTypeId.HasValue,
                    IsTotalRow = false
                } )
                .ToList();
        }

        private PaymentDetailBag GetPaymentDetailBag( FinancialPaymentDetail paymentDetail, Guid creditCardGuid )
        {
            if ( paymentDetail == null )
            {
                return null;
            }

            var currencyType = paymentDetail.CurrencyTypeValueId.HasValue
                ? DefinedValueCache.Get( paymentDetail.CurrencyTypeValueId.Value )
                : null;

            var creditCardType = paymentDetail.CreditCardTypeValueId.HasValue
                ? DefinedValueCache.GetName( paymentDetail.CreditCardTypeValueId.Value )
                : null;

            return new PaymentDetailBag
            {
                CurrencyTypeValueId = paymentDetail.CurrencyTypeValueId,
                CurrencyType = currencyType == null
                    ? null
                    : new ListItemBag { Value = currencyType.Value },
                CreditCardTypeValueId = paymentDetail.CreditCardTypeValueId,
                CreditCardType = creditCardType,
                NameOnCard = paymentDetail.NameOnCard,
                AccountNumberMasked = paymentDetail.AccountNumberMasked,
                ExpirationDate = paymentDetail.ExpirationDate,
                IsCreditCard = currencyType != null && currencyType.Guid == creditCardGuid
            };
        }

        private string GetForeignCurrencyDisplay( int? foreignCurrencyCodeValueId, out string symbol )
        {
            if ( !GetAttributeValue( AttributeKey.EnableForeignCurrency ).AsBoolean() )
            {
                symbol = string.Empty;
                return string.Empty;
            }

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
        }

        private AuthorizedPersonBag GetAuthorizedPersonBag( Person person )
        {
            if ( person == null )
            {
                return null;
            }

            var addresses = GetAuthorizedPersonAddresses( person.Id );

            return new AuthorizedPersonBag
            {
                Guid = person.Guid,
                Id = person.Id,
                Name = person.FullName,
                Email = person.Email,
                PhotoUrl = person.PhotoUrl,
                Addresses = addresses,
                Campus = person.PrimaryCampusId.HasValue
                    ? CampusCache.Get( person.PrimaryCampusId.Value )?.Name
                    : null
            };
        }

        private List<AddressBag> GetAuthorizedPersonAddresses( int personId )
        {
            var allowedLocationTypeIds = GetAttributeValue( AttributeKey.LocationTypes )
                .SplitDelimitedValues()
                .AsGuidList()
                .Select( g => DefinedValueCache.Get( g ) )
                .Where( dv => dv != null )
                .Select( dv => dv.Id )
                .ToList();

            var familyGroupTypeId = GroupTypeCache.GetFamilyGroupType()?.Id;

            var query = new GroupLocationService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( gl =>
                    gl.Location != null &&
                    gl.Location.Street1 != null &&
                    gl.Group.GroupTypeId == familyGroupTypeId &&
                    gl.Group.Members.Any( m => m.PersonId == personId ) );

            if ( allowedLocationTypeIds.Any() )
            {
                query = query.Where( gl =>
                    gl.GroupLocationTypeValueId.HasValue &&
                    allowedLocationTypeIds.Contains( gl.GroupLocationTypeValueId.Value ) );
            }

            return query
                .OrderByDescending( gl => gl.IsMailingLocation )
                .ThenByDescending( gl => gl.IsMappedLocation )
                .ThenByDescending( gl => gl.CreatedDateTime )
                .Select( gl => new AddressBag
                {
                    Type = gl.GroupLocationTypeValue.Value,
                    FormattedAddress = gl.Location.Street1
                        + ( gl.Location.Street2 != null ? " " + gl.Location.Street2 : "" )
                        + ", " + gl.Location.City
                        + ", " + gl.Location.State
                        + " " + gl.Location.PostalCode,
                    IsPrimary = gl.IsMailingLocation
                } )
                .ToList();
        }

        public List<string> GetTransactionUpdates( FinancialTransaction transaction )
        {
            var updates = new List<string>();

            var rootUrl = ResolveRockUrlIncludeRoot( "/" );

            var transactionUpdatePeople = new FinancialTransactionService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( t => t.Id == transaction.Id )
                .Select( t => new
                {
                    CreatedByPersonId = ( int? ) t.CreatedByPersonAlias.Person.Id,
                    CreatedByPersonNickName = t.CreatedByPersonAlias.Person.NickName,
                    CreatedByPersonLastName = t.CreatedByPersonAlias.Person.LastName,

                    ProcessedByPersonId = ( int? ) t.ProcessedByPersonAlias.Person.Id,
                    ProcessedByPersonNickName = t.ProcessedByPersonAlias.Person.NickName,
                    ProcessedByPersonLastName = t.ProcessedByPersonAlias.Person.LastName,

                    ModifiedByPersonId = ( int? ) t.ModifiedByPersonAlias.Person.Id,
                    ModifiedByPersonNickName = t.ModifiedByPersonAlias.Person.NickName,
                    ModifiedByPersonLastName = t.ModifiedByPersonAlias.Person.LastName,
                } )
                .FirstOrDefault();

            if ( transactionUpdatePeople == null )
            {
                return updates;
            }

            if ( transactionUpdatePeople.CreatedByPersonId.HasValue && transaction.CreatedDateTime.HasValue )
            {
                var person = new Person
                {
                    Id = transactionUpdatePeople.CreatedByPersonId.Value,
                    NickName = transactionUpdatePeople.CreatedByPersonNickName,
                    LastName = transactionUpdatePeople.CreatedByPersonLastName
                };

                updates.Add( $"Created by {person.GetAnchorTag( rootUrl )} on {transaction.CreatedDateTime.Value.ToShortDateString()} at {transaction.CreatedDateTime.Value.ToShortTimeString()}" );
            }

            if ( transactionUpdatePeople.ProcessedByPersonId.HasValue && transaction.ProcessedDateTime.HasValue )
            {
                var person = new Person
                {
                    Id = transactionUpdatePeople.ProcessedByPersonId.Value,
                    NickName = transactionUpdatePeople.ProcessedByPersonNickName,
                    LastName = transactionUpdatePeople.ProcessedByPersonLastName
                };

                updates.Add( $"Created by {person.GetAnchorTag( rootUrl )} on {transaction.ProcessedDateTime.Value.ToShortDateString()} at {transaction.ProcessedDateTime.Value.ToShortTimeString()}" );
            }

            if ( transactionUpdatePeople.ModifiedByPersonId.HasValue && transaction.ModifiedDateTime.HasValue )
            {
                var person = new Person
                {
                    Id = transactionUpdatePeople.ModifiedByPersonId.Value,
                    NickName = transactionUpdatePeople.ModifiedByPersonNickName,
                    LastName = transactionUpdatePeople.ModifiedByPersonLastName
                };

                updates.Add( $"Created by {person.GetAnchorTag( rootUrl )} on {transaction.ModifiedDateTime.Value.ToShortDateString()} at {transaction.ModifiedDateTime.Value.ToShortTimeString()}" );
            }

            return updates;
        }

        /// <summary>
        /// Resolves the rock URL and includes the original scheme and domain
        /// from the request.
        /// </summary>
        /// <param name="context">The context of the current request.</param>
        /// <param name="url">The URL to ben resolved.</param>
        /// <returns>A new string resolved to the proper domain.</returns>
        private string ResolveRockUrlIncludeRoot( string url )
        {
            var virtualPath = RequestContext.ResolveRockUrl( url );

            if ( !virtualPath.StartsWith( "/" ) )
            {
                return virtualPath;
            }

            if ( RequestContext.RootUrlPath.IsNotNullOrWhiteSpace() )
            {
                return $"{RequestContext.RootUrlPath}{virtualPath}";
            }

            return GlobalAttributesCache.Get().GetValue( "PublicApplicationRoot" ) + virtualPath.RemoveLeadingForwardslash();
        }

        private List<RelatedTransactionBag> GetRelatedTransactions( FinancialTransaction transaction )
        {
            if ( transaction == null
                || !transaction.FinancialGatewayId.HasValue
                || transaction.TransactionCode.IsNullOrWhiteSpace()
                || !transaction.AuthorizedPersonAliasId.HasValue )
            {
                return new List<RelatedTransactionBag>();
            }

            return new FinancialTransactionService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( t =>
                    t.FinancialGatewayId == transaction.FinancialGatewayId &&
                    t.TransactionCode == transaction.TransactionCode &&
                    t.AuthorizedPersonAliasId == transaction.AuthorizedPersonAliasId &&
                    t.Id != transaction.Id &&
                    t.TransactionDateTime.HasValue )
                .OrderBy( t => t.TransactionDateTime )
                .Select( t => new RelatedTransactionBag
                {
                    Id = t.Id,
                    TransactionDateTime = t.TransactionDateTime.Value,
                    TransactionReference = t.TransactionCode,
                    Amount = t.TransactionDetails.Sum( d => d.Amount )
                } )
                .ToList();
        }

        private List<TransactionImageBag> GetImageBags( int transactionId )
        {
            var imageBag = new FinancialTransactionImageService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( i => i.TransactionId == transactionId )
                .OrderBy( i => i.Order )
                .Select( ( i ) => new TransactionImageBag
                {
                    Guid = i.Guid,
                    BinaryFileId = i.BinaryFileId,
                } )
                .ToList();

            if(imageBag.Any())
            {
                foreach(var image in imageBag)
                {
                    image.ImageUrl = FileUrlHelper.GetImageUrl( image.BinaryFileId );
                }
            }

            return imageBag;
        }

        protected override bool UpdateEntityFromBox( FinancialTransaction entity, ValidPropertiesBox<TransactionBag> box )
        {
            if ( box.ValidProperties == null )
            {
                return false;
            }

            SaveTransactionImages( box.Bag, entity );

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


        private void SaveTransactionImages(TransactionBag bag, FinancialTransaction entity )
        {
            var images = bag.Images;
            if ( images.Any() )
            {
                foreach( var image in images)
                {
                    var binaryFileId = image.ImageItemBag.GetEntityId<BinaryFile>( RockContext );

                    if ( binaryFileId != null )
                    {
                        if( image.IsMarkedForDeletion)
                        {
                            RemoveImageFromTransaction( binaryFileId.Value, entity.Id );
                        }
                        else
                        {
                            MaintainOrAddImage( binaryFileId.Value, entity.Id );
                        }
                    }
                    
                }
            }
        }

        private void RemoveImageFromTransaction( int binaryFileId, int transactionId )
        {

            // First set the binary file to temporary so it is removed
            var binaryFileService = new BinaryFileService( RockContext );

            var imagePreview = binaryFileService.Get( binaryFileId );

            if( imagePreview != null )
            {
                imagePreview.IsTemporary = true;
            }

            // Remove the transaction reference so it does not get pulled in while it is temporarily in db

            var txnImageService = new FinancialTransactionImageService( RockContext );

            var txnImageToRemove = txnImageService.Queryable().Where( i => i.BinaryFileId == binaryFileId && i.TransactionId == transactionId ).FirstOrDefault();

            if( txnImageToRemove != null )
            {
                txnImageService.Delete( txnImageToRemove );
            }

        }

        private void MaintainOrAddImage( int binaryFileId, int transactionId )
        {
            var txnImageService = new FinancialTransactionImageService( RockContext );
            var binaryFileService = new BinaryFileService( RockContext );

            // Check if the transaction image already exists
            var existingTxnImage = txnImageService.Queryable()
                .Where( i => i.BinaryFileId == binaryFileId && i.TransactionId == transactionId )
                .FirstOrDefault();

            if ( existingTxnImage == null )
            {
                // Add new transaction image
                var newTxnImage = new FinancialTransactionImage
                {
                    BinaryFileId = binaryFileId,
                    TransactionId = transactionId
                };
                txnImageService.Add( newTxnImage );

                // Mark the binary file as not temporary so it is persisted
                var binaryFile = binaryFileService.Get( binaryFileId );
                if ( binaryFile != null )
                {
                    binaryFile.IsTemporary = false;
                }
            }
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
