using CommandLine;

namespace nier_rein_code_diff
{
    class Options
    {
        [Option('i', "input", Required = true, HelpText = "The dump.cs to differentiate against.")]
        public string Input { get; set; }

        [Option('o', "output", Required = true, HelpText = "The folder of code to apply differences at. Its structure has to follow the namespaces of the dump.cs")]
        public string Output { get; set; }

        [Option('v',"verbose",Required = false,HelpText = "If all found differences and information should be printed to the console.")]
        public bool Verbose { get; set; }
    }
}
