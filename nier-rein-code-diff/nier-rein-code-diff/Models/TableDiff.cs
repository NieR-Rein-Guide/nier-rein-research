using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nier_rein_code_diff.Models
{
    class TableDiff
    {
        public DiffType Type { get; set; }
        public FieldInfo DumpFieldInfo { get; set; }
        public FieldInfo OwnFieldInfo { get; set; }
    }
}
