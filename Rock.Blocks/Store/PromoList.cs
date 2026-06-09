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
using System.Linq;

using Rock.Attribute;
using Rock.Model;
using Rock.Store;
using Rock.ViewModels.Blocks.Store.PromoList;

namespace Rock.Blocks.Store
{
    /// <summary>
    /// Lists Rock Store promotions as a card list or rotator.
    /// </summary>

    [DisplayName( "Promo List" )]
    [Category( "Store" )]
    [Description( "Lists Rock Store promotions." )]
    [IconCssClass( "fa fa-star" )]
    [SupportedSiteTypes( SiteType.Web )]

    #region Block Attributes

    [CustomRadioListField( "Display Style",
        Description = "Determines how the promotions are rendered: a card list or a rotator.",
        ListSource = "Card List,Rotator",
        IsRequired = true,
        DefaultValue = "Card List",
        Key = AttributeKey.DisplayStyle,
        Order = 0 )]

    [CustomRadioListField( "Promo Type",
        Description = "Display the promos of the specified type",
        ListSource = "All, Top Paid, Top Free, Featured",
        IsRequired = true,
        DefaultValue = "Normal",
        Order = 1 )]

    [TextField( "Category Id",
        Description = "Filters promos for a specific category id. If none is provided it will show promos with no category.",
        IsRequired = false,
        Order = 2 )]

    [LinkedPage( "Detail Page",
        Description = "Page reference to use for the detail page.",
        IsRequired = false,
        Order = 3 )]

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "085D1D51-CC02-4E07-A90D-4E10638B7B46" )]
    [Rock.SystemGuid.BlockTypeGuid( "511C6C14-4D98-42CF-B89D-82F4D63C1E49" )]
    public class PromoList : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string PromoType = "PromoType";
            public const string CategoryId = "CategoryId";
            public const string DisplayStyle = "DisplayStyle";
            public const string DetailPage = "DetailPage";
        }

        private static class DisplayStyleValue
        {
            public const string CardList = "Card List";
            public const string Rotator = "Rotator";
        }

        private static class PageParameterKey
        {
            public const string PackageId = "PackageId";
        }

        private static class NavigationUrlKey
        {
            public const string DetailPage = "DetailPage";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            return GetBox();
        }

        /// <summary>
        /// Builds the initialization box for the block, including block settings
        /// and the promo data the Obsidian component needs to render.
        /// </summary>
        /// <returns>The initialization box for the block.</returns>
        private PromoListInitializationBox GetBox()
        {
            var box = new PromoListInitializationBox
            {
                DisplayStyle = GetAttributeValue( AttributeKey.DisplayStyle )
            };

            box.NavigationUrls = GetBoxNavigationUrls();

            var promos = GetPromoListBags();

            if ( promos == null )
            {
                return box;
            }

            box.Promos = promos;

            return box;
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.DetailPage] = this.GetLinkedPageUrl( AttributeKey.DetailPage, "PackageId", "((Key))" )
            };
        }

        /// <summary>
        /// Fetches the promos from the Rock Store API and maps them to bags
        /// for the Obsidian component.
        /// </summary>
        /// <returns>The list of promo bags, or <c>null</c> if the store could not be reached.</returns>
        private List<PromoBag> GetPromoListBags()
        {
            var promoService = new PromoService();

            var promos = new List<Promo>();

            var categoryParam = GetAttributeValue( AttributeKey.CategoryId );

            int? categoryId = categoryParam.IsNotNullOrWhiteSpace()
                ? categoryParam.AsInteger()
                : null;

            var promoType = GetAttributeValue( "PromoType" );

            bool isTopPaid = promoType == "Top Paid";
            bool isTopFree = promoType == "Top Free";
            bool isFeatured = promoType == "Featured";

            promos = promoService.GetPromos( categoryId, isTopFree, isFeatured, isTopPaid );

            if ( promos == null )
            {
                return new List<PromoBag>();
            }

            var detailPageUrl = this.GetLinkedPageUrl( AttributeKey.DetailPage, PageParameterKey.PackageId, "{0}" );

            return promos.Select( p => new PromoBag
            {
                PackageId = p.PackageId,
                PackageName = p.PackageName,
                PackageVendor = p.PackageVendor,
                PackageRating = p.PackageRating,
                PackagePrice = p.PackagePrice,
                PackageIconUrl = p.PackageIconBinaryFile?.ImageUrl,
                ImageLargeUrl = p.ImageLarge?.ImageUrl,
                DetailPageUrl = string.Format( detailPageUrl, p.PackageId )
            } ).ToList();
        }

        #endregion Methods
    }
}
