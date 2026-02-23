using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.ViewModels.Blocks.Core.EntityTypes
{
    /// <summary>
    /// The bag of data that contains entity type data for the Entity Types block.
    /// </summary>
    public class EntityTypesBag
    {
        /// <summary>
        /// Id of Entity Type
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Name of entity type
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// friendly name of entity type
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// Represents whether the entity is commonly used or not
        /// </summary>
        public bool IsCommon { get; set; }
    }
}
