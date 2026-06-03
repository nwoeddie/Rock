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
using System.Linq;

using Rock.Attribute;
using Rock.Blocks.Event;
using Rock.Store;
using Rock.ViewModels.Blocks.Event;
using Rock.ViewModels.Blocks.Store;
using Rock.Web.UI.Controls;

namespace Rock.Blocks.Store
{
    /// <summary>
    /// Lists categories for Rock Store pages using a Lava template.
    /// </summary>
    /// <seealso cref="Rock.Blocks.RockBlockType" />

    [DisplayName( "Package Category List Lava" )]
    [Category( "Store" )]
    [Description( "Lists categories for Rock Store pages." )]
    [IconCssClass( "fa fa-list" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    #region Block Attributes

    [CodeEditorField( "Lava Template",
        Description = "Lava template to use to display the categories",
        EditorMode = CodeEditorMode.Lava,
        EditorHeight = 400,
        IsRequired = true,
        DefaultValue = @"{% include '~/Assets/Lava/Store/PackageCategoryListSidebar.lava' %}",
        Order = 2 )]

    [LinkedPage( "Detail Page",
        Description = "Page reference to use for the detail page.",
        IsRequired = false,
        Key = AttributeKey.DetailPage,
        Order = 4 )]

    #endregion

    [Rock.SystemGuid.EntityTypeGuid( "9A9F0646-F48C-4232-B10A-222109677C9F" )]
    [Rock.SystemGuid.BlockTypeGuid( "69DC9386-1A26-41E0-ACA2-6D5ABFB22873" )]
    // [Rock.SystemGuid.BlockTypeGuid( "470C6EFF-091C-4593-848C-49547D0EBEEE" )]
    public class PackageCategoryListLava : RockBlockType
    {
        #region Keys

        private static class AttributeKey
        {
            public const string LavaTemplate = "LavaTemplate";
            public const string DetailPage = "DetailPage";
        }

        #endregion Keys

        #region Properties

        private string LavaTemplate => GetAttributeValue( AttributeKey.LavaTemplate );

        #endregion Properties

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {

            var packageCategoryListLavaBox = new PackageCategoryListLavaBox
            {
                Html = GetCategoriesHtml()
            };

            return packageCategoryListLavaBox;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override string GetInitialHtmlContent()
        {
            return GetCategoriesHtml();
        }

        /// <summary>
        /// Fetches the Rock Store package categories, resolves the configured Lava template,
        /// and returns the rendered HTML (or an error panel when the store is unavailable).
        /// </summary>
        /// <returns>The rendered HTML for the category list.</returns>
        private string GetCategoriesHtml()
        {
            var categoryService = new PackageCategoryService();
            var errors = "";

            var categories = categoryService.GetCategories( out errors ).OrderBy( c => c.Name );

            // When the store cannot be reached, mirror the WebForms error panel:
            // a "Store Currently Not Available" message plus the underlying error detail.
            if ( !errors.IsNullOrWhiteSpace() )
            {
                return $@"<div class=""alert alert-warning"">
    <h4>Store Currently Not Available</h4>
    <p>We're sorry, the Rock Store is currently not available. Check back soon!</p>
    <small><em>{errors.EncodeHtml()}</em></small>
</div>";
            }

            var mergeFields = RequestContext.GetCommonMergeFields();
            mergeFields.Add( "Categories", categories );

            var linkedPages = new Dictionary<string, object>
            {
                { "DetailPage", this.GetLinkedPageUrl( AttributeKey.DetailPage ) }
            };

            mergeFields.Add( "LinkedPages", linkedPages );

            return LavaTemplate.ResolveMergeFields( mergeFields );
        }

        #endregion Methods
    }
}
