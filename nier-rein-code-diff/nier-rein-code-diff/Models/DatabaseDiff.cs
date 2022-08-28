using System;
using System.Collections.Generic;

namespace nier_rein_code_diff.Models
{
    class DatabaseDiff
    {
        public IList<TableDiff> TableDiff { get; } = new List<TableDiff>();
        public IList<EntityDiff> EntityDiff { get; } = new List<EntityDiff>();

        public void Print()
        {
            PrintTableDiff();
            PrintEntityDiff();
        }

        private void PrintTableDiff()
        {
            Console.WriteLine("Table offsets:");
            foreach (var field in TableDiff)
                switch (field.Type)
                {
                    case DiffType.Changed:
                        Console.WriteLine($"  {field.DumpFieldInfo.Name} 0x{field.OwnFieldInfo.Offset:X} -> 0x{field.DumpFieldInfo.Offset:X}");
                        break;

                    case DiffType.New:
                        Console.WriteLine($"  +{field.DumpFieldInfo.Name} 0x{field.DumpFieldInfo.Offset:X}");
                        break;

                    case DiffType.Removed:
                        Console.WriteLine($"  -{field.OwnFieldInfo.Name}");
                        break;
                }
        }

        private void PrintEntityDiff()
        {
            foreach (var changed in EntityDiff)
            {
                if (changed.FieldDiff.Count <= 0)
                    continue;

                Console.WriteLine($"{changed.Name}");
                foreach (var field in changed.FieldDiff)
                    switch (field.Type)
                    {
                        case DiffType.Changed:
                            Console.WriteLine($"  {field.DumpFieldInfo.Name} 0x{field.OwnFieldInfo.Offset:X} -> 0x{field.DumpFieldInfo.Offset:X}");
                            break;

                        case DiffType.New:
                            Console.WriteLine($"  +{field.DumpFieldInfo.Name} 0x{field.DumpFieldInfo.Offset:X}");
                            break;

                        case DiffType.Removed:
                            Console.WriteLine($"  -{field.OwnFieldInfo.Name}");
                            break;
                    }

                Console.WriteLine();
            }
        }
    }
}
