using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GameLink.Tests.Shared;

internal static class CoreSourceCompiler
{
    private static readonly Lazy<Type> ManagerType = new(Compile);

    public static dynamic Create() => Activator.CreateInstance(ManagerType.Value)
        ?? throw new InvalidOperationException("Could not construct headless ProfileManager.");

    private static Type Compile()
    {
        string root = AppContext.BaseDirectory;
        string manager = Extract(Path.Combine(root, "CoreSource", "ProfileManager.cs"),
            "CalculateComponent", "CalculateOutput", "DoConditions", "DoMaths", "Deadzone", "AntiRollover");
        string helpers = Extract(Path.Combine(root, "CoreSource", "Helpers.cs"),
            "Clamp01", "Lerp", "ClampBetween", "Clamp", "NormalizeAngle", "ToDegree", "ToRadian");
        string facade = File.ReadAllText(Path.Combine(root, "HeadlessCore.cs.txt"));
        string source = facade.Replace("/* PROFILE_METHODS */", manager)
            .Replace("/* HELPER_METHODS */", helpers);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Append(typeof(YawGLAPI.Profile_Component).Assembly.Location)
            .Distinct().Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("HeadlessGameLinkCore",
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException("Attached Core methods did not compile:\n"
                + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray()).GetType("GameLink.Core.ProfileManager", throwOnError: true)!;
    }

    private static string Extract(string path, params string[] names)
    {
        var methods = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        return string.Join("\n", names.Select(name =>
            methods.Single(m => m.Identifier.ValueText == name).WithoutTrivia().ToFullString()));
    }
}
