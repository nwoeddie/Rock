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

namespace Rock.ViewModels.Blocks.Event.RegistrantDetail
{
    /// <summary>
    /// The item details for the Registrant Detail block.
    /// </summary>
    public class RegistrantBag : EntityBagBase
    {
        // ── Editable Fields ──────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the person alias associated with this registrant.
        /// </summary>
        public ListItemBag PersonAlias { get; set; }

        /// <summary>
        /// Gets or sets the individual cost override for this registrant.
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the registration's discount code applies to this registrant.
        /// </summary>
        public bool DiscountApplies { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this registrant is on the wait list.
        /// </summary>
        public bool IsOnWaitList { get; set; }

        /// <summary>
        /// Gets or sets the current fee selections for this registrant.
        /// </summary>
        public List<RegistrantFeeBag> Fees { get; set; }

        /// <summary>
        /// Gets or sets the Guid of the binary file for a newly uploaded signature document.
        /// </summary>
        public Guid? SignatureDocumentBinaryFileId { get; set; }

        // ── Context / Display ────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the id key of the registration this registrant belongs to.
        /// Required when creating a new registrant.
        /// </summary>
        public string RegistrationIdKey { get; set; }

        /// <summary>
        /// Gets or sets the registration template name for the wizard breadcrumb.
        /// </summary>
        public string RegistrationTemplateName { get; set; }

        /// <summary>
        /// Gets or sets the registration instance name for the wizard breadcrumb.
        /// </summary>
        public string RegistrationInstanceName { get; set; }

        /// <summary>
        /// Gets or sets the registration name for the wizard breadcrumb.
        /// </summary>
        public string RegistrationName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the wait list toggle should be visible.
        /// </summary>
        public bool IsWaitListEnabled { get; set; }

        /// <summary>
        /// Gets or sets the name of the required signature document template, used as the file uploader label.
        /// </summary>
        public string SignatureDocumentTemplateName { get; set; }

        /// <summary>
        /// Gets or sets the binary file type guid for the signature document template, used to restrict uploads.
        /// </summary>
        public Guid? SignatureDocumentTemplateBinaryFileTypeGuid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an existing valid signature document was found for this person.
        /// When true, a notification is shown informing the editor.
        /// </summary>
        public bool HasExistingSignatureDocument { get; set; }
    }
}
