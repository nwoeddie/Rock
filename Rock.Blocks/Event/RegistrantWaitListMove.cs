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
using System.ComponentModel;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.ViewModels.Blocks.Event.RegistrantWaitListMove;
using Rock.Web.Cache;

namespace Rock.Blocks.Event
{
    /// <summary>
    /// Moves registrants from the wait list to active status for a registration
    /// instance and optionally sends them a confirmation email.
    /// </summary>
    [DisplayName( "Registrant Wait List Move" )]
    [Category( "Event" )]
    [Description( "Moves the person from the wait list to be a full registrant." )]
    [IconCssClass( "fa fa-clipboard-check" )]
    [SupportedSiteTypes( Model.SiteType.Web )]
    [Rock.SystemGuid.EntityTypeGuid( "7700303a-45e2-4fbf-9c10-c7f3726f529e" )]
    [Rock.SystemGuid.BlockTypeGuid( "7733cb25-df4d-4ffe-a898-ff6a03858799" )]
    public class RegistrantWaitListMove : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string RegistrationInstancePage = "RegistrationInstancePage";
        }

        private static class PageParameterKey
        {
            public const string EntitySetId = "EntitySetId";
        }

        private static class MergeFieldKey
        {
            public const string RegistrationInstance = "RegistrationInstance";
            public const string Registration = "Registration";
            public const string TransitionedRegistrants = "TransitionedRegistrants";
            public const string AdditionalFieldsNeeded = "AdditionalFieldsNeeded";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var bag = new RegistrantWaitListMoveBag();

            return bag;
        }

        /// <summary>
        /// Loads the list of <see cref="RegistrationRegistrant"/> records from the
        /// entity set identified by the <see cref="PageParameterKey.EntitySetId"/> page parameter.
        /// Returns an empty list when the parameter is missing or the set cannot be found.
        /// </summary>
        private List<RegistrationRegistrant> GetRegistrantsFromEntitySet()
        {
            return null;
        }

        /// <summary>
        /// Moves each registrant in <paramref name="registrants"/> from wait-list
        /// status to active, adds them to any group associated with the registration
        /// instance, and updates <c>LastPaymentReminderDateTime</c> on the parent
        /// <see cref="Registration"/>. Persists all changes via <see cref="RockContext"/>.
        /// </summary>
        private void MoveRegistrantsOffWaitList( List<RegistrationRegistrant> registrants )
        {
        }

        /// <summary>
        /// Adds <paramref name="registrant"/> to the group linked to the registration
        /// instance, using the appropriate group role and member status from the template.
        /// A no-op when no group is configured or the person is already a member.
        /// </summary>
        private void AddRegistrantToGroup( RegistrationRegistrant registrant )
        {
        }

        /// <summary>
        /// Builds the Lava merge-field dictionary for a single <paramref name="registration"/>
        /// including the <c>RegistrationInstance</c>, <c>Registration</c>,
        /// <c>TransitionedRegistrants</c>, and the <c>AdditionalFieldsNeeded</c> flag.
        /// </summary>
        private Dictionary<string, object> BuildMergeFields( Registration registration, RegistrationInstance registrationInstance, List<RegistrationRegistrant> transitionedRegistrants )
        {
            return null;
        }

        /// <summary>
        /// Groups <paramref name="registrants"/> by their parent <see cref="Registration"/>
        /// and returns a list of <see cref="RegistrantWaitListMoveRecipientBag"/> suitable
        /// for display in the email recipient list. Email template fields are resolved
        /// against each registration's merge fields using the first registrant as a sample.
        /// </summary>
        private List<RegistrantWaitListMoveBag> BuildRecipientBags( List<RegistrationRegistrant> registrants, RegistrationInstance registrationInstance )
        {
            return null;
        }

        /// <summary>
        /// Resolves the email template fields (from name, from email, subject, body)
        /// from the <see cref="RegistrationTemplate"/> against the supplied
        /// <paramref name="mergeFields"/>.
        /// </summary>
        private (string FromName, string FromEmail, string Subject, string Body) ResolveEmailTemplate( RegistrationTemplate template, Dictionary<string, object> mergeFields )
        {
            return default;
        }

        /// <summary>
        /// Builds the URL for the "Return to Registration Instance" navigation link.
        /// Returns an empty string when the linked page attribute is not configured.
        /// </summary>
        private string BuildRegistrationInstancePageUrl( RegistrationInstance registrationInstance )
        {
            return string.Empty;
        }

        #endregion Methods

        #region Block Actions

        /// <summary>
        /// Sends confirmation emails to the selected recipients using the supplied
        /// from/subject/body overrides. Each email is sent and saved in its own
        /// <see cref="RockContext"/> to avoid change-tracker bloat on large batches.
        /// Returns a summary message with sent and error counts.
        /// </summary>
        [BlockAction]
        public BlockActionResult SendEmails( RegistrantWaitListMoveEmailBag request )
        {
            return ActionOk();
        }

        #endregion Block Actions
    }
}
