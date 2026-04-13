using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.ViewModels.Blocks.Finance.TransactionDetail
{
    /// <summary>
    /// 
    /// </summary>
    public class BatchNavigationBag
    {
        /// <summary>
        /// 
        /// </summary>
        public int? PreviousTransactionId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int? NextTransactionId { get; set; }
    }
}
