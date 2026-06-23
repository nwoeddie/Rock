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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using Rock.Data;
using Rock.Enums.Security;
using Rock.Security;

namespace Rock.Model
{
    /// <summary>
    /// Represents an Authentication Client
    /// </summary>

    [RockDomain( "Core" )]
    [Table( "AuthClient" )]
    [DataContract]
    [CodeGenerateRest( Enums.CodeGenerateRestEndpoint.ReadOnly, DisableEntitySecurity = true )]
    [Rock.SystemGuid.EntityTypeGuid( "CBD66C3A-959A-4A0B-926C-C3ADE43066B1")]
    public partial class AuthClient : Model<AuthClient>, IHasActiveFlag
    {

        /// <summary>
        /// Gets or sets a value indicating whether [allow user API access].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [allow user API access]; otherwise, <c>false</c>.
        /// </value>
        [DataMember]
        public bool AllowUserApiAccess { get; set; }

        /// <summary>
        /// Gets or sets the allowed claims.
        /// </summary>
        /// <value>
        /// The allowed claims.
        /// </value>
        [DataMember]
        [StringValidation( StringValidationProfile.Unrestricted )]
        public string AllowedClaims { get; set; }

        /// <summary>
        /// Gets or sets the allowed scopes.
        /// </summary>
        /// <value>
        /// The allowed scopes.
        /// </value>
        [DataMember]
        [StringValidation( StringValidationProfile.Unrestricted )]
        public string AllowedScopes { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if this item is active or not.
        /// </summary>
        /// <value>
        /// Active.
        /// </value>
        [DataMember]
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [DataMember( IsRequired = true )]
        [Required]
        [StringValidation( StringValidationProfile.Name )]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the client identifier.
        /// </summary>
        /// <value>
        /// The client identifier.
        /// </value>
        [DataMember( IsRequired = true )]
        [Required]
        [Index( IsUnique = true )]
        [MaxLength(50)]
        [StringValidation( StringValidationProfile.PlainText )]
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret hash.
        /// </summary>
        /// <value>
        /// The client secret hash.
        /// </value>
        [HideFromReporting]
        [StringValidation( StringValidationProfile.PlainText )]
        public string ClientSecretHash { get; set; }

        /// <summary>
        /// Gets or sets the redirect URL.
        /// </summary>
        /// <value>
        /// The redirect URL.
        /// </value>
        [DataMember( IsRequired = true )]
        [Required]
        [StringValidation( StringValidationProfile.PlainText )]
        public string RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the post logout redirect URI.
        /// </summary>
        /// <value>
        /// The post logout redirect URI.
        /// </value>
        [DataMember( IsRequired = true )]
        [Required]
        [StringValidation( StringValidationProfile.PlainText )]
        public string PostLogoutRedirectUri { get; set; }

        /// <summary>
        /// Gets or sets the scope approval expiration in days.
        /// </summary>
        /// <value>
        /// The scope approval expiration in days.
        /// </value>
        [DataMember( IsRequired = true )]
        [Required]
        [Range( 0, int.MaxValue )]
        public int ScopeApprovalExpiration { get; set; } = 365;

        #region Redirect URI Helpers

        /*
            6/18/26 - CLAUDE

            Redirect URIs are stored as a single comma-delimited string in the
            RedirectUri column (the format the OIDC authorization provider and
            the dynamic-registration paths already use). These helpers centralize
            the split/join/validate rules so the provider, the detail block, and
            their tests all share one implementation.

            Reason: One source of truth for multi-redirect-URI parsing and validation.
        */

        /// <summary>
        /// Gets the list of registered redirect URIs by splitting the comma-delimited
        /// <see cref="RedirectUri"/> value. Entries are trimmed, blank entries removed,
        /// and duplicates removed using an ordinal comparison. Returns an empty list
        /// when no redirect URIs are registered.
        /// </summary>
        /// <returns>The normalized list of registered redirect URIs.</returns>
        public List<string> GetRedirectUris()
        {
            if ( RedirectUri.IsNullOrWhiteSpace() )
            {
                return new List<string>();
            }

            return RedirectUri
                .Split( ',' )
                .Select( uri => uri.Trim() )
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Distinct( StringComparer.Ordinal )
                .ToList();
        }

        /// <summary>
        /// Normalizes a set of redirect URIs into the comma-delimited string used to
        /// store the <see cref="RedirectUri"/> value. Entries are trimmed, blank
        /// entries removed, and duplicates removed using an ordinal comparison.
        /// </summary>
        /// <param name="redirectUris">The redirect URIs to normalize and join.</param>
        /// <returns>The comma-delimited storage value, or an empty string when none are supplied.</returns>
        public static string JoinRedirectUris( IEnumerable<string> redirectUris )
        {
            if ( redirectUris == null )
            {
                return string.Empty;
            }

            var normalized = redirectUris
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Select( uri => uri.Trim() )
                .Distinct( StringComparer.Ordinal )
                .ToList();

            return string.Join( ",", normalized );
        }

        /// <summary>
        /// Validates a set of redirect URIs. At least one URI is required, and each
        /// must be a well-formed absolute http or https URI that does not contain a
        /// comma (the storage delimiter).
        /// </summary>
        /// <param name="redirectUris">The redirect URIs to validate.</param>
        /// <param name="errorMessage">On <c>false</c> return, the reason validation failed; otherwise empty.</param>
        /// <returns><c>true</c> when all redirect URIs are valid; otherwise <c>false</c>.</returns>
        public static bool ValidateRedirectUris( IEnumerable<string> redirectUris, out string errorMessage )
        {
            errorMessage = string.Empty;

            var uris = ( redirectUris ?? Enumerable.Empty<string>() )
                .Where( uri => uri.IsNotNullOrWhiteSpace() )
                .Select( uri => uri.Trim() )
                .ToList();

            if ( !uris.Any() )
            {
                errorMessage = "At least one redirect URI is required.";
                return false;
            }

            foreach ( var uri in uris )
            {
                if ( !IsValidRedirectUri( uri, out errorMessage ) )
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a single redirect URI is valid for storage: it must be
        /// non-blank, must not contain a comma (the storage delimiter), and must be a
        /// well-formed absolute http or https URI.
        /// </summary>
        /// <param name="redirectUri">The redirect URI to validate.</param>
        /// <param name="errorMessage">On <c>false</c> return, the reason the URI is invalid; otherwise empty.</param>
        /// <returns><c>true</c> when the redirect URI is valid; otherwise <c>false</c>.</returns>
        private static bool IsValidRedirectUri( string redirectUri, out string errorMessage )
        {
            errorMessage = string.Empty;

            if ( redirectUri.IsNullOrWhiteSpace() )
            {
                errorMessage = "A redirect URI cannot be blank.";
                return false;
            }

            if ( redirectUri.Contains( "," ) )
            {
                errorMessage = $"The redirect URI '{redirectUri}' cannot contain a comma. Enter each redirect URI separately.";
                return false;
            }

            if ( !Uri.TryCreate( redirectUri, UriKind.Absolute, out var uri )
                || ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps ) )
            {
                errorMessage = $"The redirect URI '{redirectUri}' must be a valid absolute http or https URL.";
                return false;
            }

            return true;
        }

        #endregion Redirect URI Helpers
    }

    #region Entity Configuration

    /// <summary>
    /// AuthClient Configuration Class
    /// </summary>
    public partial class AuthClientConfiguration : EntityTypeConfiguration<AuthClient>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthClientConfiguration"/> class.
        /// </summary>
        public AuthClientConfiguration()
        {
            // Empty constructor. This is required to tell EF that this model exists.
        }
    }

    #endregion Entity Configuration
}
