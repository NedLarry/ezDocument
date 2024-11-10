using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzDocument.Shared
{
    public static class FileHandler
    {
        public static async Task WriteLineToFileAsync(string content, StringWriter stringWriter)
        {
            await stringWriter.WriteLineAsync(content);
        }

        public static async Task WriteToFileAsync(string content, StringWriter stringWriter)
        {
            await stringWriter.WriteAsync(content);
        }
    }
}
