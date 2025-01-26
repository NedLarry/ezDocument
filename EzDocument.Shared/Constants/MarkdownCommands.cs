namespace EzDocument.Shared.Constants
{
    public static class MarkdownCommands
    {
        public static string ITALIC { get; set; } = "_";
        public static string BOLD { get; set; } = "**";
        public static string HEADER { get; set; } = "#";
        public static char CHAR_HEADER { get; set; } = '#';
        public static string BLOCKQUOTE { get; set; } = ">";
        public static string LINK { get; set; } = "[textToReplace](urlToReplace)";
        public static string IMAGES { get; set; } = "![textToReplace](urlToReplace)";
        public static string CODE { get; set; } = "```";
    }
}
