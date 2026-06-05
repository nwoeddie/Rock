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

namespace Rock.ViewModels.Blocks.Store.LinkOrganization
{
    /// <summary>
    /// The response returned after saving the organization's average weekly
    /// attendance to the store.
    /// </summary>
    public class SaveAttendanceResponseBag
    {
        /// <summary>
        /// Gets or sets a value indicating whether the save was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the name of the configured organization.
        /// </summary>
        public string OrganizationName { get; set; }

        /// <summary>
        /// Gets or sets an error message to display when the save failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
