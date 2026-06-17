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

namespace Rock.ViewModels.Blocks.Finance.ScheduledTransactionListLava
{
    /// <summary>
    /// One row in the Scheduled Transaction List Lava block. After a successful
    /// or failed Cancel, the server returns a replacement bag whose RowHtml is
    /// an alert fragment (no action buttons), so the row keeps its place in
    /// the layout while showing the result.
    /// </summary>
    public class ScheduledTransactionItemBag
    {
        /// <summary>
        /// Gets or sets the hashed identifier of the scheduled transaction.
        /// Used as the v-for key and as the argument to the Cancel block action.
        /// </summary>
        public string IdKey { get; set; }

        /// <summary>
        /// Gets or sets the inner HTML of the row's .scheduledtransaction-item
        /// element. Contains the Lava-rendered body followed by the .actions div
        /// with the Edit / Delete buttons (or, after a Cancel, an alert div).
        /// The element-level wrapper is supplied by the Vue v-for, so this string
        /// is placed directly inside .scheduledtransaction-item to match the
        /// WebForms DOM tree exactly.
        /// </summary>
        public string RowHtml { get; set; }
    }
}
