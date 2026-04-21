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
     [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage(
    "Batch Detail Page",
    Description = "Page used to view batch.",
    IsRequired = true,
    Order = 0,
    Key = AttributeKey.BatchDetailPage )]

    [LinkedPage(
    "Scheduled Transaction Detail Page",
    Description = "Page used to view scheduled transaction detail.",
    IsRequired = false,
    Order = 1,
    Key = AttributeKey.ScheduledTransactionDetailPage )]

    [LinkedPage(
    "Registration Detail Page",
    Description = "Page used to view an event registration.",
    IsRequired = false,
    Order = 2,
    Key = AttributeKey.RegistrationDetailPage )]

    [TextField(
    "Refund Batch Name Suffix",
    Description = "The text appended to the batch name when a refund is processed. To use the original batch name without a suffix, disable Append Suffix to Batch Name.",
    IsRequired = false,
    DefaultValue = " - Refund",
    Order = 3,
    Key = AttributeKey.RefundBatchNameSuffix )]

    [BooleanField(
    "Append Suffix to Batch Name",
    Description = "When enabled, appends a suffix to the batch name for refund transactions. When disabled, uses the original batch name. Note: financial gateways that support settlement batches ignore this setting—all transactions process through the settlement batch regardless.",
    DefaultBooleanValue = true,
    Order = 4,
    Key = AttributeKey.AppendSuffixToBatchName )]

    [DefinedValueField(
    Rock.SystemGuid.DefinedType.GROUP_LOCATION_TYPE,
    "Location Types",
    Description = "The type of location type to display for person (if none are selected all addresses will be included ).",
    IsRequired = false,
    AllowMultiple = true,
    Order = 5,
    Key = AttributeKey.LocationTypes )]

    [BooleanField(
    "Transaction Source Required",
    Description = "Determine if Transaction Source should be required.",
    DefaultBooleanValue = false,
    Order = 6,
    Key = AttributeKey.TransactionSourceRequired )]

    [BooleanField(
    "Enable Foreign Currency",
    Description = "Shows the transaction's currency code field if enabled.",
    DefaultBooleanValue = false,
    Order = 7,
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
            public const string BatchId = "BatchId";
        }

        private static class NavigationUrlKey
        {
            public const string ParentPage = "ParentPage";
        }

        private static class AttributeKey
        {
            public const string BatchDetailPage = "BatchDetailPage";
            public const string ScheduledTransactionDetailPage = "ScheduledTransactionDetailPage";
            public const string RegistrationDetailPage = "RegistrationDetailPage";
            public const string RefundBatchNameSuffix = "RefundBatchNameSuffix";
            public const string AppendSuffixToBatchName = "AppendSuffixToBatchName";
            public const string LocationTypes = "LocationTypes";
            public const string TransactionSourceRequired = "TransactionSourceRequired";
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

            options.TransactionTypesGuid = SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE;

            options.TransactionSourceTypesGuid = SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE;

            options.RefundReasonTypesGuid = SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON;

            options.CurrencyTypesGuid = SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE;

            options.CurrencyCodesGuid = SystemGuid.DefinedType.FINANCIAL_CURRENCY_CODE;

            options.CreditCardTypesGuid = SystemGuid.DefinedType.FINANCIAL_CREDIT_CARD_TYPE;

            options.AssetTypes = SystemGuid.DefinedType.FINANCIAL_NONCASH_ASSET_TYPE;

            options.ShowForeignCurrencyFields = GetAttributeValue( AttributeKey.EnableForeignCurrency ).AsBoolean();

            options.TransactionSourceRequired = GetAttributeValue( AttributeKey.TransactionSourceRequired ).AsBoolean();

            options.BatchDetailPageUrl = this.GetLinkedPageUrl( AttributeKey.BatchDetailPage );

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

            if ( financialTransaction.BatchId == null || financialTransaction.BatchId == 0 )
            {
                errorMessage = "New transactions can only be added to an existing batch.";
                return false;
            }

            if ( financialTransaction.TransactionTypeValueId == 0 )
            {
                errorMessage = "Transaction type is required.";
                return false;
            }

            if ( financialTransaction.FinancialPaymentDetail == null )
            {
                errorMessage = "Payment detail is required.";
                return false;
            }

            if ( financialTransaction.RefundDetails != null )
            {
                var totalAmount = financialTransaction.TransactionDetails?.Sum( d => d.Amount ) ?? 0m;

                if ( totalAmount > 0 )
                {
                    errorMessage = "A refund should have a negative amount. Please unselect the refund option, or change amounts to be negative values.";
                    return false;
                }
            }

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

        private TransactionBag GetBaseTransactionBag( FinancialTransaction transaction )
        {
            var creditCardGuid = Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.AsGuid();

            // Only query for the aggregate sums — everything else comes from the already-loaded entity
            //var aggregates = new FinancialTransactionDetailService( RockContext )
            //    .Queryable()
            //    .Where( d => d.TransactionId == transaction.Id )
            //    .GroupBy( d => d.TransactionId )
            //    .Select( g => new
            //    {
            //        TotalAmount = g.Sum( d => ( decimal? ) d.Amount - ( d.FeeCoverageAmount ?? 0 ) ) ?? 0m,
            //        TotalFeeAmount = g.Sum( d => ( decimal? ) d.FeeAmount ) ?? 0m,
            //        TotalFeeCoverageAmount = g.Sum( d => ( decimal? ) d.FeeCoverageAmount ) ?? 0m
            //    } )
            //    .FirstOrDefault();

            var totalAmount = transaction.TransactionDetails?.Sum( d => d.Amount - ( d.FeeCoverageAmount ?? 0 ) ) ?? 0m;
            var totalFeeAmount = transaction.TransactionDetails?.Sum( d => d.FeeAmount ) ?? 0m;
            var totalFeeCoverageAmount = transaction.TransactionDetails?.Sum( d => d.FeeCoverageAmount ) ?? 0m;

            var transactionDefinedValue = DefinedValueCache.Get( transaction.TransactionTypeValueId );

            var sourceDefinedValue = transaction.SourceTypeValueId.HasValue ? DefinedValueCache.Get( transaction.SourceTypeValueId.Value ) : null;

            var currencyDefinedValue = transaction.ForeignCurrencyCodeValueId.HasValue ? DefinedValueCache.Get( transaction.ForeignCurrencyCodeValueId.Value ) : null;

            var assetTypeDefinedValue = transaction.NonCashAssetTypeValueId.HasValue ? DefinedValueCache.Get( transaction.NonCashAssetTypeValueId.Value ) : null;

            var financialGatewayDefinedValue = transaction.FinancialGatewayId.HasValue ? new FinancialGatewayService( RockContext ).Get( transaction.FinancialGatewayId.Value ) : null;

            return new TransactionBag
            {
                Id = transaction.Id,
                BatchId = transaction.BatchId,
                ScheduledTransactionId = transaction.ScheduledTransactionId,
                AuthorizedPersonAliasId = transaction.AuthorizedPersonAliasId,
                ShowAsAnonymous = transaction.ShowAsAnonymous,
                SourceType = sourceDefinedValue != null ? new ListItemBag
                {
                    Value = sourceDefinedValue.Guid.ToString(),
                    Text = sourceDefinedValue.Value
                }
                : null,
                TransactionType = new ListItemBag
                {
                    Value = transactionDefinedValue.Guid.ToString(),
                    Text = transactionDefinedValue.Value
                },
                TransactionCode = transaction.TransactionCode,
                Summary = transaction.Summary,
                FinancialGateway = financialGatewayDefinedValue != null ? new ListItemBag
                {
                    Value = financialGatewayDefinedValue.Guid.ToString(),
                    Text = financialGatewayDefinedValue.Name
                }
                : null,
                NonCashAssetType = assetTypeDefinedValue != null ? new ListItemBag
                {
                    Text = assetTypeDefinedValue.Value,
                    Value = assetTypeDefinedValue.Guid.ToString()
                }
                : null,
                CurrencyCode = currencyDefinedValue != null ? new ListItemBag
                {
                    Text = currencyDefinedValue.Value,
                    Value = currencyDefinedValue.Guid.ToString()
                }
                : null,
                TotalAmount = totalAmount,
                TotalFeeAmount = totalFeeAmount,
                TotalFeeCoverageAmount = totalFeeCoverageAmount,
                TransactionDateTime = transaction.TransactionDateTime,
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
                Status = transaction.Status,
                StatusMessage = transaction.StatusMessage,
                PaymentDetail = GetPaymentDetailBag( transaction.FinancialPaymentDetail, creditCardGuid ),
                RefundDetails = GetRefundDetailBag( transaction.RefundDetails ),
                AuthorizedPerson = GetAuthorizedPersonBag( transaction.AuthorizedPersonAlias?.Person ),
                ScheduledTransaction = transaction.ScheduledTransaction != null
                    ? GetScheduledTransactionBag( transaction.ScheduledTransaction )
                    : null,
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

            bag.IdKey = bag.Id.AsIdKey();

            bag.TransactionDetails = GetTransactionLineItemBags( entity.Id, entity.TransactionDetails );
            bag.Images = GetImageBags( entity.Id );
            bag.Updates = GetTransactionUpdates( entity );
            bag.RelatedTransactions = GetRelatedTransactions( entity );
            bag.Refunds = GetRefundOverview( entity.Id );

            bag.Registrations = GetRegistrations( entity );

            bag.IsRefund = entity.RefundDetails != null;

            var foreignCurrencySymbol = string.Empty;

            bag.ForeignCurrencyDisplay = bag.CurrencyCode != null ? GetForeignCurrencyDisplay( bag.CurrencyCode.Value, out foreignCurrencySymbol ) : null;
            bag.ForeignCurrencySymbol = foreignCurrencySymbol;

            bag.CanRefund =
                entity != null
                && entity.Id > 0
                && entity.RefundDetails == null
                && IsOrganizationCurrency( entity.ForeignCurrencyCodeValueId )
                && entity.IsAuthorized( Authorization.REFUND, RequestContext.CurrentPerson );

            entity.LoadAttributes( RockContext );
            bag.LoadAttributesAndValuesForPublicView( entity, RequestContext.CurrentPerson, enforceSecurity: false );

            return bag;
        }

        //// <inheritdoc/>
        protected override TransactionBag GetEntityBagForEdit( FinancialTransaction entity )
        {
            var batchId = PageParameter( PageParameterKey.BatchId ).AsInteger();

            if ( entity == null )
            {
                return null;
            }
            var bag = new TransactionBag();

            bag.BatchId = batchId;

            entity.LoadAttributes( RockContext );

            bag.LoadAttributesAndValuesForPublicEdit( entity, RequestContext.CurrentPerson, enforceSecurity: false );
            return bag;
        }

        /// <inheritdoc/>

        /// <inheritdoc/>
        protected override FinancialTransaction GetInitialEntity()
        {
            var parameter = PageParameter( PageParameterKey.TransactionId );

            if ( parameter.IsNullOrWhiteSpace() || parameter.AsInteger() == 0 )
            {
                return new FinancialTransaction
                {
                    FinancialPaymentDetail = new FinancialPaymentDetail()
                };
            }

            return new FinancialTransactionService( RockContext )
                .GetQueryableByKey( parameter, !PageCache.Layout.Site.DisablePredictableIds )
                .Include( t => t.Batch )
                .Include( t => t.FinancialPaymentDetail )
                .Include( t => t.RefundDetails )
                .Include( t => t.ProcessedByPersonAlias.Person )
                .Include( t => t.AuthorizedPersonAlias.Person )
                .Include( t => t.CreatedByPersonAlias.Person )
                .Include( t => t.ModifiedByPersonAlias.Person )
                .Include( t => t.ScheduledTransaction )
                .Include( t => t.TransactionDetails )
                .Include( t => t.TransactionDetails.Select( d => d.Account ) )
                .Include( t => t.TransactionDetails.Select( d => d.EntityType ) )
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

            var refundReasonDefinedValue = refundDetails.RefundReasonValueId.HasValue
                ? DefinedValueCache.Get( refundDetails.RefundReasonValueId.Value )
                : null;

            return new RefundDetailBag
            {
                OriginalTransactionId = refundDetails.OriginalTransactionId,
                RefundReason = refundReasonDefinedValue == null
                    ? null
                    : new ListItemBag
                    {
                        Value = refundReasonDefinedValue.Guid.ToString(),
                        Text = refundReasonDefinedValue.Value
                    },
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
                Url = this.GetLinkedPageUrl( AttributeKey.ScheduledTransactionDetailPage, "ScheduledTransactionId", scheduledTransaction.Id.ToString() )
            };
        }

        private TransactionDetailsBag GetTransactionLineItemBags( int transactionId, ICollection<FinancialTransactionDetail> transactionDetails )
        {
            var tempFinancialTransactionDetail = new FinancialTransactionDetail
            {
                TransactionId = transactionId
            };

            var attributes = tempFinancialTransactionDetail.GetPublicAttributesForEdit( RequestContext.CurrentPerson );
            var attributeValues = tempFinancialTransactionDetail.GetPublicAttributeValuesForEdit( RequestContext.CurrentPerson );

            var rows = ( transactionDetails ?? Enumerable.Empty<FinancialTransactionDetail>() )
                .Select( d => new TransactionLineItemBag
                {
                    Guid = d.Guid,
                    Id = d.Id,
                    Account = d.Account == null ? null : new ListItemBag
                    {
                        Text = d.Account.Name,
                        Value = d.Account.Guid.ToString()
                    },
                    Amount = d.FeeCoverageAmount.HasValue
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
                        Text = d.EntityType.FriendlyName,
                        Value = d.EntityType.Name
                    },
                    CanEdit = true,
                    CanDelete = !d.EntityTypeId.HasValue,
                    IsTotalRow = false
                } )
                .ToList();

            return new TransactionDetailsBag
            {
                Rows = rows,
                Attributes = attributes,
                AttributeValues = attributeValues
            };
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
                ? DefinedValueCache.Get( paymentDetail.CreditCardTypeValueId.Value )
                : null;


            return new PaymentDetailBag
            {
                CurrencyType = currencyType == null
                    ? null
                    : new ListItemBag {
                        Text = currencyType.Value,
                        Value = currencyType.Guid.ToString()
                    },
                CreditCardType = creditCardType == null
                    ? null
                    : new ListItemBag
                    {
                        Text = creditCardType.Value,
                        Value = creditCardType.Guid.ToString()
                    },
                NameOnCard = paymentDetail.NameOnCard,
                AccountNumberMasked = paymentDetail.AccountNumberMasked,
                ExpirationDate = paymentDetail.ExpirationDate,
                IsCreditCard = currencyType != null && currencyType.Guid == creditCardGuid
            };
        }

        private string GetForeignCurrencyDisplay( string currencyCodeGuid, out string symbol )
        {
            if ( !GetAttributeValue( AttributeKey.EnableForeignCurrency ).AsBoolean() )
            {
                symbol = string.Empty;
                return string.Empty;
            }

            {
                symbol = string.Empty;

                if ( !currencyCodeGuid.IsNotNullOrWhiteSpace() )
                {
                    return string.Empty;
                }

                var definedValue = DefinedValueCache.Get( currencyCodeGuid );
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

            if ( transaction.CreatedByPersonAlias?.Person != null && transaction.CreatedDateTime.HasValue )
            {
                updates.Add( $"Created by {transaction.CreatedByPersonAlias.Person.GetAnchorTag( rootUrl )} on {transaction.CreatedDateTime.Value.ToShortDateString()} at {transaction.CreatedDateTime.Value.ToShortTimeString()}" );
            }

            if ( transaction.ProcessedByPersonAlias?.Person != null && transaction.ProcessedDateTime.HasValue )
            {
                updates.Add( $"Processed by {transaction.ProcessedByPersonAlias.Person.GetAnchorTag( rootUrl )} on {transaction.ProcessedDateTime.Value.ToShortDateString()} at {transaction.ProcessedDateTime.Value.ToShortTimeString()}" );
            }

            if ( transaction.ModifiedByPersonAlias?.Person != null && transaction.ModifiedDateTime.HasValue )
            {
                updates.Add( $"Modified by {transaction.ModifiedByPersonAlias.Person.GetAnchorTag( rootUrl )} on {transaction.ModifiedDateTime.Value.ToShortDateString()} at {transaction.ModifiedDateTime.Value.ToShortTimeString()}" );
            }

            return updates;
        }

        private List<RegistrationLinkBag> GetRegistrations( FinancialTransaction transaction )
        {
            var registrationEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Registration ) )?.Id;

            if ( registrationEntityTypeId == null )
            {
                return new List<RegistrationLinkBag>();
            }

            var registrationIds = transaction.TransactionDetails
                .Where( d => d.EntityTypeId == registrationEntityTypeId )
                .Select( d => d.EntityId )
                .Where( id => id.HasValue )
                .Select( id => id.Value )
                .Distinct()
                .ToList();

            if ( !registrationIds.Any() )
            {
                return new List<RegistrationLinkBag>();
            }

            return new RegistrationService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( r =>
                    r.RegistrationInstance != null &&
                    r.RegistrationInstance.RegistrationTemplate != null &&
                    registrationIds.Contains( r.Id ) )
                .Select( r => new
                {
                    r.Id,
                    TemplateName = r.RegistrationInstance.RegistrationTemplate.Name,
                    InstanceName = r.RegistrationInstance.Name
                } )
                .ToList()
                .Select( r => new RegistrationLinkBag
                {
                    Id = r.Id,
                    Text = $"{r.TemplateName} - {r.InstanceName}",
                    Url = this.GetLinkedPageUrl( AttributeKey.RegistrationDetailPage, "RegistrationId", r.Id.ToString() )
                } )
                .ToList();
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
                    Amount = t.TransactionDetails.Select( d => ( decimal? ) d.Amount ).DefaultIfEmpty().Sum() ?? 0m
                } )
                .ToList();
        }

        private List<TransactionImageBag> GetImageBags( int transactionId )
        {
            var imageBag = new FinancialTransactionImageService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( i => i.TransactionId == transactionId && i.BinaryFile != null)
                .OrderBy( i => i.Order )
                .Select( ( i ) => new TransactionImageBag
                {
                    Guid = i.BinaryFile.Guid,
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
            if ( box?.Bag == null || box.ValidProperties == null )
            {
                return false;
            }

            // Match legacy behavior: always ensure payment detail exists before mapping fields.
            if ( entity.FinancialPaymentDetail == null )
            {
                entity.FinancialPaymentDetail = new FinancialPaymentDetail();
            }

            if( box.Bag.AuthorizedPerson != null)
            {
                box.Bag.AuthorizedPersonAliasId = new PersonAliasService( RockContext ).GetId( box.Bag.AuthorizedPerson.Guid);

                if ( box.Bag.AuthorizedPersonAliasId.HasValue )
                {
                    entity.AuthorizedPersonAliasId = box.Bag.AuthorizedPersonAliasId;
                }
            }

            if( box.Bag.BatchId != null )
            {
                entity.BatchId = box.Bag.BatchId;
            }

            // Core FinancialTransaction fields.
            box.IfValidProperty( nameof( box.Bag.ShowAsAnonymous ),
                () => entity.ShowAsAnonymous = box.Bag.ShowAsAnonymous );

            box.IfValidProperty( nameof( box.Bag.TransactionDateTime ),
                () => entity.TransactionDateTime = box.Bag.TransactionDateTime );

            box.IfValidProperty( nameof( box.Bag.TransactionCode ),
                () => entity.TransactionCode = box.Bag.TransactionCode );

            box.IfValidProperty( nameof( box.Bag.Summary ),
                () => entity.Summary = box.Bag.Summary );

            box.IfValidProperty( nameof( box.Bag.SourceType ),
                () => entity.SourceTypeValueId = box.Bag.SourceType?.GetEntityId<DefinedValue>( RockContext ) );

            // New transactions can set gateway; existing ones should not change it.
            if ( entity.Id == 0 )
            {
                box.IfValidProperty( nameof( box.Bag.FinancialGateway ),
                    () => entity.FinancialGatewayId = box.Bag.FinancialGateway?.GetEntityId<FinancialGateway>( RockContext ) );
            }

            // Defined values coming from ListItemBag selections.
            box.IfValidProperty( nameof( box.Bag.TransactionType ),
                () => entity.TransactionTypeValueId = box.Bag.TransactionType?.GetEntityId<DefinedValue>( RockContext ) ?? 0 );


            box.IfValidProperty( nameof( box.Bag.NonCashAssetType ),
                () => entity.NonCashAssetTypeValueId = box.Bag.NonCashAssetType?.GetEntityId<DefinedValue>( RockContext ) );

            box.IfValidProperty( nameof( box.Bag.CurrencyCode ),
                () =>
                {
                    if ( GetAttributeValue( AttributeKey.EnableForeignCurrency ).AsBoolean() )
                    {
                        entity.ForeignCurrencyCodeValueId = box.Bag.CurrencyCode?.GetEntityId<DefinedValue>( RockContext );
                    }
                } );

            // Payment detail fields.
            box.IfValidProperty( nameof( box.Bag.PaymentDetail ),
                () =>
                {
                    var paymentBag = box.Bag.PaymentDetail;
                    if ( paymentBag == null )
                    {
                        return;
                    }

                    entity.FinancialPaymentDetail.CurrencyTypeValueId =
                        paymentBag.CurrencyType?.GetEntityId<DefinedValue>( RockContext );

                    var isCreditCard = paymentBag.CurrencyType?.Value == Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD.ToLower();

                    entity.FinancialPaymentDetail.CreditCardTypeValueId = isCreditCard
                        ? paymentBag.CreditCardType?.GetEntityId<DefinedValue>( RockContext )
                        : null;

                    entity.FinancialPaymentDetail.NameOnCard = paymentBag.NameOnCard;
                } );

            // non-cash asset only applies to Non-Cash transactions.
            var nonCashCurrencyType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_NONCASH );
            if ( nonCashCurrencyType == null
                || entity.FinancialPaymentDetail.CurrencyTypeValueId != nonCashCurrencyType.Id )
            {
                entity.NonCashAssetTypeValueId = null;
            }

            // Transaction attributes.
            box.IfValidProperty( nameof( box.Bag.AttributeValues ),
                () =>
                {
                    entity.LoadAttributes( RockContext );
                    entity.SetPublicAttributeValues( box.Bag.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: false );
                } );

            box.IfValidProperty( nameof( box.Bag.TransactionDetails ),
                () =>
                {
                    SaveTransactionDetails( entity, box.Bag.TransactionDetails );
                } );

            if ( box.IsValidProperty( nameof( box.Bag.IsRefund ) )
                || box.IsValidProperty( nameof( box.Bag.RefundDetails ) ) )
            {
                ApplyRefundState( entity, box.Bag );
            }

            SaveTransactionImages( box.Bag, entity );

            return true;
        }

        private void SaveTransactionDetails( FinancialTransaction entity, TransactionDetailsBag detailsBag )
        {
            if ( detailsBag == null )
            {
                return;
            }

            var txnDetailService = new FinancialTransactionDetailService( RockContext );

            var dbDetails = txnDetailService.Queryable()
                .Where( d => d.TransactionId == entity.Id )
                .ToList();

            // Explicit deletes only.
            foreach ( var guid in detailsBag.RowsToDelete.Distinct() )
            {
                var dbDetail = dbDetails.FirstOrDefault( d => d.Guid == guid );

                if ( dbDetail != null && !dbDetail.EntityTypeId.HasValue )
                {
                    txnDetailService.Delete( dbDetail );
                }
            }

            // Add/update remaining rows.
            foreach ( var row in detailsBag.Rows.Where( r => r != null && !r.IsTotalRow ) )
            {
                var detail = dbDetails.FirstOrDefault( d => d.Guid == row.Guid );

                if ( detail == null )
                {
                    detail = new FinancialTransactionDetail
                    {
                        Guid = row.Guid != Guid.Empty ? row.Guid : Guid.NewGuid()
                    };

                    entity.TransactionDetails.Add( detail );
                    dbDetails.Add( detail );
                }

                detail.AccountId = row.Account?.GetEntityId<FinancialAccount>( RockContext ) ?? 0;
                detail.Amount = row.Amount + ( row.FeeCoverageAmount ?? 0 );
                detail.FeeAmount = row.FeeAmount;
                detail.FeeCoverageAmount = row.FeeCoverageAmount;
                detail.ForeignCurrencyAmount = row.ForeignCurrencyAmount;
                detail.Summary = row.Summary;

                if(row?.AttributeValues != null)
                {
                    detail.LoadAttributes( RockContext );
                    detail.SetPublicAttributeValues( row.AttributeValues, RequestContext.CurrentPerson, enforceSecurity: false );
                }
            }
        }


        private void SaveTransactionImages(TransactionBag bag, FinancialTransaction entity )
        {
            var images = bag.Images;
            if ( images.Any() )
            {
                foreach( var image in images)
                {
                    var binaryFileId = new BinaryFileService( RockContext ).GetId( image.Guid );

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

        private void ApplyRefundState( FinancialTransaction entity, TransactionBag bag )
        {
            var isRefund = bag.IsRefund;

            if ( isRefund )
            {
                if ( entity.RefundDetails == null )
                {
                    entity.RefundDetails = new FinancialTransactionRefund();
                }

                entity.RefundDetails.RefundReasonValueId =
                    bag.RefundDetails?.RefundReason?.GetEntityId<DefinedValue>( RockContext );

                entity.RefundDetails.RefundReasonSummary =
                    bag.RefundDetails?.RefundReasonSummary;
            }
            else if ( entity.RefundDetails != null )
            {
                new FinancialTransactionRefundService( RockContext ).Delete( entity.RefundDetails );
                entity.RefundDetails = null;
            }
        }

        private bool IsOrganizationCurrency( int? foreignCurrencyValueId )
        {
            // If no foreign currency is set, it's organization currency.
            if ( !foreignCurrencyValueId.HasValue )
            {
                return true;
            }

            // Get the organization's configured currency code (global attribute).
            var organizationCurrencyGuid = GlobalAttributesCache.Get()
                .GetValue( Rock.SystemKey.SystemSetting.ORGANIZATION_CURRENCY_CODE )
                .AsGuidOrNull();

            // If not configured, default to allowing.
            if ( !organizationCurrencyGuid.HasValue )
            {
                return true;
            }

            // Resolve the defined value for the organization currency.
            var organizationCurrencyDefinedValue = DefinedValueCache.Get( organizationCurrencyGuid.Value );

            if ( organizationCurrencyDefinedValue == null )
            {
                return true;
            }

            // Compare IDs
            return organizationCurrencyDefinedValue.Id == foreignCurrencyValueId.Value;
        }

        private decimal GetMaxRefundAmountForTransaction( FinancialTransaction transaction )
        {
            var hasTransactionCode = !string.IsNullOrWhiteSpace( transaction.TransactionCode );

            var otherAmounts = new FinancialTransactionDetailService( RockContext )
                .Queryable()
                .AsNoTracking()
                .Where( d =>
                    d.Transaction != null &&
                    (
                        (
                            transaction.FinancialGatewayId.HasValue &&
                            hasTransactionCode &&
                            d.Transaction.FinancialGatewayId.HasValue &&
                            d.Transaction.FinancialGatewayId.Value == transaction.FinancialGatewayId.Value &&
                            d.Transaction.TransactionCode == transaction.TransactionCode &&
                            d.TransactionId != transaction.Id
                        )
                        ||
                        (
                            d.Transaction.RefundDetails != null &&
                            d.Transaction.RefundDetails.OriginalTransactionId == transaction.Id
                        )
                    ) )
                .Select( d => d.Amount )
                .ToList()
                .Sum();

            return transaction.TotalAmount + otherAmounts;
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

                foreach ( var detail in entity.TransactionDetails )
                {
                    detail.SaveAttributeValues( RockContext );
                }
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

            var bag = GetEntityBagForView( entity );

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

        [BlockAction]
        public BlockActionResult GetTransactionDetailAttributes( string guid )
        {
            FinancialTransactionDetail detailEntity;

            if( guid.IsNotNullOrWhiteSpace())
            {
                detailEntity = new FinancialTransactionDetailService( RockContext ).Get( guid.AsGuid() );
            }
            else
            {
                detailEntity = new FinancialTransactionDetail();
            }

            if ( detailEntity == null )
            {
                return ActionBadRequest( $"{FinancialTransactionDetail.FriendlyTypeName} not found." );
            }

            detailEntity.LoadAttributes( RockContext );

            var bag = new TransactionDetailsBag();

            bag.LoadAttributesAndValuesForPublicEdit( detailEntity, RequestContext.CurrentPerson, enforceSecurity: false );

            return ActionOk( bag );
        }

        [BlockAction]
        public BlockActionResult GetMaxRefundAmount( string key )
        {
            var transaction = new FinancialTransactionService( RockContext )
                .GetQueryableByKey( key )
                .FirstOrDefault();

            if ( transaction == null )
            {
                return ActionBadRequest( $"{FinancialTransaction.FriendlyTypeName} not found." );
            }

            return ActionOk( GetMaxRefundAmountForTransaction( transaction ) );
        }


        [BlockAction]
        public BlockActionResult Refund( string key, RefundRequestBag bag )
        {
            if ( bag == null )
            {
                return ActionBadRequest( "Refund information is required." );
            }

            if ( bag.Amount <= 0 )
            {
                return ActionBadRequest( "Refund amount must be greater than zero." );
            }

            var transactionService = new FinancialTransactionService( RockContext );

            var transactionQry = transactionService.GetQueryableByKey(key);

            var transaction = transactionQry
                .Include( t => t.Batch )
                .Include( t => t.FinancialGateway )
                .Include( t => t.FinancialPaymentDetail )
                .Include( t => t.RefundDetails )
                .Include( t => t.ProcessedByPersonAlias.Person )
                .Include( t => t.AuthorizedPersonAlias.Person )
                .FirstOrDefault();

            var maximumRefundAmount = GetMaxRefundAmountForTransaction( transaction );
            if ( bag.Amount > maximumRefundAmount )
            {
                return ActionBadRequest( $"Refund amount cannot exceed {maximumRefundAmount.FormatAsCurrency()}." );
            }

            if ( transaction == null )
            {
                return ActionBadRequest( $"{FinancialTransaction.FriendlyTypeName} not found." );
            }

            if ( !transaction.IsAuthorized( Authorization.REFUND, RequestContext.CurrentPerson ) )
            {
                return ActionForbidden();
            }

            if ( transaction.RefundDetails != null )
            {
                return ActionBadRequest( "This transaction cannot be refunded." );
            }

            if ( !IsOrganizationCurrency( transaction.ForeignCurrencyCodeValueId ) )
            {
                return ActionBadRequest( "Refunds are not supported for transactions in foreign currencies." );
            }

            if ( transaction.Batch != null && ( transaction.Batch.Status == BatchStatus.Closed || transaction.Batch.IsAutomated ) )
            {
                return ActionBadRequest( "Refunds are not allowed for transactions in a closed or automated batch." );
            }

            var canProcess = !string.IsNullOrWhiteSpace( transaction.TransactionCode ) && transaction.FinancialGateway != null;
            var process = canProcess && bag.Process;

            var refundReasonValueId = bag.RefundReason?.GetEntityId<DefinedValue>( RockContext );

            var appendSuffix = GetAttributeValue( AttributeKey.AppendSuffixToBatchName ).AsBoolean();
            var batchNameSuffix = appendSuffix ? GetAttributeValue( AttributeKey.RefundBatchNameSuffix ) : string.Empty;

            string errorMessage;
            var refundTransaction = transactionService.ProcessRefund(
                transaction,
                bag.Amount,
                refundReasonValueId,
                bag.RefundReasonSummary,
                process,
                batchNameSuffix,
                out errorMessage );

            if ( refundTransaction == null )
            {
                return ActionBadRequest( errorMessage ?? "Unable to process refund." );
            }

            RockContext.SaveChanges();

            var updatedEntity = new FinancialTransactionService( RockContext )
                .Queryable()
                .Include( t => t.Batch )
                .Include( t => t.FinancialPaymentDetail )
                .Include( t => t.RefundDetails )
                .Include( t => t.ProcessedByPersonAlias.Person )
                .Include( t => t.AuthorizedPersonAlias.Person )
                .Include( t => t.FinancialGateway )
                .Include( t => t.TransactionDetails )
                .FirstOrDefault( t => t.Id == transaction.Id );


            return ActionOk( GetEntityBagForView( updatedEntity ) );
        }

        [BlockAction]
        public BlockActionResult GetBatchNavigation( string key )
        {
            var transactionId = RequestContext.PageParameters
                .GetValueOrNull( PageParameterKey.TransactionId )
                .AsInteger();

            var batchId = RequestContext.PageParameters
                .GetValueOrNull( PageParameterKey.BatchId )
                .AsInteger();

            if ( batchId == 0 )
            {
                return ActionOk( new BatchNavigationBag() );
            }

            var transactionIds = new FinancialTransactionService( RockContext )
                .Queryable()
                .Where( t => t.BatchId == batchId )
                .OrderBy( t => t.Id )
                .Select( t => t.Id )
                .ToList();

            var nextId = transactionIds.FirstOrDefault( id => id > transactionId );
            var prevId = transactionIds.LastOrDefault( id => id < transactionId );

            return ActionOk( new BatchNavigationBag
            {
                PreviousTransactionId = prevId == default( int ) ? ( int? ) null : prevId,
                NextTransactionId = nextId == default( int ) ? ( int? ) null : nextId
            } );
        }

        #endregion
    }
}
