using System.Runtime.CompilerServices;
using Trivial.CodeSecurity;

namespace PatchVerifier;

/// <summary>
/// Runs the game's own Trivial.CodeSecurity engine over the compiled mod with the restriction
/// set the loader ends up with: the RoslynCSharpSettings asset's assembly/type/member rules,
/// namespace rules replaced by Mod.SetupSecurity for the manifest permissions, ignoreCase=true.
///
/// Why this matters: the loader does NOT security-check the assembly on the launch that
/// (re)compiles it (ScriptDomain.CompileFiles skips the check). It checks on every later launch,
/// when it loads the cached Temp/&lt;assemblyName&gt;.dll via ScriptDomain.LoadAssembly. A violation
/// therefore shows up as "works once, then silently never loads again".
/// </summary>
public static class SecurityCheck
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run(RoslynSettingsAsset settings, LoaderModel.SecurityOptions permissions, string dll, Report report)
    {
        report.Section("code security (Trivial.CodeSecurity from the game, loader restriction set)");
        if (!settings.SecurityCheckCode)
            report.Info("asset has securityCheckCode=false; the loader still checks cached assemblies only when it is true");

        var r = new CodeSecurityRestrictions();
        Copy(settings.AssemblyReferences, r.AssemblyReferences);
        Copy(settings.TypeReferences, r.TypeReferences);
        Copy(settings.MemberReferences, r.MemberReferences);
        // Mod.SetupSecurity: clear namespace lists, default Allow, then add the deny entries.
        r.NamespaceReferences.DefaultBehaviour = CodeSecurityRestrictions.CodeSecurityBehaviour.Allow;
        foreach (string ns in LoaderModel.DeniedNamespaceEntries(permissions))
            r.NamespaceReferences.AddEntryName(ns, CodeSecurityRestrictions.CodeSecurityBehaviour.Deny);
        r.IgnoreCase = true;
        report.Info("denied namespaces (exact match; \".*\" does not cover sub-namespaces in this engine): "
                    + string.Join(", ", r.NamespaceReferences.DenyEntries));
        report.Info("denied members: " + string.Join(", ", r.MemberReferences.DenyEntries) + "; denied types: " + string.Join(", ", r.TypeReferences.DenyEntries)
                    + "; denied assemblies: " + string.Join(", ", r.AssemblyReferences.DenyEntries));

        using var engine = new CodeSecurityEngine(File.ReadAllBytes(dll));
        bool ok = engine.SecurityCheckAssembly(r, out CodeSecurityReport result);
        if (ok)
        {
            report.Ok("assembly passes the loader's security check");
            return;
        }
        foreach (string line in result.GetAllLines(true))
            if (!string.IsNullOrWhiteSpace(line))
                report.Error("security: " + line.Trim());
        report.Error("the game would refuse to load the cached assembly on the next launch (see above); "
                     + "declare the needed permission in Manifest.json or avoid the API");
    }

    private static void Copy(RoslynSettingsAsset.Entry from, CodeSecurityRestrictionsEntry to)
    {
        to.AllowEntries.Clear();
        to.DenyEntries.Clear();
        foreach (string s in from.Allow) to.AddEntryName(s, CodeSecurityRestrictions.CodeSecurityBehaviour.Allow);
        foreach (string s in from.Deny) to.AddEntryName(s, CodeSecurityRestrictions.CodeSecurityBehaviour.Deny);
        to.DefaultBehaviour = (CodeSecurityRestrictions.CodeSecurityBehaviour)from.DefaultBehaviour;
    }
}
