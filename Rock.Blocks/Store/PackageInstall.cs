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
using System.ComponentModel;
using System.IO.Compression;

using Rock;
using Rock.Attribute;
using Rock.Store;
using Rock.ViewModels.Blocks.Store.PackageInstall;

namespace Rock.Blocks.Store
{
    /// <summary>
    /// Installs a package that has been downloaded in the Rock Shop.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Package Install" )]
    [Category( "Store" )]
    [Description( "Installs a package." )]
    [IconCssClass( "ti ti-gift" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage( "Link Organization Page",
        Key = AttributeKey.LinkOrganizationPage,
        Description = "Page to allow the user to link an organization to the store.",
        IsRequired = false,
        Order = 0 )]

    #endregion Block Attributes

    [Rock.SystemGuid.EntityTypeGuid( "1078292D-78F0-48C2-93C5-594B2BFA7C10" )]
    [Rock.SystemGuid.BlockTypeGuid( "00B39CC5-CEFA-4A2C-AF09-D8DC35275C5B" )]
    //[Rock.SystemGuid.BlockTypeGuid( "EA60C1AB-ADAB-4EDF-94F8-B0FE214B6F15" )]
    public class PackageInstall : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string LinkOrganizationPage = "LinkOrganizationPage";
        }

        private static class PageParameterKey
        {
            public const string PackageId = "PackageId";
        }

        #endregion Keys

        #region Constants

        /// <summary>
        /// The login message shown when a prior version of the package is
        /// already installed.
        /// </summary>
        private const string UpdateMessage = "Log in below with your Rock Store account to upgrade this package.";

        /// <summary>
        /// The login message shown when the package was previously purchased
        /// but is not currently installed.
        /// </summary>
        private const string InstallPreviousMessage = "Log in below with your Rock Store account to install this previously purchased package.";

