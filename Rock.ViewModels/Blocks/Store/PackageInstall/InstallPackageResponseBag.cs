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

namespace Rock.ViewModels.Blocks.Store.PackageInstall
{
    /// <summary>
    /// The outcome of a package purchase and install attempt.
    /// </summary>
    public class InstallPackageResponseBag
    {
        /// <summary>
        /// Gets or sets a value indicating whether the package was installed
        /// successfully. When <c>true</c> the login form is hidden and the
        /// post-install instructions are shown.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the HTML message describing the result. For a failed
        /// attempt this is the reason the purchase or install did not complete
        /// (may contain help links).
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the alert style token for the result message, matching
        /// the Obsidian AlertType values (e.g. "warning", "danger", "success").
        /// </summary>
        public string MessageAlertType { get; set; }

        /// <summary>
        /// Gets or sets the post-install instructions supplied by the package,
        /// shown when <see cref="IsSuccess"/> is <c>true</c>.
        /// </summary>
        public string PostInstallInstructions { get; set; }
    }
}
