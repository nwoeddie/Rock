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

using System.ComponentModel;
using System.Linq;

using Rock.Attribute;
using Rock.Store;
using Rock.Utility.ExtensionMethods;
using Rock.ViewModels.Blocks.Store;

namespace Rock.Blocks.Store
{
    /// <summary>
    /// Lists categories for Rock Store pages.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Package Category List Lava" )]
    [Category( "Store" )]
    [Description( "Lists categories for Rock Store pages." )]
    [IconCssClass( "fa fa-list" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [LinkedPage( "Detail Page",
        Description = "Page reference to use for the detail page.",
        IsRequired = false,
        Key = AttributeKey.DetailPage,
        Order = 4 )]

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "9A9F0646-F48C-4232-B10A-222109677C9F" )]
    [Rock.SystemGuid.BlockTypeGuid( "69DC9386-1A26-41E0-ACA2-6D5ABFB22873" )]
    public class PackageCategoryListLava : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DetailPage = "DetailPage";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            // Store.css supplies the styling the store category list relies on
            // (mirrors the WebForms block's OnInit AddCSSLink).
            RequestContext.Response.AddCssLink( RequestContext.ResolveRockUrl( "~/Styles/Blocks/Store/Store.css" ), true );

            return GetBox();
        }

        /// <summary>
        /// Fetches the Rock Store package categories and builds the box the Obsidian
        /// component uses to render the category list (or the store-unavailable panel).
        /// </summary>
        /// <returns>The initialization box for the block.</returns>
        private PackageCategoryListLavaBox GetBox()
        {
            var box = new PackageCategoryListLavaBox();

            var categoryService = new PackageCategoryService();
            var categories = categoryService.GetCategories( out var errors ).OrderBy( c => c.Name );

            // When the store cannot be reached, surface the error so the component can
            // render the "Store Currently Not Available" panel instead of the list.
            if ( !errors.IsNullOrWhiteSpace() )
            {
                box.StoreErrorMessage = errors;
                return box;
            }

            box.Categories = categories
                .Select( c => new PackageCategoryListItemBag
                {
                    Id = c.Id,
                    Name = c.Name
                } )
                .ToList();

            box.DetailPageUrl = this.GetLinkedPageUrl( AttributeKey.DetailPage );

            return box;
        }

        #endregion Methods
    }
}
