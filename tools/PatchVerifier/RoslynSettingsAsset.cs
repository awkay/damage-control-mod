using System.Text;

namespace PatchVerifier;

/// <summary>
/// Reads the serialized RoslynCSharp settings ScriptableObject ("RoslynCSharpSettings") out of
/// the game's resources.assets. The mod loader only overrides parts of it (define symbols,
/// reference assemblies, namespace rules, unsafe); everything else comes from this asset:
/// language version, warning level, optimization, extra references by name, and the
/// assembly / type / member security rules (e.g. the UnityEngine.Object.Instantiate ban).
///
/// Layout follows the field order of RoslynCSharp.RoslynCSharp and
/// Trivial.CodeSecurity.CodeSecurityRestrictions(Entry); Unity serializes int32 little endian,
/// strings as int32 length + UTF-8 bytes padded to 4, bools padded to 4, lists as int32 count.
/// </summary>
public sealed class RoslynSettingsAsset
{
    public sealed class Entry
    {
        public List<string> Allow = new();
        public List<string> Deny = new();
        public int DefaultBehaviour; // 0 Allow, 1 Deny
        public override string ToString() =>
            $"allow=[{string.Join(", ", Allow)}] deny=[{string.Join(", ", Deny)}] default={(DefaultBehaviour == 0 ? "Allow" : "Deny")}";
    }

    public bool SecurityCheckCode;
    public Entry AssemblyReferences, NamespaceReferences, TypeReferences, MemberReferences;
    public bool IgnoreCase;
    public int LogDetail;
    public bool AllowUnsafeCode, AllowOptimizeCode, AllowConcurrentCompile, GenerateInMemory, GenerateSymbols;
    public int WarningLevel;
    public int LanguageVersion; // Microsoft.CodeAnalysis.CSharp.LanguageVersion value; 0 = Default
    public int TargetPlatform;  // Microsoft.CodeAnalysis.Platform value; 0 = AnyCpu
    public List<string> References = new();
    public List<string> DefineSymbols = new();

    public static RoslynSettingsAsset Load(string resourcesAssetsPath)
    {
        byte[] data = File.ReadAllBytes(resourcesAssetsPath);
        byte[] name = Encoding.ASCII.GetBytes("RoslynCSharpSettings");
        int found = -1;
        for (int i = 4; i <= data.Length - name.Length; i++)
        {
            if (data[i] != name[0] || BitConverter.ToInt32(data, i - 4) != name.Length) continue;
            if (data.AsSpan(i, name.Length).SequenceEqual(name)) { found = i; break; }
        }
        if (found < 0) throw new InvalidDataException("RoslynCSharpSettings object not found in " + resourcesAssetsPath);

        var r = new Reader(data, found + name.Length);
        var s = new RoslynSettingsAsset
        {
            SecurityCheckCode = r.Bool(),
            AssemblyReferences = r.EntryBlock(),
            NamespaceReferences = r.EntryBlock(),
            TypeReferences = r.EntryBlock(),
            MemberReferences = r.EntryBlock(),
            IgnoreCase = r.Bool(),
            LogDetail = r.Int(),
            AllowUnsafeCode = r.Bool(),
            AllowOptimizeCode = r.Bool(),
            AllowConcurrentCompile = r.Bool(),
            GenerateInMemory = r.Bool(),
            GenerateSymbols = r.Bool(),
            WarningLevel = r.Int(),
            LanguageVersion = r.Int(),
            TargetPlatform = r.Int(),
            References = r.StringList(),
            DefineSymbols = r.StringList(),
        };
        if (s.WarningLevel is < 0 or > 9999 || s.LogDetail is < 0 or > 3 || s.TargetPlatform is < 0 or > 10)
            throw new InvalidDataException("RoslynCSharpSettings parsed to implausible values; layout changed?");
        return s;
    }

    public void Print(Report report)
    {
        report.Info($"asset: securityCheckCode={SecurityCheckCode} languageVersion={LanguageVersion}{(LanguageVersion == 0 ? " (Default = C# 7.0 in Roslyn 2.10)" : "")} warningLevel={WarningLevel} optimize={AllowOptimizeCode} platform={TargetPlatform}");
        report.Info($"asset: references=[{string.Join(", ", References)}] defines=[{string.Join(", ", DefineSymbols)}] (defines are replaced by the loader)");
        report.Info($"asset: assemblyRefs {AssemblyReferences}");
        report.Info($"asset: typeRefs {TypeReferences}");
        report.Info($"asset: memberRefs {MemberReferences}");
        report.Info($"asset: namespaceRefs {NamespaceReferences} (replaced by Mod.SetupSecurity)");
    }

    private sealed class Reader
    {
        private readonly byte[] d;
        private int p;

        public Reader(byte[] data, int pos)
        {
            d = data;
            p = pos;
            Align();
        }

        private void Align() => p = (p + 3) & ~3;

        public int Int()
        {
            int v = BitConverter.ToInt32(d, p);
            p += 4;
            return v;
        }

        public bool Bool()
        {
            int v = Int();
            if (v is not (0 or 1)) throw new InvalidDataException($"expected bool at {p - 4}, got {v}");
            return v == 1;
        }

        public string Str()
        {
            int len = Int();
            if (len < 0 || len > 4096) throw new InvalidDataException($"implausible string length {len} at {p - 4}");
            string s = Encoding.UTF8.GetString(d, p, len);
            p += len;
            Align();
            return s;
        }

        public List<string> StringList()
        {
            int n = Int();
            if (n < 0 || n > 1000) throw new InvalidDataException($"implausible list count {n} at {p - 4}");
            var list = new List<string>(n);
            for (int i = 0; i < n; i++) list.Add(Str());
            return list;
        }

        public Entry EntryBlock() => new() { Allow = StringList(), Deny = StringList(), DefaultBehaviour = Int() };
    }
}
