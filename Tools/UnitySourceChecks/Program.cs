using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string[] files = Directory.GetFiles(Path.Combine(root, "Assets", "Cellblock"), "*.cs", SearchOption.AllDirectories);
int errors = 0;
foreach (string file in files)
{
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.CSharp9), file);
    foreach (Diagnostic diagnostic in syntax.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
    { Console.Error.WriteLine(diagnostic); errors++; }
}
Console.WriteLine($"Parsed {files.Length} Unity C# files; {errors} syntax errors. Unity API/type checking and editor execution are not performed here.");
Environment.ExitCode = errors == 0 ? 0 : 1;
