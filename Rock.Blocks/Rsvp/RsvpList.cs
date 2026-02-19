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
            var queryParams = new Dictionary<string, string>
            {
                ["OccurrenceId"] = "((Key))",
                ["GroupId"] = PageParameter( PageParameterKey.GroupId )
            };

            return new Dictionary<string, string>
            {
                [NavigationUrlKey.DetailPage] = this.GetLinkedPageUrl( AttributeKey.DetailPage, queryParams )
            };
        }

        /// <inheritdoc/>
        protected override IQueryable<RsvpListBag> GetListQueryable( RockContext rockContext )
        {
            var groupId = PageParameter( PageParameterKey.GroupId );
            var group = new GroupService( rockContext ).Get( groupId );

            var locationIds = new List<int>();
            var scheduleIds = new List<int>();

            if ( group == null )
            {
                return new List<RsvpListBag>().AsQueryable();
            }

            var occurrenceQry = new AttendanceOccurrenceService( rockContext ).Queryable().Where( o => o.GroupId == group.Id);

            var attendanceQry = new AttendanceService( rockContext ).Queryable();

            var qry = occurrenceQry.Select( o => new RsvpListBag
            {
                Id = o.Id,
                Name = o.Name,
                Date = o.OccurrenceDate,
                LocationId = o.LocationId ?? null,
                LocationName = o.Location != null ? o.Location.Name : "",
                ScheduleId = o.ScheduleId ?? null,
                ScheduleName =
                    o.Schedule != null
                        ? ( o.Schedule.Name != null && o.Schedule.Name != ""
                            ? o.Schedule.Name
                            : o.Schedule.Description )
                        : "",
                InvitedCount = o.Attendees.Count(),
                AcceptedCount = o.Attendees.Count( at => at.RSVP == RSVP.Yes ),
                DeclinedCount = o.Attendees.Count( at => at.RSVP == RSVP.No ),
                NoResponseCount = o.Attendees.Count( at => at.RSVP == RSVP.Unknown ),
                GroupTypeId = group.GroupTypeId
            } );

            return qry;
        }

        protected override List<RsvpListBag> GetListItems( IQueryable<RsvpListBag> queryable, RockContext rockContext )
        {
            // materialize the DB occurrences
            var data = queryable.ToList();

            // Get Occurrneces from Schedule that have not been added to DB yet
            if(data.Any(data => data.ScheduleId.HasValue ) )
            {
                var scheduleId = data.Where( d => d.ScheduleId.HasValue ).Select( d => d.ScheduleId.Value ).FirstOrDefault();
                var groupTypeId = data.Where( d => d.GroupTypeId != 0 ).Select( d => d.GroupTypeId ).FirstOrDefault();
                data.AddRange( GetScheduleOccurrences( data, scheduleId, groupTypeId ) );
            }

            var result = data.OrderByDescending(o => o.Date).ToList();
            return result;
        }

        /// <summary>
        /// Used to get all occurrences for a schedule that have not had any interactions and therefore have not been added to the database yet.
        /// This allows the block to show future occurrences that haven't had any interactions yet.
        /// Similar logic to GetGroupOccurrences in the AttendanceOccurrenceService, but rewritten for this block to avoid early materialization.
        /// </summary>
        /// <param name="existingOccurrences"></param>
        /// <returns></returns>
        private List<RsvpListBag> GetScheduleOccurrences(List<RsvpListBag> existingOccurrences, int scheduleId, int groupTypeId)
        {
            var newOccurrences = new List<RsvpListBag>();

            Schedule groupSchedule = null;
            groupSchedule = new ScheduleService( RockContext ).Get( scheduleId );

            if ( groupSchedule == null )
            {
                return newOccurrences;
            }

            var startDate = RockDateTime.Today.AddMonths( -2 );
            var endDate = RockDateTime.Today.AddDays( 1 );


            var existingDates = existingOccurrences
                .Where( o => o.ScheduleId.HasValue && o.ScheduleId == groupSchedule.Id )
                .Select( o => o.Date.Date )
                .Distinct()
                .ToHashSet();

            var possibleNewOccurrenceDates = new List<DateTime>();

            if ( !string.IsNullOrWhiteSpace( groupSchedule.iCalendarContent ) )
            {
                // If schedule has an iCal schedule, get all the past occurrences
                foreach ( var occurrence in groupSchedule.GetICalOccurrences( startDate, endDate ) )
                {
                    possibleNewOccurrenceDates.Add( occurrence.Period.StartTime.Date );
                }
            }
            else if ( groupSchedule.WeeklyDayOfWeek.HasValue )
            {
                var dt = startDate;

                while (dt.DayOfWeek != groupSchedule.WeeklyDayOfWeek.Value )
                {
                    dt = dt.AddDays( 1 );
                }

                if ( groupSchedule.WeeklyTimeOfDay.HasValue )
                {
                    dt = dt.Add( groupSchedule.WeeklyTimeOfDay.Value );
                }

                while ( dt < endDate )
                {
                    possibleNewOccurrenceDates.Add( dt );
                    dt = dt.AddDays( 7 );
                }
            }


            // filter exclusions
            var groupType = GroupTypeCache.Get( groupTypeId );
            var exclusions = groupType.GroupScheduleExclusions;
            foreach (var exclusion in exclusions )
            {
                if( !exclusion.Start.HasValue || !exclusion.End.HasValue )
                {
                    continue;
                }

                foreach( var occurrence in possibleNewOccurrenceDates.ToList() )
                {
                    if ( occurrence.Date >= exclusion.Start.Value.Date &&
                        occurrence.Date < exclusion.End.Value.Date.AddDays( 1 ))
                    {
                        possibleNewOccurrenceDates.Remove( occurrence );
                    }
                }
            }

            // Loop through schedule occurrences and add if they don't exist in the database
            foreach ( var date in possibleNewOccurrenceDates)
            {
                
                if ( !existingDates.Contains( date.Date ) )
                {
                    newOccurrences.Add( new RsvpListBag
                    {
                        Id = 0,
                        Name = string.Empty,
                        Date = date,
                        LocationId = null,
                        LocationName = string.Empty,
                        ScheduleId = groupSchedule.Id,
                        ScheduleName =
                            groupSchedule != null
                                ? ( groupSchedule.Name != null && groupSchedule.Name != ""
                                    ? groupSchedule.Name
                                    : groupSchedule.ToString() )
                                : "",
                        InvitedCount = 0,
                        AcceptedCount = 0,
                        DeclinedCount = 0,
                        NoResponseCount = 0
                    } );
                }
            }
            return newOccurrences;
        }

        /// <inheritdoc/>
        protected override GridBuilder<RsvpListBag> GetGridBuilder()
        {
            var groupId = PageParameter( PageParameterKey.GroupId );
            return new GridBuilder<RsvpListBag>()
                .WithBlock( this )
                .AddTextField( "keyField", a => a.Id == 0 || a.Id == null ? $"{a.Date}_{a.ScheduleId ?? 0}_{a.LocationId ?? 0}" : a.Id.AsIdKey() )
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
                .AddField( "noResponse", a => a.NoResponseCount )
                .AddField( "acceptedPercentage", a => a.AcceptedPercentage )
                .AddField( "declinedPercentage", a => a.DeclinedPercentage );
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

            //If this occurrence has already been created, just return the existing one.
            var id = attendanceOccurrenceService.Queryable()
            .Where( o => o.OccurrenceDate == occurrenceDate )
            .Where( o => groupId == null || o.GroupId == group.Id )
            .Where( o => scheduleId == null || o.ScheduleId == scheduleId )
            .Where( o => locationId == null || o.LocationId == locationId )
            .Select( o => o.Id )
            .FirstOrDefault();

            if ( id != 0 )
            {
                return ActionOk( id );
            }

            var attendanceOccurrence = new AttendanceOccurrence();

            if ( group.Id != null )
            {
                attendanceOccurrence.GroupId = group.Id;
            }

            if ( scheduleId != null && scheduleId != 0 )
            {
                attendanceOccurrence.ScheduleId = scheduleId;
            }
            if ( locationId != null && locationId != 0 )
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

