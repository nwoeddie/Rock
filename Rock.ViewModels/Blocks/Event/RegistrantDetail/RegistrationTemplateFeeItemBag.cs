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

namespace Rock.ViewModels.Blocks.Event.RegistrantDetail
{
    /// <summary>
    /// Represents a single selectable item within a registration template fee,
    /// sent to the client to render fee controls.
    /// </summary>
    public class RegistrationTemplateFeeItemBag
    {
        /// <summary>
        /// Gets or sets the fee item identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of this fee item.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the cost per unit of this fee item.
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of times this fee item may be selected.
        /// Null means no limit.
        /// </summary>
        public int? MaximumUsageCount { get; set; }

        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        public int Order { get; set; }
    }
}
