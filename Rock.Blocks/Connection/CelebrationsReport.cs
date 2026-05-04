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
using System.Data.Entity;
using System.Linq;

using Rock.Attribute;
using Rock.Data;
using Rock.Enums.Controls;
using Rock.Model;
using Rock.Obsidian.UI;
using Rock.ViewModels.Blocks;
using Rock.ViewModels.Blocks.Connection.CelebrationsReport;
using Rock.ViewModels.Controls;
using Rock.ViewModels.Utility;
using Rock.Web.Cache;

namespace Rock.Blocks.Connection
{
    /// <summary>
    /// Displays a list of connection celebrations.
    /// </summary>

    [DisplayName( "Connection Celebrations Report" )]
    [Category( "Connection" )]
    [Description( "Displays a list of connection celebrations." )]
    [IconCssClass( "ti ti-trophy" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    [LinkedPage( "Connection Request Detail Page",
        Description = "The page that will show the connection request details.",
        Key = AttributeKey.ConnectionRequestDetailPage,
        IsRequired = false )]

    [CustomizedGrid]

    [Rock.SystemGuid.EntityTypeGuid( "B5C0F4D7-2A1E-4C8B-9F3D-6E0A7B2C5D9F" )]
    [Rock.SystemGuid.BlockTypeGuid( "8D3E5A9C-7B2F-4E1D-A6C0-3F9B8D2E5A7C" )]
    public class CelebrationsReport : RockEntityListBlockType<Note>
    {
        #region Keys

        private static class AttributeKey
        {
            public const string ConnectionRequestDetailPage = "ConnectionRequestDetailPage";
        }

        private static class NavigationUrlKey
        {
            public const string ConnectionRequestDetailPage = "ConnectionRequestDetailPage";
        }

        private static class PreferenceKey
        {
            public const string FilterDateRange = "filter-date-range";
            public const string FilterConnectionTypeGuid = "filter-connection-type-guid";
        }

        #endregion Keys

        #region Fields

        /// <summary>
        /// Lookup of ConnectionRequest by Id, populated in <see cref="GetListItems"/> and
        /// consumed by <see cref="GetGridBuilder"/> lambdas to avoid N+1 queries.
        /// </summary>
        private Dictionary<int, ConnectionRequest> _connectionRequestMap;

        /// <summary>
        /// Default date range applied when no preference is saved: last 8 months.
        /// </summary>
        private static readonly SlidingDateRangeBag DefaultDateRange = new SlidingDateRangeBag
        {
            RangeType = SlidingDateRangeType.Last,
            TimeUnit = TimeUnitType.Month,
            TimeValue = 8
        };

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the sliding date range used to filter results by connection request created date.
        /// Falls back to <see cref="DefaultDateRange"/> when no preference is stored.
        /// </summary>
        private SlidingDateRangeBag FilterDateRange => GetBlockPersonPreferences()
            .GetValue( PreferenceKey.FilterDateRange )
            .ToSlidingDateRangeBagOrNull() ?? DefaultDateRange;

        /// <summary>
        /// Gets the connection type Guid to filter results by.
        /// </summary>
        private Guid? FilterConnectionTypeGuid => GetBlockPersonPreferences()
            .GetValue( PreferenceKey.FilterConnectionTypeGuid )
            .FromJsonOrNull<ListItemBag>()?.Value?.AsGuidOrNull();

        #endregion Properties

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new ListBlockBox<CelebrationsReportOptionsBag>();
            var builder = GetGridBuilder();

            box.IsAddEnabled = false;
            box.IsDeleteEnabled = false;
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
        private CelebrationsReportOptionsBag GetBoxOptions()
        {
            var options = new CelebrationsReportOptionsBag
            {
                ConnectionTypes = ConnectionTypeCache.All()
                    .Where( ct => ct.IsActive && ct.EnabledFeatures.HasFlag( Rock.Enums.Connection.EnabledFeatureFlags.Celebration ) )
                    .OrderBy( ct => ct.Name )
                    .Select( ct => new ListItemBag { Text = ct.Name, Value = ct.Guid.ToString() } )
                    .ToList()
            };

            return options;
        }

        /// <summary>
        /// Gets the box navigation URLs required for the page to operate.
        /// </summary>
        /// <returns>A dictionary of key names and URL values.</returns>
        private Dictionary<string, string> GetBoxNavigationUrls()
        {
            return new Dictionary<string, string>
            {
                [NavigationUrlKey.ConnectionRequestDetailPage] = this.GetLinkedPageUrl( AttributeKey.ConnectionRequestDetailPage, new Dictionary<string, string>
                {
                    ["ConnectionRequestId"] = "((Key))"
                } )
            };
        }

        /// <inheritdoc/>
        protected override IQueryable<Note> GetListQueryable( RockContext rockContext )
        {
            var celebrationNoteTypeId = NoteTypeCache.Get( Rock.SystemGuid.NoteType.CELEBRATION_NOTE.AsGuid() ).Id;

            var noteQuery = new NoteService( rockContext ).Queryable()
                .Where( n => n.NoteTypeId == celebrationNoteTypeId && n.EntityId != null );

            var dateRange = FilterDateRange.ToActualDateRange();
            var connectionTypeGuid = FilterConnectionTypeGuid;

            var crQuery = new ConnectionRequestService( rockContext ).Queryable();

            if ( dateRange?.Start != null )
            {
                crQuery = crQuery.Where( cr => cr.CreatedDateTime >= dateRange.Start );
            }

            if ( dateRange?.End != null )
            {
                crQuery = crQuery.Where( cr => cr.CreatedDateTime < dateRange.End );
            }

            if ( connectionTypeGuid.HasValue )
            {
                crQuery = crQuery.Where( cr => cr.ConnectionOpportunity.ConnectionType.Guid == connectionTypeGuid.Value );
            }

            var validConnectionRequestIds = crQuery.Select( cr => cr.Id );
            noteQuery = noteQuery.Where( n => validConnectionRequestIds.Contains( n.EntityId.Value ) );

            return noteQuery
                .Include( n => n.EditedByPersonAlias.Person )
                .Include( n => n.CreatedByPersonAlias.Person );
        }

        /// <inheritdoc/>
        protected override List<Note> GetListItems( IQueryable<Note> queryable, RockContext rockContext )
        {
            var notes = base.GetListItems( queryable, rockContext );

            var connectionRequestIds = notes
                .Where( n => n.EntityId.HasValue )
                .Select( n => n.EntityId.Value )
                .ToList();

            _connectionRequestMap = new ConnectionRequestService( rockContext )
                .Queryable()
                .Include( cr => cr.PersonAlias.Person.ConnectionStatusValue )
                .Include( cr => cr.ConnectionOpportunity.ConnectionType )
                .Where( cr => connectionRequestIds.Contains( cr.Id ) )
                .ToDictionary( cr => cr.Id );

            return notes
                .Where( n => n.EntityId.HasValue && _connectionRequestMap.ContainsKey( n.EntityId.Value ) )
                .OrderByDescending( n => n.CreatedDateTime )
                .ToList();
        }

        /// <inheritdoc/>
        protected override GridBuilder<Note> GetGridBuilder()
        {
            return new GridBuilder<Note>()
                .WithBlock( this )
                .AddTextField( "idKey", n => n.IdKey )
                .AddDateTimeField( "date", n => GetConnectionRequest( n )?.CreatedDateTime )
                .AddPersonField( "requester", n => GetConnectionRequest( n )?.PersonAlias?.Person )
                .AddTextField( "type", n => GetConnectionRequest( n )?.ConnectionOpportunity?.ConnectionType?.Name )
                .AddTextField( "opportunity", n => GetConnectionRequest( n )?.ConnectionOpportunity?.Name )
                .AddTextField( "storyDetails", n => n.Text )
                .AddTextField( "storyAuthorName", n => n.EditedByPersonAlias?.Person?.FullName ?? n.CreatedByPersonAlias?.Person?.FullName )
                .AddTextField( "storyAuthorPersonAliasGuid", n => ( n.EditedByPersonAlias ?? n.CreatedByPersonAlias )?.Guid.ToString() )
                .AddTextField( "connectionRequestIdKey", n => GetConnectionRequest( n )?.IdKey );
        }

        /// <summary>
        /// Updates the celebration story text and author for the specified note.
        /// </summary>
        [BlockAction]
        public BlockActionResult SaveCelebrationStory( string key, string text, Guid? authorPersonAliasGuid )
        {
            using ( var rockContext = new RockContext() )
            {
                var note = new NoteService( rockContext ).Get( key, !PageCache.Layout.Site.DisablePredictableIds );

                if ( note == null )
                {
                    return ActionNotFound();
                }

                note.Text = text;
                note.EditedDateTime = RockDateTime.Now;

                if ( authorPersonAliasGuid.HasValue )
                {
                    var personAlias = new PersonAliasService( rockContext ).Get( authorPersonAliasGuid.Value );
                    note.EditedByPersonAliasId = personAlias?.Id;
                }
                else
                {
                    note.EditedByPersonAliasId = RequestContext.CurrentPerson?.PrimaryAliasId;
                }

                rockContext.SaveChanges();

                var savedAlias = note.EditedByPersonAliasId.HasValue
                    ? new PersonAliasService( rockContext ).Get( note.EditedByPersonAliasId.Value )
                    : null;

                return ActionOk( new
                {
                    storyAuthorName = savedAlias?.Person?.FullName ?? string.Empty,
                    storyAuthorPersonAliasGuid = savedAlias?.Guid.ToString() ?? string.Empty
                } );
            }
        }

        /// <summary>
        /// Returns the <see cref="ConnectionRequest"/> associated with the given <see cref="Note"/>
        /// using the pre-loaded <see cref="_connectionRequestMap"/>.
        /// </summary>
        /// <param name="note">The celebration note.</param>
        /// <returns>The associated <see cref="ConnectionRequest"/>, or <c>null</c> if not found.</returns>
        private ConnectionRequest GetConnectionRequest( Note note )
        {
            if ( !note.EntityId.HasValue )
            {
                return null;
            }

            _connectionRequestMap.TryGetValue( note.EntityId.Value, out var connectionRequest );
            return connectionRequest;
        }

        #endregion Methods
    }
}
