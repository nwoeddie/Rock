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
using System.Net;
using System.Text;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Financial;
using Rock.Model;
using Rock.Utility;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Finance.ScheduledTransactionListLava;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace Rock.Blocks.Finance
{
    /// <summary>
    /// Lists the current person's (and their related businesses') active
    /// scheduled-transaction records. Each row's body is produced by a
    /// configurable Lava template. Edit/Transfer and Delete actions wrap the
    /// Lava body in the .obs component.
    /// </summary>
    [DisplayName( "Scheduled Transaction List Lava" )]
    [Category( "Finance" )]
    [Description( "Block that shows a list of scheduled transactions for the currently logged in user with the ability to modify the formatting using a lava template." )]

    [CodeEditorField( "Template",
        Key = AttributeKey.Template,
        Description = "Lava template for the display of the scheduled transactions.",
        EditorMode = CodeEditorMode.Lava,
        EditorHeight = 400,
        IsRequired = true,
        DefaultValue = @"{% include '~~/Assets/Lava/ScheduledTransactionListLiquid.lava' %}",
        Order = 1 )]

    [LinkedPage(
        "Scheduled Transaction Entry Page",
        Key = AttributeKey.ScheduledTransactionEntryPage,
        Description = "Link to use when adding new transactions.",
        IsRequired = false,
        Order = 4 )]

    [TextField( "Transaction Label",
        Key = AttributeKey.TransactionLabel,
        Description = "The label to use to describe the transaction (e.g. 'Gift', 'Donation', etc.)",
        IsRequired = true,
        DefaultValue = "Gift",
        Order = 5 )]

    [FinancialGatewayField(
        "Gateway Filter",
        Key = AttributeKey.GatewayFilter,
        Description = "When set, causes only scheduled transaction's of a particular gateway to be shown.",
        IsRequired = false,
        Order = 6 )]

    [FinancialGatewayField(
        "Transfer-To Gateway",
        Key = AttributeKey.TransferToGateway,
        Description = "Set this if you want people to transfer their existing scheduled transactions to this new gateway. When set, the Edit button becomes 'Transfer' (default or whatever is set in the 'Transfer Button Text' setting) if the scheduled transaction's gateway does not match the transfer-to gateway.",
        IsRequired = false,
        Order = 7 )]

    [TextField(
        "Transfer Button Text",
        Key = AttributeKey.TransferButtonText,
        Description = "The text to use on the transfer (edit) button which is used when a Transfer-To gateway is set.",
        IsRequired = true,
        DefaultValue = "Transfer",
        Order = 8 )]

    [LinkedPage(
        "Scheduled Transaction Edit Page",
        Key = AttributeKey.ScheduledTransactionEditPage,
        Description = "The page used to update an existing scheduled transaction for Gateways that don't support a hosted payment interface.",
        Order = 9 )]

    [LinkedPage(
        "Scheduled Transaction Edit Page for Hosted Gateways",
        Key = AttributeKey.ScheduledTransactionEditPageHosted,
        Description = "The page used to update an existing scheduled transaction for Gateways that support a hosted payment interface.",
        IsRequired = false,
        Order = 10 )]

    [DefinedValueField(
        "Transaction Types",
        Key = AttributeKey.TransactionTypes,
        Description = "This setting filters the list of transactions by transaction type.",
        IsRequired = false,
        DefinedTypeGuid = Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE,
        DefaultValue = Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION,
        AllowMultiple = true,
        Order = 11 )]

    [Rock.SystemGuid.EntityTypeGuid( "769FDD5D-73D2-4C63-BBA3-050BA1E1CD3E" )]
// ⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️
// TODO: ⚠️⚠️⚠️ Nick, remember to delete the old, ScheduledTransactionListLiquid.ascx block after the A-B test is complete and update this guid to the old block's guid so existing block instances are automatically chopped on start.
// ⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️
    [Rock.SystemGuid.BlockTypeGuid( "A51CA42A-80E0-46B7-BD61-5B33F050856A" )] // for now during side-by-side A-B testing
    // will become [Rock.SystemGuid.BlockTypeGuid( "081FF29F-0A9F-4EC3-95AD-708FA0E6132D" )]
    public class ScheduledTransactionListLava : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string Template = "Template";
            public const string ScheduledTransactionEditPage = "ScheduledTransactionEditPage";
            public const string ScheduledTransactionEditPageHosted = "ScheduledTransactionEditPageHosted";
            public const string ScheduledTransactionEntryPage = "ScheduledTransactionEntryPage";
            public const string TransactionLabel = "TransactionLabel";
            public const string GatewayFilter = "GatewayFilter";
            public const string TransferToGateway = "TransferToGateway";
            public const string TransferButtonText = "TransferButtonText";
            public const string TransactionTypes = "TransactionTypes";
        }

        private static class PageParameterKey
        {
            public const string ScheduledTransactionGuid = "ScheduledTransactionGuid";
            public const string Transfer = "transfer";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new CustomBlockBox<ScheduledTransactionListLavaBag, ScheduledTransactionListLavaOptionsBag>
            {
                Bag = BuildBag()
            };

            return box;
        }

        /// <summary>
        /// Builds the top-level bag with the per-row item list, empty-state
        /// flag, and Add-button visibility. Returns an empty bag for anonymous
        /// viewers so the block renders nothing (matching the original behavior).
        /// </summary>
        private ScheduledTransactionListLavaBag BuildBag()
        {
            var transactionLabel = GetAttributeValue( AttributeKey.TransactionLabel );
            var entryPageConfigured = GetAttributeValue( AttributeKey.ScheduledTransactionEntryPage ).IsNotNullOrWhiteSpace();

            var bag = new ScheduledTransactionListLavaBag
            {
                Items = new List<ScheduledTransactionItemBag>(),
                AddButtonText = $"Create New {transactionLabel}",
                ShowAddButton = entryPageConfigured
            };

            if ( RequestContext.CurrentPerson == null )
            {
                // Anonymous viewers see nothing: no items, no empty-state alert,
                // no Add button. Matches the original WebForms behavior.
                bag.ShowAddButton = false;
                return bag;
            }

            var schedules = LoadSchedulesForCurrentPerson();

            if ( schedules.Count == 0 )
            {
                bag.IsEmpty = true;
                bag.EmptyMessage = $"No {transactionLabel.Pluralize().ToLower()} currently exist.";
                return bag;
            }

            var lastPaymentDates = GetLastPaymentDates( schedules.Select( s => s.Id ) );

            var commonMergeFields = RequestContext.GetCommonMergeFields();
            var template = GetAttributeValue( AttributeKey.Template );
            var transferToGatewayGuid = GetAttributeValue( AttributeKey.TransferToGateway ).AsGuidOrNull();
            var eventRegistrationTransactionTypeValueId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_EVENT_REGISTRATION.AsGuid() );

            foreach ( var transactionSchedule in schedules )
            {
                bag.Items.Add( BuildItemBag(
                    transactionSchedule,
                    template,
                    commonMergeFields,
                    lastPaymentDates,
                    transferToGatewayGuid,
                    eventRegistrationTransactionTypeValueId ) );
            }

            return bag;
        }

        /// <summary>
        /// Loads the active scheduled transactions belonging to the current
        /// person (and any businesses they own), applying the optional gateway
        /// and transaction-type filters. Calls GetStatus to refresh gateway
        /// state for each schedule before returning.
        /// </summary>
        private List<FinancialScheduledTransaction> LoadSchedulesForCurrentPerson()
        {
            var transactionService = new FinancialScheduledTransactionService( RockContext );
            var personService = new PersonService( RockContext );

            // Collect the giving IDs we want to match: the current person's
            // own giving ID plus the giving ID of every business they own.
            // This matches the WebForms behavior so historical schedules
            // attached to either form (G{n} or P{n}) are picked up.
            var givingIds = personService.GetBusinesses( RequestContext.CurrentPerson.Id )
                .Select( g => g.GivingId )
                .ToList();
            givingIds.Add( RequestContext.CurrentPerson.GivingId );

            var schedules = transactionService.Queryable()
                .Include( a => a.TransactionTypeValue )
                .Include( a => a.TransactionFrequencyValue )
                .Include( a => a.FinancialGateway )
                .Include( a => a.FinancialPaymentDetail.CurrencyTypeValue )
                .Include( a => a.FinancialPaymentDetail.CreditCardTypeValue )
                .Include( a => a.AuthorizedPersonAlias.Person )
                .Include( a => a.ScheduledTransactionDetails.Select( s => s.Account ) )
                .Where( s => givingIds.Contains( s.AuthorizedPersonAlias.Person.GivingId ) && s.IsActive == true );

            var gatewayFilterGuid = GetAttributeValue( AttributeKey.GatewayFilter ).AsGuidOrNull();
            if ( gatewayFilterGuid.HasValue )
            {
                schedules = schedules.Where( s => s.FinancialGateway.Guid == gatewayFilterGuid.Value );
            }

            // Transaction-type filter intentionally includes schedules whose
            // TransactionTypeValueId is null so legacy rows keep rendering.
            var transactionTypeGuids = GetTransactionTypesFilter();
            schedules = schedules.Where( s => !s.TransactionTypeValueId.HasValue || transactionTypeGuids.Contains( s.TransactionTypeValue.Guid ) );

            var scheduleList = schedules.ToList();

            // Refresh gateway status (NextPaymentDate / IsActive) for each
            // active schedule. Hits the gateway once per row; preserved from
            // the original block to keep the card-expired / status badges
            // current with what the gateway shows. Mutations are applied to
            // the materialized list above before any rows are rendered.
            transactionService.GetStatus( scheduleList, true );

            return scheduleList;
        }

        /// <summary>
        /// Computes the most-recent transaction date for each scheduled
        /// transaction in a single batched query rather than the per-row
        /// lazy load the original block did.
        /// </summary>
        private Dictionary<int, DateTime?> GetLastPaymentDates( IEnumerable<int> scheduleIds )
        {
            var ids = scheduleIds.ToList();

            if ( ids.Count == 0 )
            {
                return new Dictionary<int, DateTime?>();
            }

            return new FinancialTransactionService( RockContext ).Queryable()
                .Where( t => t.ScheduledTransactionId.HasValue
                    && ids.Contains( t.ScheduledTransactionId.Value )
                    && t.TransactionDateTime.HasValue )
                .GroupBy( t => t.ScheduledTransactionId.Value )
                .Select( g => new
                {
                    ScheduledTransactionId = g.Key,
                    LastPaymentDate = g.Max( t => t.TransactionDateTime )
                } )
                .ToDictionary( x => x.ScheduledTransactionId, x => x.LastPaymentDate );
        }

        /// <summary>
        /// Returns the configured transaction-type defined-value guids,
        /// filtered to those still present in the DefinedValueCache.
        /// </summary>
        private List<Guid> GetTransactionTypesFilter()
        {
            return this.GetAttributeValues( AttributeKey.TransactionTypes )
                .AsGuidList()
                .Where( guid => DefinedValueCache.Get( guid ) != null )
                .ToList();
        }

        /// <summary>
        /// Builds the per-row bag. RowHtml is the full inner HTML of the
        /// .scheduledtransaction-item element: the Lava-rendered body followed
        /// by the .actions div. The whole row is server-rendered so the Vue
        /// side can v-html it directly onto the .scheduledtransaction-item
        /// without introducing an extra wrapper element (which would break
        /// theme CSS selectors targeting the WebForms DOM tree).
        /// </summary>
        private ScheduledTransactionItemBag BuildItemBag(
            FinancialScheduledTransaction transactionSchedule,
            string template,
            IDictionary<string, object> commonMergeFields,
            Dictionary<int, DateTime?> lastPaymentDates,
            Guid? transferToGatewayGuid,
            int? eventRegistrationTransactionTypeValueId )
        {
            // Determine whether this row should route through the Transfer
            // flow (i.e., the configured transfer-to gateway differs from
            // the schedule's current gateway).
            var isTransfer = transferToGatewayGuid.HasValue
                && transactionSchedule.FinancialGateway != null
                && transactionSchedule.FinancialGateway.Guid != transferToGatewayGuid.Value;

            // Whether the schedule's gateway supports a hosted edit page.
            var hostedGatewayComponent = transactionSchedule.FinancialGateway?.GetGatewayComponent() as IHostedGatewayComponent;
            var useHostedGatewayEditPage = hostedGatewayComponent != null
                && hostedGatewayComponent.GetSupportedHostedGatewayModes( transactionSchedule.FinancialGateway ).Contains( HostedGatewayMode.Hosted );

            // Event-registration schedules are read-only from this block.
            var isEventRegistration = eventRegistrationTransactionTypeValueId.HasValue
                && eventRegistrationTransactionTypeValueId == transactionSchedule.TransactionTypeValueId;

            bool showEditButton;
            if ( isEventRegistration )
            {
                showEditButton = false;
            }
            else if ( useHostedGatewayEditPage )
            {
                showEditButton = GetAttributeValue( AttributeKey.ScheduledTransactionEditPageHosted ).IsNotNullOrWhiteSpace();
            }
            else
            {
                showEditButton = GetAttributeValue( AttributeKey.ScheduledTransactionEditPage ).IsNotNullOrWhiteSpace();
            }

            var editButtonText = isTransfer
                ? GetAttributeValue( AttributeKey.TransferButtonText )
                : "Edit";

            var editUrl = ResolveEditUrl( transactionSchedule, isTransfer, useHostedGatewayEditPage );

            var lavaContent = RenderRowLava(
                transactionSchedule,
                template,
                commonMergeFields,
                lastPaymentDates,
                useHostedGatewayEditPage );

            var actionsHtml = BuildActionsHtml( transactionSchedule.IdKey, showEditButton, editButtonText, editUrl, showDeleteButton: true );

            return new ScheduledTransactionItemBag
            {
                IdKey = transactionSchedule.IdKey,
                RowHtml = lavaContent + actionsHtml
            };
        }

        /// <summary>
        /// Builds the .actions div with Edit and Delete buttons, matching the
        /// WebForms-emitted markup: input[type=submit].btn.btn-default.edit and
        /// a.btn.btn-danger.delete. Data attributes carry the edit URL and the
        /// row's IdKey so the .obs side can dispatch via event delegation
        /// without re-implementing the routing logic.
        /// </summary>
        private string BuildActionsHtml( string idKey, bool showEditButton, string editButtonText, string editUrl, bool showDeleteButton )
        {
            if ( !showEditButton && !showDeleteButton )
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append( "<div class=\"actions\">" );

            if ( showEditButton )
            {
                sb.Append( "<input type=\"submit\" class=\"btn btn-default edit\" value=\"" );
                sb.Append( WebUtility.HtmlEncode( editButtonText ?? "Edit" ) );
                sb.Append( "\" accesskey=\"m\" data-shortcut-key=\"e\" title=\"Alt+e\" data-edit-url=\"" );
                sb.Append( WebUtility.HtmlEncode( editUrl ?? string.Empty ) );
                sb.Append( "\" />" );
            }

            if ( showDeleteButton )
            {
                // Literal space between Edit and Delete preserves the small
                // inline-block gap the WebForms markup gets from indentation
                // whitespace; without it, the two buttons render flush.
                if ( showEditButton )
                {
                    sb.Append( ' ' );
                }

                sb.Append( "<a href=\"#\" class=\"btn btn-danger delete\" data-id-key=\"" );
                sb.Append( WebUtility.HtmlEncode( idKey ?? string.Empty ) );
                sb.Append( "\">Delete</a>" );
            }

            sb.Append( "</div>" );
            return sb.ToString();
        }

        /// <summary>
        /// Builds the ScheduledTransaction.* merge-field dictionary for one
        /// schedule and resolves the configured Lava template against it.
        /// Merge-field names match the original block verbatim so churches
        /// can keep using their customized templates without changes.
        /// </summary>
        private string RenderRowLava(
            FinancialScheduledTransaction transactionSchedule,
            string template,
            IDictionary<string, object> commonMergeFields,
            Dictionary<int, DateTime?> lastPaymentDates,
            bool useHostedGatewayEditPage )
        {
            var scheduleSummary = new Dictionary<string, object>
            {
                ["Id"] = transactionSchedule.Id,
                ["Guid"] = transactionSchedule.Guid,
                ["StartDate"] = transactionSchedule.StartDate,
                ["EndDate"] = transactionSchedule.EndDate,
                ["NextPaymentDate"] = transactionSchedule.NextPaymentDate
            };

            if ( transactionSchedule.NextPaymentDate.HasValue )
            {
                scheduleSummary["DaysTillNextPayment"] = ( transactionSchedule.NextPaymentDate.Value - RockDateTime.Now ).Days;
            }
            else
            {
                scheduleSummary["DaysTillNextPayment"] = null;
            }

            lastPaymentDates.TryGetValue( transactionSchedule.Id, out var lastPaymentDate );
            scheduleSummary["LastPaymentDate"] = lastPaymentDate;

            if ( lastPaymentDate.HasValue )
            {
                scheduleSummary["DaysSinceLastPayment"] = ( RockDateTime.Now - lastPaymentDate.Value ).Days;
            }
            else
            {
                scheduleSummary["DaysSinceLastPayment"] = null;
            }

            scheduleSummary["PersonName"] = transactionSchedule.AuthorizedPersonAlias?.Person?.FullName ?? string.Empty;
            scheduleSummary["CurrencyType"] = transactionSchedule.FinancialPaymentDetail?.CurrencyTypeValue?.Value ?? string.Empty;
            scheduleSummary["CreditCardType"] = transactionSchedule.FinancialPaymentDetail?.CreditCardTypeValue?.Value ?? string.Empty;
            scheduleSummary["AccountNumberMasked"] = transactionSchedule.FinancialPaymentDetail?.AccountNumberMasked ?? string.Empty;
            scheduleSummary["UrlEncryptedKey"] = transactionSchedule.UrlEncodedKey;
            scheduleSummary["Frequency"] = transactionSchedule.TransactionFrequencyValue?.Value;
            scheduleSummary["FrequencyDescription"] = transactionSchedule.TransactionFrequencyValue?.Description;
            scheduleSummary["Status"] = transactionSchedule.Status;
            scheduleSummary["CardExpirationDate"] = transactionSchedule.FinancialPaymentDetail?.ExpirationDate;
            scheduleSummary["CardIsExpired"] = transactionSchedule.FinancialPaymentDetail?.CardExpirationDate < RockDateTime.Now;
            scheduleSummary["TransactionType"] = transactionSchedule.TransactionTypeValue?.Value;

            var summaryDetails = new List<Dictionary<string, object>>();
            decimal totalAmount = 0m;

            foreach ( var detail in transactionSchedule.ScheduledTransactionDetails )
            {
                summaryDetails.Add( new Dictionary<string, object>
                {
                    ["AccountId"] = detail.Account.Id,
                    ["AccountName"] = detail.Account.Name,
                    ["Amount"] = detail.Amount,
                    ["Summary"] = detail.Summary
                } );

                totalAmount += detail.Amount;
            }

            scheduleSummary["ScheduledAmount"] = totalAmount;
            scheduleSummary["TransactionDetails"] = summaryDetails;
            scheduleSummary["IsHostedGateway"] = useHostedGatewayEditPage;
            scheduleSummary["EditPage"] = useHostedGatewayEditPage
                ? GetLinkedPageRoute( AttributeKey.ScheduledTransactionEditPageHosted )
                : GetLinkedPageRoute( AttributeKey.ScheduledTransactionEditPage );

            // Clone the common merge fields so the per-row addition does not
            // leak ScheduledTransaction state between rows.
            var mergeFields = new Dictionary<string, object>( commonMergeFields )
            {
                ["ScheduledTransaction"] = scheduleSummary
            };

            return template.ResolveMergeFields( mergeFields );
        }

        /// <summary>
        /// Resolves the route portion of a linked-page attribute (without query
        /// string), suitable for surfacing inside the Lava merge fields.
        /// Returns null when the attribute is not configured.
        /// </summary>
        private string GetLinkedPageRoute( string attributeKey )
        {
            var pageReference = new Rock.Web.PageReference( GetAttributeValue( attributeKey ) );
            return pageReference.PageId > 0 ? pageReference.BuildUrl() : null;
        }

        /// <summary>
        /// Resolves the Edit-button URL for a row, applying the same routing
        /// rules the original block used at click time: a Transfer routes to
        /// the entry page with transfer=true; otherwise hosted-gateway support
        /// picks between the hosted and classic edit pages.
        /// </summary>
        private string ResolveEditUrl( FinancialScheduledTransaction transactionSchedule, bool isTransfer, bool useHostedGatewayEditPage )
        {
            var queryParams = new Dictionary<string, string>
            {
                [PageParameterKey.ScheduledTransactionGuid] = transactionSchedule.Guid.ToString()
            };

            if ( isTransfer && GetAttributeValue( AttributeKey.ScheduledTransactionEntryPage ).IsNotNullOrWhiteSpace() )
            {
                queryParams[PageParameterKey.Transfer] = "true";
                return this.GetLinkedPageUrl( AttributeKey.ScheduledTransactionEntryPage, queryParams );
            }

            return useHostedGatewayEditPage
                ? this.GetLinkedPageUrl( AttributeKey.ScheduledTransactionEditPageHosted, queryParams )
                : this.GetLinkedPageUrl( AttributeKey.ScheduledTransactionEditPage, queryParams );
        }

        #endregion Methods

        #region Block Actions

        /// <summary>
        /// Inactivates the specified scheduled transaction at the gateway and
        /// returns a replacement bag for the affected row.
        /// </summary>
        /// <param name="idKey">The hashed identifier of the scheduled transaction to cancel.</param>
        /// <returns>A replacement <see cref="ScheduledTransactionItemBag"/> whose RowHtml has been swapped for a success or error alert.</returns>
        [BlockAction]
        public BlockActionResult CancelScheduledTransaction( string idKey )
        {
            /*
                 8/27/2021 - MDP

                 (Carried forward from the WebForms block)

                 We really don't want to actually delete a FinancialScheduledTransaction.
                 Just inactivate it, even if there aren't FinancialTransactions associated with it.
                 It is possible that the Gateway has processed a transaction on it that Rock doesn't
                 know about yet. If that happens, Rock won't be able to match a record for that
                 downloaded transaction! We also might want to match inactive or "deleted" schedules
                 on the Gateway to a person in Rock, so we'll need the ScheduledTransaction to do that.

                 Reason: Preserve gateway/transaction reconciliation even when the person
                         cancels their schedule.
            */

            var scheduledTransactionId = IdHasher.Instance.GetId( idKey ) ?? 0;

            var fstService = new FinancialScheduledTransactionService( RockContext );
            var currentTransaction = fstService.Get( scheduledTransactionId );
            if ( currentTransaction != null && currentTransaction.FinancialGateway != null )
            {
                currentTransaction.FinancialGateway.LoadAttributes( RockContext );
            }

            var transactionLabel = GetAttributeValue( AttributeKey.TransactionLabel );

            string errorMessage = string.Empty;
            var replacement = new ScheduledTransactionItemBag
            {
                IdKey = idKey
            };

            if ( fstService.Cancel( currentTransaction, out errorMessage ) )
            {
                try
                {
                    fstService.GetStatus( currentTransaction, out errorMessage );
                }
                catch
                {
                    // Ignore
                }

                RockContext.SaveChanges();

                replacement.RowHtml = $"<div class='alert alert-success'>Your scheduled {WebUtility.HtmlEncode( transactionLabel.ToLower() )} has been deleted.</div>";
            }
            else
            {
                replacement.RowHtml = $"<div class='alert alert-danger'>An error occurred while deleting your scheduled transaction. Message: {WebUtility.HtmlEncode( errorMessage ?? string.Empty )}</div>";
            }

            return ActionOk( replacement );
        }

        #endregion Block Actions
    }
}
