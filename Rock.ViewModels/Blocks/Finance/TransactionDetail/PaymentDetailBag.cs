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

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.TransactionDetail
{
    /// <summary>
    /// Payment method details for a financial transaction, including currency type
    /// and credit card information when applicable.
    /// </summary>
    public class PaymentDetailBag
    {
        /// <summary>
        /// Gets or sets the currency type defined value (e.g. Credit Card, Check, Cash).
        /// </summary>
        public ListItemBag CurrencyType { get; set; }

        /// <summary>
        /// Gets or sets the credit card network defined value (e.g. Visa, Mastercard).
        /// Only populated when <see cref="IsCreditCard"/> is <c>true</c>.
        /// </summary>
        public ListItemBag CreditCardType { get; set; }

        /// <summary>
        /// Gets or sets the name printed on the credit or debit card.
        /// </summary>
        public string NameOnCard { get; set; }

        /// <summary>
        /// Gets or sets the masked account or card number (e.g. xxxx-xxxx-xxxx-1234).
        /// </summary>
        public string AccountNumberMasked { get; set; }

        /// <summary>
        /// Gets or sets the card expiration date string (e.g. 12/27).
        /// </summary>
        public string ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the currency type is credit card,
        /// used to show or hide card-specific fields in the UI.
        /// </summary>
        public bool IsCreditCard { get; set; }
    }
}
