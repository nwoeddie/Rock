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
using System.ComponentModel;

using Rock.Attribute;
using Rock.ViewModels.Blocks.Reporting.CalendarDimensionSettings;

namespace Rock.Blocks.Reporting
{
    [DisplayName( "Calendar Dimension Settings" )]
    [Category( "Reporting" )]
    [Description( "Helps configure and generate the AnalyticsSourceDate table for BI Analytics" )]
    [IconCssClass( "ti ti-calendar" )]
    [SupportedSiteTypes( Model.SiteType.Web )]

    [SystemGuid.EntityTypeGuid( "df6324dc-1e42-4556-b337-d4e416868a01" )]
    [Rock.SystemGuid.BlockTypeGuid( "a4c6b9df-5034-40f6-958a-342921142ffb" )]
    internal class CalendarDimensionSettings : RockBlockType
    {
        #region Methods

        /// <inheritdoc/>
        public override object GetObsidianBlockInitialization()
        {
            var bag = new CalendarDimensionSettingsBag
            {
                StartDate = Rock.Web.SystemSettings.GetValue( Rock.SystemKey.SystemSetting.ANALYTICS_CALENDAR_DIMENSION_START_DATE ).AsDateTime()
                    ?? new DateTime( RockDateTime.Today.AddYears( -150 ).Year, 1, 1 ),
                EndDate = Rock.Web.SystemSettings.GetValue( Rock.SystemKey.SystemSetting.ANALYTICS_CALENDAR_DIMENSION_END_DATE ).AsDateTime()
                    ?? new DateTime( RockDateTime.Today.AddYears( 101 ).Year, 1, 1 ).AddDays( -1 ),
                FiscalStartMonth = Rock.Web.SystemSettings.GetValue( Rock.SystemKey.SystemSetting.ANALYTICS_CALENDAR_DIMENSION_FISCAL_START_MONTH ).AsIntegerOrNull() ?? 1,
                IsGivingMonthUseSundayDate = Rock.Web.SystemSettings.GetValue( Rock.SystemKey.SystemSetting.ANALYTICS_CALENDAR_DIMENSION_GIVING_MONTH_USE_SUNDAY_DATE ).AsBoolean()
            };

            return bag;
        }

        #endregion
    }
}
