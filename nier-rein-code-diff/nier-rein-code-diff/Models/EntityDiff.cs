using System.Collections.Generic;

namespace nier_rein_code_diff.Models
{
    class EntityDiff
    {
        public string Name { get; set; }
        public IList<EntityFieldDiff> FieldDiff { get; } = new List<EntityFieldDiff>();
    }
}
