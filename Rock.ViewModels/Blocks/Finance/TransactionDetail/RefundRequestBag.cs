using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.ViewModels.Utility;

namespace Rock.ViewModels.Blocks.Finance.TransactionDetail
{
    /// <summary>
    /// 
    /// </summary>
    public class RefundRequestBag
    {
        /// <summary>
        /// 
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public decimal MaximumAmount { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ListItemBag RefundReason { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string RefundReasonSummary { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool Process { get; set; }
    }
}
