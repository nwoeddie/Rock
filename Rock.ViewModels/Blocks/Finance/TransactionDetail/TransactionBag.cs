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

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.TransactionDetail
{
    /// <summary>
    /// The main transaction data for the Transaction Detail block.
    /// </summary>
    public class TransactionBag : EntityBagBase
    {
        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the authorized person alias.
        /// </summary>
        public AuthorizedPersonBag AuthorizedPerson { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Boolean ShowAsAnonymous { get; set; }

        /// <summary>
        /// Gets or sets the authorized person alias identifier.
        /// </summary>
        public int? AuthorizedPersonAliasId { get; set; }

        /// <summary>
        /// Gets or sets the transaction source type.
        /// </summary>
        public ListItemBag SourceType { get; set; }

        /// <summary>
        /// Gets or sets the financial batch containing this transaction.
        /// </summary>
        public ListItemBag Batch { get; set; }

        /// <summary>
        /// Gets or sets the batch identifier.
        /// </summary>
        public int? BatchId { get; set; }

        /// <summary>
        /// Gets or sets the financial gateway.
        /// </summary>
        public ListItemBag FinancialGateway { get; set; }

        /// <summary>
        /// Gets or sets the payment detail information.
        /// </summary>
        public PaymentDetailBag PaymentDetail { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ListItemBag CurrencyCode { get; set; }
        /// <summary>
        /// Gets or sets the foreign currency display text.
        /// </summary>
        public string ForeignCurrencyDisplay { get; set; }

        /// <summary>
        /// Gets or sets the foreign currency symbol.
        /// </summary>
        public string ForeignCurrencySymbol { get; set; }

        /// <summary>
        /// Gets or sets the images associated with this transaction.
        /// </summary>
        public List<TransactionImageBag> Images { get; set; }

        /// <summary>
        /// Gets or sets the non-cash asset type.
        /// </summary>
        public ListItemBag NonCashAssetType { get; set; }

        /// <summary>
        /// Gets or sets the person alias who processed the transaction.
        /// </summary>
        public ListItemBag ProcessedByPersonAlias { get; set; }


        /// <summary>
        /// Gets or sets the scheduled transaction that initiated this transaction.
        /// </summary>
        public ScheduledTransactionBag ScheduledTransaction { get; set; }

        /// <summary>
        /// Gets or sets the scheduled transaction identifier.
        /// </summary>
        public int? ScheduledTransactionId { get; set; }

        /// <summary>
        /// Gets or sets the status of the transaction.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Gets or sets the transaction summary/comments.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the transaction code.
        /// </summary>
        public string TransactionCode { get; set; }

        /// <summary>
        /// Gets or sets the date/time the transaction occurred.
        /// </summary>
        public DateTime? TransactionDateTime { get; set; }

        /// <summary>
        /// Gets or sets the transaction detail line items.
        /// </summary>
        public TransactionDetailsBag TransactionDetails { get; set; }

        /// <summary>
        /// Gets or sets the transaction type.
        /// </summary>
        public ListItemBag TransactionType { get; set; }

        /// <summary>
        /// Gets or sets the total amount of the transaction.
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Gets or sets the total fee amount.
        /// </summary>
        public decimal? TotalFeeAmount { get; set; }

        /// <summary>
        /// Gets or sets the total fee coverage amount.
        /// </summary>
        public decimal? TotalFeeCoverageAmount { get; set; }

        /// <summary>
        /// Gets or sets the total foreign currency amount.
        /// </summary>
        public decimal? TotalForeignCurrencyAmount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<string> Updates { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsRefund { get; set; }

        /// <summary>
        /// Gets or sets the refund details if this is a refund transaction.
        /// </summary>
        public RefundDetailBag RefundDetails { get; set; }

        /// <summary>
        /// Gets refund info if any part of transaction has been refunded
        /// </summary>
        public List<RefundTransactionBag> Refunds { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<RelatedTransactionBag> RelatedTransactions { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool CanRefund { get; set; }
    }
}