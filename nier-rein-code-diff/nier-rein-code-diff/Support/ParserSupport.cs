using System;
using System.IO;
using nier_rein_code_diff.Compilation;
using nier_rein_code_diff.Extensions;

namespace nier_rein_code_diff.Support
{
    static class ParserSupport
    {
        public static SyntaxNode ParseFile(string file)
        {
            using var ownParser = new CsParser(NodeReader.FromFile(file));
            return ownParser.Parse();
        }

        public static SyntaxNode ParseClass(string file, Func<string,int> findIndex)
        {
            var dumpText = File.ReadAllText(file);
            var masterIndex = findIndex(dumpText);

            var reader = new StringReader(dumpText);
            reader.SetPosition(masterIndex);

            var nodeReader = new NodeReader(new Lexer(reader));
            using var parser = new CsParser(nodeReader);

            return parser.ResolveClass();
        }

        public static Func<string, int> FindIndexByString(string indexOfIndicator)
        {
            return s => s.IndexOf(indexOfIndicator, StringComparison.Ordinal);
        }
    }
}
