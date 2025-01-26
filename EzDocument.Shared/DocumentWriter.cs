using EzDocument.Shared.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzDocument.Shared
{
    public static class DocumentWriter
    {
        public static string WriteItalics(string text)
        {
            return $"{MarkdownCommands.ITALIC}{ text }{MarkdownCommands.ITALIC}  ";
        }

        public static string WriteBold(string text)
        {
            return $"{MarkdownCommands.BOLD}{ text }{MarkdownCommands.BOLD}  ";
        }

        public static string WriteBlockQuote(string text)
        {
            return $"{MarkdownCommands.BLOCKQUOTE} { text }  ";
        }

        public static string WriteCode(string text)
        {
            return $"{MarkdownCommands.CODE} { text } {MarkdownCommands.CODE}  ";
        }

        public static string WriteHeader(string text, int headerLevel)
        {
            if (headerLevel >= 6)
            {
                headerLevel = 6 ;
            }

            var headers = new String (MarkdownCommands.CHAR_HEADER, headerLevel);

            return $"{headers} { text }  ";
        }
    }
}
