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

using System.Collections.Generic;

namespace Rock.ViewModels.Blocks.Finance.ScheduledTransactionListLava
{
    /// <summary>
    /// The top-level bag returned by the Scheduled Transaction List Lava block.
    /// </summary>
    public class ScheduledTransactionListLavaBag
    {
        /// <summary>
        /// Gets or sets the list of scheduled-transaction items the current
        /// person owns (or is on the giving-group / business owner side of).
        /// Empty when the viewer is anonymous or has no active schedules.
        /// </summary>
        public List<ScheduledTransactionItemBag> Items { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to surface the empty-state
        /// message. False for anonymous viewers (those see a silent block,
        /// matching the original WebForms behavior).
        /// </summary>
        public bool IsEmpty { get; set; }

        /// <summary>
        /// Gets or sets the empty-state message, already formatted using the
        /// block's Transaction Label setting (e.g. "No gifts currently exist.").
        /// </summary>
        public string EmptyMessage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Add button should appear.
        /// True only when the Scheduled Transaction Entry Page attribute is set.
        /// </summary>
        public bool ShowAddButton { get; set; }

        /// <summary>
        /// Gets or sets the Add button label, formatted as "Create New {Transaction Label}".
        /// </summary>
        public string AddButtonText { get; set; }
    }
}
