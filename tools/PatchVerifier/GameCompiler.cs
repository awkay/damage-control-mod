using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace PatchVerifier;

/// <summary>
/// Compiles the mod the way the game does: Roslyn 2.10.0 (same version the game ships), the
/// loader's reference list, define symbols, and the RoslynCSharp settings asset's language
/// version / warning level / optimization / platform. AllowUnsafe follows NativeAccess.
/// </summary>
public static class GameCompiler
{
    public static bool Compile(GameEnvironment env, RoslynSettingsAsset settings, string modDir, ManifestCheck.Result manifest,
        string outputDll, Report report)
    {
        report.Section("game-exact compile (Roslyn " + typeof(CSharpCompilation).Assembly.GetName().Version + ")");
        string source = Path.Combine(modDir, "Source");
        string[] files = Directory.Exists(source)
            ? Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        if (files.Length == 0)
        {
            report.Error("no Source/**/*.cs to compile");
            return false;
        }

        var refs = env.ResolveLoaderReferences(manifest.Permissions, settings, report);
        report.Info("references: " + string.Join(" ", refs.Select(Path.GetFileName)));
        var langVersion = (LanguageVersion)settings.LanguageVersion;
        report.Info($"language version {langVersion} (effective {langVersion.MapSpecifiedToEffectiveVersion()}), defines {string.Join(";", LoaderModel.DefineSymbols)}, warning level {settings.WarningLevel}");

        bool unsafeAllowed = (manifest.Permissions & LoaderModel.SecurityOptions.NativeAccess) != 0;
        bool ok = CompileFiles(manifest.AssemblyName, files, refs, settings, unsafeAllowed, outputDll, modDir, report, "mod");

        // Core purity: Source/Core must compile alone against the base class library only.
        string core = Path.Combine(source, "Core");
        string[] coreFiles = Directory.Exists(core) ? Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories) : Array.Empty<string>();
        if (coreFiles.Length > 0)
        {
            report.Section("Source/Core purity (compiled alone against mscorlib, System, System.Core)");
            var bcl = new[] { "mscorlib", "System", "System.Core" }.Select(env.Dll).ToList();
            bool coreOk = CompileFiles("damage-control-core-check", coreFiles, bcl, settings, false, null, modDir, report, "core");
            if (coreOk) report.Ok($"{coreFiles.Length} Core files reference no UnityEngine, game or Harmony types");
            else report.Error("Source/Core must not depend on UnityEngine, game assemblies, Harmony or non-Core mod code");
            ok &= coreOk;
        }
        return ok;
    }

    private static bool CompileFiles(string assemblyName, string[] files, List<string> refs, RoslynSettingsAsset settings,
        bool allowUnsafe, string outputDll, string modDir, Report report, string label)
    {
        var parse = new CSharpParseOptions((LanguageVersion)settings.LanguageVersion, DocumentationMode.Parse, SourceCodeKind.Regular,
            LoaderModel.DefineSymbols);
        var trees = files.Select(f =>
        {
            using var stream = File.OpenRead(f);
            return CSharpSyntaxTree.ParseText(SourceText.From(stream, Encoding.UTF8), parse, f);
        }).ToArray();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: settings.AllowOptimizeCode ? OptimizationLevel.Release : OptimizationLevel.Debug,
            checkOverflow: false,
            allowUnsafe: allowUnsafe,
            platform: (Platform)settings.TargetPlatform,
            generalDiagnosticOption: ReportDiagnostic.Default,
            warningLevel: settings.WarningLevel,
            concurrentBuild: settings.AllowConcurrentCompile);
        var compilation = CSharpCompilation.Create(assemblyName, trees, refs.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r)), options);

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        // CS1701/CS1702 (mscorlib 2.0 vs 4.0 unification) are emitted for every game assembly and are
        // harmless; the game's compile produces them too. Summarize instead of listing each one.
        int unification = result.Diagnostics.Count(d => d.Id is "CS1701" or "CS1702");
        if (unification > 0) report.Info($"[{label}] {unification} CS1701/CS1702 assembly-unification warnings suppressed (benign, also emitted in game)");
        foreach (var d in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning && d.Id is not ("CS1701" or "CS1702")).OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            string where = d.Location.IsInSource
                ? $"{Path.GetRelativePath(modDir, d.Location.SourceTree.FilePath)}({d.Location.GetLineSpan().StartLinePosition.Line + 1},{d.Location.GetLineSpan().StartLinePosition.Character + 1})"
                : "";
            string msg = $"[{label}] {where}: {d.Id}: {d.GetMessage()}";
            if (d.Severity == DiagnosticSeverity.Error) report.Error(msg);
            else report.Warn(msg);
        }
        if (!result.Success) return false;
        if (outputDll != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputDll))!);
            File.WriteAllBytes(outputDll, ms.ToArray());
            report.Ok($"compiled {files.Length} files -> {outputDll}");
        }
        return true;
    }
}
