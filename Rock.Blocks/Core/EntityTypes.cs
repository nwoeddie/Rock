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
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Obsidian.UI;
using Rock.Security;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Core.EntityTypes;
using Rock.ViewModels.Blocks.Core.PersonSignalList;
using Rock.Web.Cache;

namespace Rock.Blocks.Core
{
    /// <summary>
    /// Displays a list of entity types.
    /// </summary>

    [DisplayName( "Entity Types" )]
    [Category( "Core" )]
    [Description( "Displays a list of entity types." )]
    [IconCssClass( "fa fa-list" )]
    // [SupportedSiteTypes( Model.SiteType.Web )]

    [LinkedPage( "Detail Page",
        Description = "The page that will show the entity type details.",
        Key = AttributeKey.DetailPage )]

    [Rock.SystemGuid.EntityTypeGuid( "712dd8aa-aafa-4e92-b804-d884b6848adf" )]
    [Rock.SystemGuid.BlockTypeGuid( "3136cb51-8b94-4116-9c34-59ba6425a1ca" )]
    [CustomizedGrid]
    public class EntityTypes : RockListBlockType<EntityTypesBag>
    {
        #region Keys

        private static class AttributeKey
        {
            public const string DetailPage = "DetailPage";
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
            var box = new ListBlockBox<EntityTypesOptionsBag>();
            var builder = GetGridBuilder();

            box.IsAddEnabled = GetIsAddEnabled();
            box.IsDeleteEnabled = BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );
            box.ExpectedRowCount = null;
            box.NavigationUrls = GetBoxNavigationUrls();
            box.Options = GetBoxOptions();
            box.GridDefinition = builder.BuildDefinition();

            return box;
        }

        /// <summary>
        /// Gets the box options required for the component to render the list.
        /// </summary>
        /// <returns>The options that provide additional details to the block.</returns>
        private EntityTypesOptionsBag GetBoxOptions()
        {
            var options = new EntityTypesOptionsBag();

            return options;
        }

        /// <summary>
        /// Determines if the add button should be enabled in the grid.
        /// <summary>
        /// <returns>A boolean value that indicates if the add button should be enabled.</returns>
        private bool GetIsAddEnabled()
        {
            return BlockCache.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson );
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.DetailPage] = this.GetLinkedPageUrl( AttributeKey.DetailPage, "EntityTypeId", "((Key))" )
            };
        }

        /// <inheritdoc/>
        protected override IQueryable<EntityTypesBag> GetListQueryable( RockContext rockContext )
        {
            var entityTypeService = new EntityTypeService( rockContext );
            var data = entityTypeService.Queryable().Where( e => e.IsEntity ).Select( e => new EntityTypesBag
                {
                    Id = e.Id,
                    Name = e.Name,
                    FriendlyName = e.FriendlyName,
                    IsCommon = e.IsCommon,
                    IsSecured = e.IsSecured
            } );

            return data;
        }

        /// <inheritdoc/>
        protected override IQueryable<EntityTypesBag> GetOrderedListQueryable( IQueryable<EntityTypesBag> queryable, RockContext rockContext )
        {
            return queryable.OrderBy( p => p.Name );
        }


        /// <inheritdoc/>
        protected override GridBuilder<EntityTypesBag> GetGridBuilder()
        {
            return new GridBuilder<EntityTypesBag>()
                .WithBlock( this )
                .AddTextField( "idKey", a => a.Id.AsIdKey() )
                .AddTextField( "name", a => a.Name )
                .AddTextField( "friendlyName", a => a.FriendlyName )
                .AddField( "isCommon", a => a.IsCommon )
                .AddField( "isSecurityDisabled", a => !a.IsSecured );
        }

        #endregion

        #region Block Actions

        /// <summary>
        /// Gets the entity type representation for editing purposes.
        /// </summary>
        /// <param name="key">The unique identifier of the entity type to be edited.</param>
        /// <returns>A response that includes the editable representation of the entity type.</returns>
        [BlockAction]
        public BlockActionResult GetEditEntityType( string key )
        {
            var entityTypeService = new EntityTypeService( RockContext );
            var entity = entityTypeService.Get( key, false );

            if ( entity == null )
            {
                return ActionBadRequest( $"{EntityType.FriendlyTypeName} not found." );
            }

            if ( !entity.IsAuthorized( Authorization.EDIT, RequestContext.CurrentPerson ) )
            {
                return ActionBadRequest( "Not authorized to make changes to this entity type." );
            }

            var editBag = new EntityTypesBag
            {
                Id = entity.Id,
                Name = entity.Name,
                FriendlyName = entity.FriendlyName,
                IsCommon = entity.IsCommon,
                IsRelatedToInteractionTrackedOnCreate = entity.IsRelatedToInteractionTrackedOnCreate,
                IndexResultTemplate = entity.IndexResultTemplate,
                IndexDocumentUrl = entity.IndexDocumentUrl,
                LinkUrlLavaTemplate = entity.LinkUrlLavaTemplate,
            };

            return ActionOk( editBag );
        }
        #endregion
    }
}
