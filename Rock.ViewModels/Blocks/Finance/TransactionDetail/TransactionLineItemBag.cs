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
    /// 
    /// </summary>
    public class TransactionLineItemBag : EntityBagBase
    {
        /// <summary>
        /// 
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ListItemBag Account { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public decimal? FeeAmount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public decimal? FeeCoverageAmount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public decimal? ForeignCurrencyAmount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int TransactionId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int? EntityId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int? EntityTypeId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ListItemBag EntityType { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsTotalRow { get; set; }
    }
}