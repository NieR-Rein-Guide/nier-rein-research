namespace nier_rein_code_diff
{
    class Program
    {
        static void Main(string[] args)
        {
            var dumpCsPath = @"D:\Users\Kirito\Desktop\NierRein\RE\tools\Il2CppDumper\workspace_ww_2.11.0\dump.cs";
            var masterDbPath = @"D:\Users\Kirito\Desktop\NierRein\Projects\nier-rein-apps\nier-rein-api";

            var differ = new DatabaseDiffer(dumpCsPath, masterDbPath);
            var diff = differ.CreateDiff();

            diff.Print();

            var applier = new PatchApplier(dumpCsPath, masterDbPath);
            applier.Apply(diff);
        }
    }
}
