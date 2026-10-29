using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Compiler.Tests;

internal static class CompilerTestHelper
{
    public static VeloraCompilation Compile(string source, string filePath = "story.vel")
    {
        SyntaxTree syntaxTree = SyntaxTree.ParseText(SourceText.From(source, filePath));
        return VeloraCompiler.Compile(SemanticModel.Create(syntaxTree));
    }

    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "scripts", "example.vel")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Velora repository root.");
    }
}
