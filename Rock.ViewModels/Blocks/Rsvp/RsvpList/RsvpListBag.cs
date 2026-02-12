using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.ViewModels.Blocks.Rsvp.RsvpList
{
    public class RsvpListBag
    {
        /// <summary>
        /// Gets or sets the IdKey.
        /// </summary>
        /// <value>
        /// The Queue Type IdKey.
        /// </value>
        public string IdKey { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Name of RSVP
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Date of RSVP Event
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Id for location used to create occurrence if doesn't exist.
        /// </summary>
        public int LocationId { get; set; }
        /// <summary>
        /// Name of the Location Event is being held at
        /// </summary>
        public string LocationName { get; set; }

        /// <summary>
        /// ID used for schedule used to create occurrence if doesn't exist.
        /// </summary>
        public int ScheduleId { get; set; }

        /// <summary>
        /// Name of the Schedule associated with the RSVP
        /// </summary>
        public string ScheduleName { get; set; }

        /// <summary>
        /// Number of people invited to the event
        /// </summary>
        public int InvitedCount { get; set; }

        /// <summary>
        /// Number of people that have accepted the invitation to the event
        /// </summary>
        public int AcceptedCount { get; set; }

        /// <summary>
        /// Number of people that have declined the invitation to the event
        /// </summary>
        public int DeclinedCount { get; set; }

        /// <summary>
        /// Number of people invited but not responded to the invitation to the event
        /// </summary>
        public int NoResponseCount { get; set; }

        /// <summary>
        /// Get the percentage of people that accepted the invitation to the event. Rounded to the nearest whole number.
        /// </summary>
        public int AcceptedPercentage
        {
            get
            {
                if ( InvitedCount == 0 )
                {
                    return 0;
                }
                return ( int ) ( Math.Round( ( decimal ) AcceptedCount / InvitedCount, 2 ) * 100 );
            }
        }

        /// <summary>
        /// Get the percentage of people that declined the invitation to the event. Rounded to the nearest whole number.
        /// </summary>
        public int DeclinedPercentage
        {
            get
            {
                if ( InvitedCount == 0 )
                {
                    return 0;
                }
                return ( int ) ( Math.Round( ( decimal ) DeclinedCount / InvitedCount, 2 ) * 100 );
            }
        }

        /// <summary>
        /// Get the percentage of people that have not responded to the invitation to the event. Rounded to the nearest whole number.
        /// </summary>
        public int UnknownPercentage
        {
            get
            {
                return 100 - AcceptedPercentage - DeclinedPercentage;
            }
        }

    }

}
