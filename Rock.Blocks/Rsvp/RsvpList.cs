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
using Rock.ViewModels.Blocks.Rsvp.RsvpList;
using Rock.Web.Cache;

namespace Rock.Blocks.Rsvp
{
    /// <summary>
    /// Displays a list of people.
    /// </summary>

    [DisplayName( "Rsvp List" )]
    [Category( "Rsvp" )]
    [Description( "Displays a list of people." )]
    [IconCssClass( "fa fa-list" )]
    // [SupportedSiteTypes( Model.SiteType.Web )]

    [LinkedPage( "Detail Page",
        Description = "The page that will show the rsvp details.",
        Key = AttributeKey.DetailPage )]

    [Rock.SystemGuid.EntityTypeGuid( "1ef2847c-137d-41f2-80b3-d4aa8d9f7790" )]
    [Rock.SystemGuid.BlockTypeGuid( "57189fa8-ab29-4a66-8c52-392dff6cb91a" )]
    [CustomizedGrid]
    public class RsvpList : RockListBlockType<RsvpListBag>
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

        private static class PageParameterKey
        {
            public const string GroupId = "GroupId";
        }

        #endregion Keys

        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var box = new ListBlockBox<RsvpListOptionsBag>();
            var builder = GetGridBuilder();

            box.IsAddEnabled = GetIsAddEnabled();
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
        private RsvpListOptionsBag GetBoxOptions()
        {
            var options = new RsvpListOptionsBag();

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
                [NavigationUrlKey.DetailPage] = this.GetLinkedPageUrl( AttributeKey.DetailPage, "RsvpId", "((Key))" )
            };
        }

        /// <inheritdoc/>
        protected override IQueryable<RsvpListBag> GetListQueryable( RockContext rockContext )
        {
            var groupId = PageParameter( PageParameterKey.GroupId );
            var group = new GroupService( rockContext ).Get( groupId );
            var locationIds = new List<int>();
            var scheduleIds = new List<int>();

            if(group == null )
            {
                return new List<RsvpListBag>().AsQueryable();
            }

            var attendanceOccurrences = new AttendanceOccurrenceService( rockContext )
                .GetGroupOccurrences( group, null, null, locationIds, scheduleIds )
                .Select( a => new RsvpListBag
                {
                    IdKey = a.Id.AsIdKey(),
                    Id = a.Id,
                    Name = a.Name,
                    Date = a.OccurrenceDate,
                    LocationName = a.Location != null ? a.Location.Name : "",
                    ScheduleName = a.Schedule != null
                        ? ( a.Schedule.Name.IsNotNullOrWhiteSpace()
                            ? a.Schedule.Name
                            : a.Schedule.ToString() )
                        : "",

                    InvitedCount = a.Attendees.Count(),
                    AcceptedCount = a.Attendees.Count( at => at.RSVP == RSVP.Yes ),
                    DeclinedCount = a.Attendees.Count( at => at.RSVP == RSVP.No ),
                    NoResponseCount = a.Attendees.Count( at => at.RSVP == RSVP.Unknown )

                } ).AsQueryable();

            return attendanceOccurrences;
        }

        /// <summary>
        /// Gets the queryable that will have proper default ordering applied.
        /// </summary>
        /// <param name="queryable">The queryable to have ordering applied to it.</param>
        /// <param name="rockContext">The rock context that will be used to access the database.</param>
        /// <returns>A <see cref="IQueryable{T}"/> that will return results in the desired order.</returns>
        protected override IQueryable<RsvpListBag> GetOrderedListQueryable( IQueryable<RsvpListBag> queryable, RockContext rockContext )
        {
            return queryable.OrderByDescending( c => c.Date);
        }

        /// <inheritdoc/>
        protected override GridBuilder<RsvpListBag> GetGridBuilder()
        {
            return new GridBuilder<RsvpListBag>()
                .WithBlock( this )
                .AddTextField( "idKey", a => a.IdKey == "0" || a.IdKey == null ? new System.Guid().ToString() : a.IdKey )
                .AddField( "id", a => a.Id )
                .AddDateTimeField( "date", a => a.Date )
                .AddTextField( "name", a => a.Name )
                .AddField( "scheduleId", a => a.ScheduleId )
                .AddTextField( "schedule", a => a.ScheduleName )
                .AddField( "locationId", a => a.LocationId )
                .AddTextField( "location", a => a.LocationName )
                .AddField( "invited", a => a.InvitedCount )
                .AddField( "accepted", a => a.AcceptedCount )
                .AddField( "declined", a => a.DeclinedCount )
                .AddField( "noResponse", a => a.NoResponseCount );
        }

        #endregion

        #region Block Actions


        [BlockAction( "GetOccurrenceId" )]
        public BlockActionResult GetOccurrenceID( GetOccurrenceIdBag bag )
        {
            var groupId = PageParameter( PageParameterKey.GroupId );
            var occurrenceDate = bag.OccurrenceDate;
            var locationId = bag.LocationId;
            var scheduleId = bag.ScheduleId;

            var group = new GroupService( new RockContext() ).Get( groupId );
            var attendanceOccurrenceService = new AttendanceOccurrenceService( RockContext );

            var attendanceOccurrence = new AttendanceOccurrence();

            if ( group.Id != null )
            {
                attendanceOccurrence.GroupId = group.Id;
            }
            if (scheduleId != null && scheduleId != 0)
            {
                attendanceOccurrence.ScheduleId = scheduleId;
            }
            if (locationId != null && locationId != 0)
            {
                attendanceOccurrence.LocationId = locationId;
            }
            attendanceOccurrence.OccurrenceDate = occurrenceDate;
            attendanceOccurrenceService.Add( attendanceOccurrence );
            RockContext.SaveChanges();
            return ActionOk( attendanceOccurrence.Id );
        }

        #endregion
    }
}

