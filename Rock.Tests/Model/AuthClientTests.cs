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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rock.Model;

namespace Rock.Tests.Model
{
    [TestClass]
    public class AuthClientTests
    {
        [TestMethod]
        public void GetRedirectUris_WithSingleUri_ReturnsOneItem()
        {
            var authClient = new AuthClient { RedirectUri = "https://example.com/callback" };

            var result = authClient.GetRedirectUris();

            Assert.HasCount( 1, result );
            Assert.AreEqual( "https://example.com/callback", result[0] );
        }

        [TestMethod]
        public void GetRedirectUris_WithCommaDelimitedUris_ReturnsTrimmedItems()
        {
            var authClient = new AuthClient { RedirectUri = "https://a.com/cb, https://b.com/cb" };

            var result = authClient.GetRedirectUris();

            CollectionAssert.AreEqual( new List<string> { "https://a.com/cb", "https://b.com/cb" }, result );
        }

        [TestMethod]
        public void GetRedirectUris_WithEmptyValue_ReturnsEmptyList()
        {
            var authClient = new AuthClient { RedirectUri = "" };

            Assert.IsEmpty( authClient.GetRedirectUris() );
        }

        [TestMethod]
        public void JoinRedirectUris_TrimsDeduplicatesAndDropsEmptyEntries()
        {
            var input = new List<string> { " https://a.com ", "https://a.com", "", "https://b.com" };

            var result = AuthClient.JoinRedirectUris( input );

            Assert.AreEqual( "https://a.com,https://b.com", result );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithValidHttpAndHttpsUris_ReturnsTrue()
        {
            var input = new List<string> { "https://a.com/cb", "http://localhost:6229/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsTrue( result );
            Assert.AreEqual( string.Empty, errorMessage );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithNoUris_ReturnsFalse()
        {
            var result = AuthClient.ValidateRedirectUris( new List<string>(), out var errorMessage );

            Assert.IsFalse( result );
            Assert.Contains( "At least one", errorMessage );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithCommaInValue_ReturnsFalse()
        {
            var input = new List<string> { "https://a.com/cb,https://b.com/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
            Assert.Contains( "comma", errorMessage );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithNonHttpScheme_ReturnsFalse()
        {
            var input = new List<string> { "ftp://a.com/cb" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
            Assert.Contains( "http", errorMessage );
        }

        [TestMethod]
        public void ValidateRedirectUris_WithRelativeUri_ReturnsFalse()
        {
            var input = new List<string> { "/callback" };

            var result = AuthClient.ValidateRedirectUris( input, out var errorMessage );

            Assert.IsFalse( result );
            Assert.Contains( "http", errorMessage );
        }
    }
}
