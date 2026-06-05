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
using System.ComponentModel;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Store;
using Rock.ViewModels.Blocks.Store.LinkOrganization;

namespace Rock.Blocks.Store
{
    /// <summary>
    /// Links a Rock organization to the store.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Link Organization" )]
    [Category( "Store" )]
    [Description( "Links a Rock organization to the store." )]
    [IconCssClass( "ti ti-link" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    [Rock.SystemGuid.EntityTypeGuid( "363974F0-1A85-40C6-AAAE-36D58A7B7C03" )]
    [Rock.SystemGuid.BlockTypeGuid( "39F9DD1F-675B-492A-8DD2-E89F7BDA3FB3" )]
    // [Rock.SystemGuid.BlockTypeGuid( "41DFED6E-2ECD-4198-80C3-816B27241EB4" )]
    public class LinkOrganization : RockBlockType
    {
        #region Keys

        private static class PageParameterKey
        {
            public const string ReturnUrl = "ReturnUrl";
        }

        #endregion Keys

        #region Constants

        /// <summary>
        /// The URL to navigate to after configuration when no ReturnUrl page
        /// parameter was provided.
        /// </summary>
        private const string DefaultContinueUrl = "/RockShop";

        #endregion Constants

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new LinkOrganizationInitializationBox
            {
                ContinueUrl = GetContinueUrl()
            };

            return box;
        }

        /// <summary>
        /// Resolves the URL to navigate to once the store has been configured.
        /// Uses the ReturnUrl page parameter when supplied, otherwise falls
        /// back to the Rock Shop home page.
        /// </summary>
        /// <returns>The continue URL.</returns>
        private string GetContinueUrl()
        {
            var returnUrl = PageParameter( PageParameterKey.ReturnUrl );

            return returnUrl.IsNotNullOrWhiteSpace() ? returnUrl : DefaultContinueUrl;
        }

        #endregion Methods

        #region Block Actions

        /// <summary>
        /// Retrieves the organizations tied to the supplied store account.
        /// </summary>
        /// <param name="bag">The username and password to authenticate with.</param>
        /// <returns>The next step to display along with any organizations found.</returns>
        [BlockAction]
        public BlockActionResult RetrieveOrganizations( RetrieveOrganizationsRequestBag bag )
        {
            var errorMessage = "";
            var warningMessage = "";

            var organizationService = new OrganizationService();
            var organizations = organizationService.GetOrganizations( bag.Username, bag.Password, out errorMessage ).ToList();

            if( errorMessage.IsNotNullOrWhiteSpace() )
            {
                return ActionBadRequest( errorMessage );
            }

            if ( !organizations.Any() )
            {
                var canAuthenticate = new StoreService().AuthenicateUser( bag.Username, bag.Password );
                if( canAuthenticate )
                {
                   warningMessage = @"It appears that no organizations have been configured for this account. You can 
                                set up an organization on the Rock RMS website. Simply log in and then select 'My Account' from the dropdown in the top right
                                corner or see the <a href='https://www.rockrms.com/RockShopHelp'>Rock Shop Help Page</a>." ;
                }
                else
                {
                    warningMessage = @"The username/password provided did not match a user on the Rock RMS website. Be sure
                    you provide a valid account from this site. If you would like to create an account or retrieve your password please <a href='https://www.rockrms.com/Login'>
                    visit the Rock RMS website</a> or see the <a href='https://www.rockrms.com/RockShopHelp'>Rock Shop Help Page</a>.";
                }

                return ActionOk( new RetrieveOrganizationsResponseBag
                {
                    WarningMessage = warningMessage
                } );
            }

            var organizationKey = StoreService.GetOrganizationKey();
            Organization selectedOrganization = null;

            if ( organizationKey.IsNotNullOrWhiteSpace() )
            {
                selectedOrganization = organizations.FirstOrDefault( o => o.Key == organizationKey );
            }
            else if ( organizations.Count == 1 )
            {
                selectedOrganization = organizations.First();
            }

            // No single organization could be determined; let the user choose.
            if ( selectedOrganization == null )
            {
                return ActionOk( new RetrieveOrganizationsResponseBag
                {
                    NextStep = "SelectOrganization",
                    Organizations = organizations
                        .Select( o => new LinkOrganizationBag { Key = o.Key, Name = o.Name } )
                        .ToList()
                } );
            }

            StoreService.SetOrganizationKey( selectedOrganization.Key );

            return ActionOk( new RetrieveOrganizationsResponseBag
            {
                NextStep = "AverageWeeklyAttendance"
            } );
        }

        /// <summary>
        /// Saves the organization's average weekly attendance to the store.
        /// </summary>
        /// <param name="bag">The credentials, organization key, and attendance.</param>
        /// <returns>The result of the save operation.</returns>
        [BlockAction]
        public BlockActionResult SaveAttendance( SaveAttendanceRequestBag bag )
        {
            // TODO: Implement attendance save logic.
            return ActionOk( new SaveAttendanceResponseBag() );
        }

        #endregion Block Actions
    }
}
