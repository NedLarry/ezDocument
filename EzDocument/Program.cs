using System.Reflection;
using EzDocument;

const string usage = """
    Usage: ezdocument [source-directory] [options]

    Scans C# source for ASP.NET Core endpoints (controllers and minimal APIs) and writes
    Markdown documentation, including which endpoints need a request body or parameters.

    Arguments:
      source-directory      Folder to scan (default: current directory)

    Options:
      -o, --output <dir>    Output folder (default: <source-directory>/api-docs).
                            The folder gets its own .gitignore so it is never committed.
      -f, --force           Write into an existing output folder not created by EzDocument
      -v, --version         Print the tool version
      -h, --help            Show this help
    """;

string? sourceDirectory = null;
string? outputDirectory = null;
var force = false;

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "-h" or "--help" or "-?":
            Console.WriteLine(usage);
            return 0;
        case "-v" or "--version":
            Console.WriteLine(typeof(DocumentationGenerator).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown");
            return 0;
        case "-f" or "--force":
            force = true;
            continue;
        case "-o" or "--output":
            if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
            {
                return Fail("The --output option requires a directory.");
            }

            outputDirectory = args[++index];
            continue;
    }

    if (args[index].StartsWith('-'))
    {
        return Fail($"Unknown option: {args[index]}");
    }

    if (sourceDirectory is not null)
    {
        return Fail("Only one source directory can be specified.");
    }

    sourceDirectory = args[index];
}

try
{
    var root = Path.GetFullPath(sourceDirectory ?? Directory.GetCurrentDirectory());
    var output = outputDirectory is null
        ? Path.Combine(root, DocumentationGenerator.DefaultOutputFolder)
        : Path.GetFullPath(outputDirectory);

    var result = new DocumentationGenerator().Generate(root, output, force);
    Console.WriteLine($"Documented {result.EndpointCount} endpoint(s) across {result.GroupCount} controller/minimal-API file(s).");
    Console.WriteLine($"Output: {result.OutputDirectory} (git-ignored)");
    foreach (var file in result.Files)
    {
        Console.WriteLine($"  {file}");
    }

    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Documentation generation failed: {exception.Message}");
    return 1;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("Run 'ezdocument --help' for usage.");
    return 2;
}
