using EzDocument.Shared;
using System.Reflection.Metadata;

Console.WriteLine("Tinkering...");

Console.WriteLine(DocumentWriter.WriteItalics("italicized"));
Console.WriteLine(DocumentWriter.WriteBlockQuote("Blockquote"));
Console.WriteLine(DocumentWriter.WriteHeader("Header", 3));