        #endregion Constants

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            return GetInitializationBox();
        }

        /// <summary>
        /// Builds the initialization box describing the package and the current
        /// store/install state for the component to render.
        /// </summary>
        /// <returns>The initialization box.</returns>
        private PackageInstallInitializationBox GetInitializationBox()
        {
            // When the store has not been linked to an organization, the
            // component redirects to the Link Organization page, so no package
            // data needs to be loaded.
            if ( !StoreService.OrganizationIsConfigured() )
            {
                return new PackageInstallInitializationBox
                {
                    IsStoreConfigured = false,
                    LinkOrganizationPageUrl = GetLinkOrganizationPageUrl()
                };
            }

            var box = new PackageInstallInitializationBox
            {
                IsStoreConfigured = true
            };

            var packageId = PageParameter( PageParameterKey.PackageId ).AsIntegerOrNull() ?? -1;

            var package = new PackageService().GetPackage( packageId, out var errorResponse );

            // Surface a store error or a missing package as the unavailable
            // notice instead of the install form.
            if ( errorResponse.IsNotNullOrWhiteSpace() )
            {
                box.ErrorMessage = errorResponse;
                return box;
            }

            if ( package == null )
            {
                box.ErrorMessage = "The requested package could not be found.";
                return box;
            }

            box.PackageName = package.Name;
            box.PackageIconImageUrl = package.PackageIconBinaryFile?.ImageUrl;
            box.InstallButtonText = "Install";
            box.InstallMessage = package.IsFree
                ? GetInstallFreeMessage( package.Name )
                : GetInstallPurchaseMessage( package.Name, package.Price );

            if ( package.IsPurchased )
            {
                var installedPackage = InstalledPackageService.InstalledPackageVersion( package.Id );

                if ( installedPackage == null )
                {
                    // Previously purchased but not currently installed.
                    box.InstallMessage = InstallPreviousMessage;
                }
                else
                {
                    // A version is already installed; the action upgrades it.
                    box.InstallMessage = UpdateMessage;
                    box.InstallButtonText = "Update";
                }
            }

            return box;
        }

        /// <summary>
        /// Resolves the Link Organization page URL, carrying a ReturnUrl back to
        /// this page so the user returns here after configuring the store.
        /// </summary>
        /// <returns>The Link Organization page URL.</returns>
        private string GetLinkOrganizationPageUrl()
        {
            var queryParams = new Dictionary<string, string>
            {
                { "ReturnUrl", RequestContext.RequestUri?.PathAndQuery }
            };

            return this.GetLinkedPageUrl( AttributeKey.LinkOrganizationPage, queryParams );
        }

        /// <summary>
        /// Gets the version id of the currently installed package.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <returns>The installed version id, or -1 if the package is not installed.</returns>
        private int GetInstalledPackageVersionId( int packageId )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds the login message shown for a paid package.
        /// </summary>
        /// <param name="name">The package name.</param>
        /// <param name="price">The package price.</param>
        /// <returns>The HTML message.</returns>
        private static string GetInstallPurchaseMessage( string name, decimal? price ) =>
            $"Log in below with your Rock Store account to install the <em>{name}</em> package. Your credit card on file will be charged ${price}.";

        /// <summary>
        /// Builds the login message shown for a free package.
        /// </summary>
        /// <param name="name">The package name.</param>
        /// <returns>The HTML message.</returns>
        private static string GetInstallFreeMessage( string name ) =>
            $"Log in below with your Rock Store account to install free <em>{name}</em> package.";

        private string GetMessage( PurchaseResult purchaseResult, string detail )
        {
            return purchaseResult switch
            {
                PurchaseResult.AuthenticationFailed => $"<strong>Could Not Authenticate</strong> {detail} If you need further help see the <a href='https://rockrms.com/RockShopHelp'>Rock Shop Help Page</a>.",
                PurchaseResult.Error => $"<strong>An Error Occurred</strong> {detail}",
                PurchaseResult.NoCardOnFile => $"<strong>No Card On File</strong> No credit card is on file for your organization. Please add a card from your <a href='{RequestContext.ResolveRockUrl( "~/RockShop/Account" )}'>Account Page</a> or see the <a href='https://rockrms.com/RockShopHelp'>Rock Shop Help Page</a>.",
                PurchaseResult.NotAuthorized => $"<div class='alert alert-warning margin-t-md'><strong>Unauthorized</strong> You are not currently authorized to make purchases for this organization. Please see your organization's primary contact to enable your account for purchases or see the <a href='https://rockrms.com/RockShopHelp'>Rock Shop Help Page</a>.</div>",
                PurchaseResult.PaymentFailed => $"<div class='alert alert-warning margin-t-md'><strong>Payment Error</strong> An error occurred while processing the credit card on file for your organization. The error was: {installResponse.Message}. Please update your card's information from your <a href='{ResolveRockUrl( "~/RockShop/Account" )}'>Account Page</a> or see the <a href='https://rockrms.com/RockShopHelp'>Rock Shop Help Page</a>.</div>"
            };
        }

        #endregion Methods

            #region Install Pipeline

            /// <summary>
            /// Runs the install steps from the purchase response that are newer than
            /// the installed version and compatible with this version of Rock.
            /// </summary>
            /// <param name="purchaseResponse">The purchase response from the Rock Shop.</param>
            /// <returns>The result of the install attempt.</returns>
        private InstallPackageResponseBag ProcessInstall( PurchaseResponse purchaseResponse )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Downloads and applies a single package install step (content files,
        /// XDT transforms, SQL, and file deletions).
        /// </summary>
        /// <param name="installStep">The install step to process.</param>
        /// <param name="purchaseResponse">The purchase response from the Rock Shop.</param>
        /// <param name="appRoot">The application root directory path.</param>
        /// <param name="rockShopWorkingDir">The Rock Shop working directory path.</param>
        /// <param name="result">The result to surface to the caller when the step ends the install.</param>
        /// <returns><c>true</c> if the step installed successfully; otherwise <c>false</c>.</returns>
        private bool InstallPackageStep( PackageInstallStep installStep, PurchaseResponse purchaseResponse, string appRoot, string rockShopWorkingDir, out InstallPackageResponseBag result )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Extracts a single zip entry from the package's content folder,
        /// applying any XDT transform or honoring the bin blacklist.
        /// </summary>
        /// <param name="entry">The zip entry to process.</param>
        /// <param name="appRoot">The application root directory path.</param>
        /// <returns><c>true</c> if some action was taken; otherwise <c>false</c>.</returns>
        private bool ProcessZipEntry( ZipArchiveEntry entry, string appRoot )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Runs the package's install/run.sql script if present.
        /// </summary>
        /// <param name="packageZip">The package archive.</param>
        /// <param name="errorMessage">The error message when execution fails.</param>
        /// <returns><c>true</c> if a script was run; otherwise <c>false</c>.</returns>
        private bool ProcessRunSQL( ZipArchive packageZip, out string errorMessage )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes the package's install/deletefile.lst, removing the listed
        /// files and folders that are not blacklisted.
        /// </summary>
        /// <param name="packageZip">The package archive.</param>
        /// <param name="appRoot">The application root directory path.</param>
        /// <param name="errorMessage">The error message when a deletion fails.</param>
        /// <returns><c>true</c> if some action was taken; otherwise <c>false</c>.</returns>
        private bool ProcessDeleteFileList( ZipArchive packageZip, string appRoot, out string errorMessage )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a downloaded package file after a failed install.
        /// </summary>
        /// <param name="packageFile">The package file path.</param>
        private void CleanUpPackage( string packageFile )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a directory if it does not already exist.
        /// </summary>
        /// <param name="directoryPath">The directory path.</param>
        private void EnsureDirectoryExists( string directoryPath )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether a file from the archive is blacklisted and should
        /// neither be installed into nor deleted from the Bin directory.
        /// </summary>
        /// <param name="fullName">The full name from the archive, such as 'bin/some.dll'.</param>
        /// <returns><c>true</c> if the file is blacklisted; otherwise <c>false</c>.</returns>
        private static bool IsBlacklisted( string fullName )
        {
            throw new NotImplementedException();
        }

        #endregion Install Pipeline

        #region Block Actions

        /// <summary>
        /// Authenticates with the store, purchases the package if needed, and
        /// installs it.
        /// </summary>
        /// <param name="bag">The store credentials.</param>
        /// <returns>The result of the purchase and install attempt.</returns>
        [BlockAction]
        public BlockActionResult Install( InstallPackageRequestBag bag )
        {
            var packageId = PageParameter( PageParameterKey.PackageId ).AsIntegerOrNull() ?? -1;

            var installResponse = new StoreService().Purchase( bag.Username, bag.Password, packageId, out var errorResponse );

            // A null response means the store could not be reached or the
            // request itself failed before a purchase result was determined.
            if ( installResponse == null )
            {
                var detail = errorResponse.IsNotNullOrWhiteSpace() ? errorResponse : "Unknown";

                return ActionOk( new InstallPackageResponseBag
                {
                    IsSuccess = false,
                    MessageAlertType = "danger",
                    Message = $"<strong>Install Error</strong> An error occurred while attempting to authenticate your install of this package. The error was: {detail}."
                } );
            }

            if ( installResponse.PurchaseResult != PurchaseResult.Success )
            {
                var message = GetMessage( installResponse.PurchaseResult, installResponse.Message );
                return ActionOk( new InstallPackageResponseBag
                {
                    IsSuccess = false,
                    MessageAlertType = "danger",
                    Message = message
                } );
            }

            ProcessInstall( installResponse );

        }

        #endregion Block Actions
    }
}
