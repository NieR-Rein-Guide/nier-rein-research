using System.IO;
using System.Reflection;

namespace nier_rein_code_diff.Extensions
{
    static class StringReaderExtensions
    {
        public static void SetPosition(this StringReader reader, int pos)
        {
            reader
                .GetType()
                .GetField("_pos", BindingFlags.NonPublic | BindingFlags.Instance)?
                .SetValue(reader, pos);
        }
    }
}
