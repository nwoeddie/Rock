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
using System.Security.Policy;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.TransactionDetail
{
    /// <summary>
    /// The additional configuration options for the Transaction Detail block.
    /// </summary>
    public class TransactionDetailOptionsBag
    {

        /// <summary>
        /// The transaction types that can be used to filter the results.
        /// </summary>
        public string TransactionTypesGuid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string TransactionSourceTypesGuid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string RefundReasonTypesGuid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string CurrencyTypesGuid {  get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string CurrencyCodesGuid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string CreditCardTypesGuid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string AssetTypes { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool BatchEditAllowed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool ShowForeignCurrencyFields { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool TransactionSourceRequired { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string BatchDetailPageUrl { get; set; }
    }
}
