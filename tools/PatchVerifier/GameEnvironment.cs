using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PatchVerifier;

/// <summary>
/// What the game's mod loader (UBOAT.Game.Core.Mods.Mod, 2026.1) does when it compiles a mod,
/// expressed as data. Keep in sync with Mod.Compile / Mod.SetupSecurity / ModManager.
/// </summary>
public static class LoaderModel
{
    /// <summary>Mod.Compile: DefineSymbols.Clear() then these three.</summary>
    public static readonly string[] DefineSymbols = { "UBOAT", "UBOAT_2024_1", "UNITY_STANDALONE_WIN" };

    /// <summary>
    /// Mod.Compile: ReferenceAssemblies.Clear() then Assembly.GetAssembly(typeof(X)) for each X.
    /// We resolve each type to the Managed/*.dll that defines it.
    /// </summary>
    public static readonly string[] ReferenceAnchorTypes =
    {
        "DWS.Common.InjectionFramework.InjectAttribute",
        "UBOAT.Game.Core.Mods.ModManager",
        "UBOAT.Game.IMod",
        "UBOAT.Game.Rendering.IImageEffect",
        "UnityEngine.Object",
        "UnityEngine.EventSystems.PointerEventData",
        "UnityEngine.Video.VideoPlayer",
        "UnityEngine.ParticleSystem",
        "UnityEngine.AudioSource",
        "UnityEngine.AI.NavMesh",
        "UnityEngine.AnimationClip",
        "UnityEngine.TerrainData",
        "UnityEngine.TerrainCollider",
        "UnityEngine.Font",
        "UnityEngine.WindZone",
        "UnityEngine.Input",
        "TMPro.TextMeshProUGUI",
        "System.Collections.Hashtable",
        // Assembly.Load("System") is handled separately (assembly by name).
        "System.Collections.Generic.HashSet`1",
        "UnityEngine.ImageConversion",
        "PlayWay.Water.Water",
        "UnityEngine.Rendering.PostProcessing.PostProcessLayer",
        "SSMS.SSMS",
        "Cysharp.Threading.Tasks.UniTask",
        "Cysharp.Text.Utf16ValueStringBuilder",
        "UnityEngine.JsonUtility",
        "System.Xml.XmlDocument",
        "Unity.Mathematics.bool2",
    };

    /// <summary>Mod.Compile: Assembly.Load("System").</summary>
    public static readonly string[] ReferenceAssemblyNames = { "System" };

    /// <summary>Added only when the manifest grants the Reflection permission.</summary>
    public const string HarmonyAnchorType = "HarmonyLib.Harmony";

    /// <summary>Mod.SecurityOptions (permissions in Manifest.json, parsed with Enum.TryParse ignoreCase).</summary>
    [Flags]
    public enum SecurityOptions { None = 0, IO = 1, Reflection = 2, Net = 4, NativeAccess = 8 }

    /// <summary>Namespace deny entries Mod.SetupSecurity adds (after clearing the asset's namespace lists).</summary>
    public static IEnumerable<string> DeniedNamespaceEntries(SecurityOptions opts)
    {
        if ((opts & SecurityOptions.IO) == 0) yield return "System.IO.*";
        if ((opts & SecurityOptions.Reflection) == 0) yield return "System.Reflection.*";
        yield return "System.Diagnostics.*";
        if ((opts & SecurityOptions.Net) == 0) yield return "System.Net.*";
        if ((opts & SecurityOptions.NativeAccess) == 0) yield return "System.Runtime.InteropServices.*";
    }
}

/// <summary>Locations of the game install, overridable via UBOAT_MANAGED.</summary>
public sealed class GameEnvironment
{
    public const string DefaultManaged = "/Volumes/Games/SteamLibrary/steamapps/common/UBOAT/UBOAT_Data/Managed";

    public string ManagedDir { get; }
    public string DataDir => Path.GetDirectoryName(ManagedDir.TrimEnd('/', '\\'))!;
    public string ResourcesAssets => Path.Combine(DataDir, "resources.assets");

    public GameEnvironment(string managedDir)
    {
        ManagedDir = Path.GetFullPath(managedDir);
        if (!File.Exists(Path.Combine(ManagedDir, "com.uboat.game.dll")))
            throw new InvalidOperationException($"Game Managed folder not found or incomplete: {ManagedDir} (set UBOAT_MANAGED)");
    }

    public static GameEnvironment FromEnvironment(string overridePath)
    {
        string dir = overridePath ?? Environment.GetEnvironmentVariable("UBOAT_MANAGED");
        return new GameEnvironment(string.IsNullOrWhiteSpace(dir) ? DefaultManaged : dir);
    }

    public string Dll(string simpleName) => Path.Combine(ManagedDir, simpleName + ".dll");

    private Dictionary<string, List<string>> typeIndex;

    /// <summary>Full type name (Namespace.Name, nested with '+') -> defining dll paths (type definitions, not forwarders).</summary>
    public IReadOnlyDictionary<string, List<string>> TypeIndex => typeIndex ??= BuildTypeIndex();

    private Dictionary<string, List<string>> BuildTypeIndex()
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string dll in Directory.GetFiles(ManagedDir, "*.dll"))
        {
            try
            {
                using var stream = File.OpenRead(dll);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata) continue;
                var md = pe.GetMetadataReader();
                foreach (var h in md.TypeDefinitions)
                {
                    var td = md.GetTypeDefinition(h);
                    if (td.GetDeclaringType().IsNil == false) continue;
                    string ns = md.GetString(td.Namespace);
                    string name = md.GetString(td.Name);
                    string full = ns.Length == 0 ? name : ns + "." + name;
                    if (!index.TryGetValue(full, out var list)) index[full] = list = new List<string>();
                    list.Add(dll);
                }
            }
            catch (BadImageFormatException)
            {
                // native or non-.NET file
            }
        }
        return index;
    }

    /// <summary>Resolves the loader's reference list to concrete DLL paths (ordered, distinct).</summary>
    public List<string> ResolveLoaderReferences(LoaderModel.SecurityOptions opts, RoslynSettingsAsset settings, Report report)
    {
        var result = new List<string>();
        void Add(string path)
        {
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
        }

        var anchors = LoaderModel.ReferenceAnchorTypes.ToList();
        if ((opts & LoaderModel.SecurityOptions.Reflection) != 0) anchors.Add(LoaderModel.HarmonyAnchorType);
        foreach (string type in anchors)
        {
            if (!TypeIndex.TryGetValue(type, out var dlls))
            {
                report.Error($"loader reference anchor type {type} not found in any Managed dll (game changed? update LoaderModel)");
                continue;
            }
            if (dlls.Count > 1)
                report.Warn($"loader reference anchor type {type} defined in several dlls: {string.Join(", ", dlls.Select(Path.GetFileName))}; using first");
            Add(dlls[0]);
        }
        foreach (string name in LoaderModel.ReferenceAssemblyNames)
        {
            string p = Dll(name);
            if (File.Exists(p)) Add(p); else report.Error($"loader reference assembly {name}.dll missing");
        }
        // RoslynCSharp settings asset "References" (resolved by file name among loaded assemblies).
        foreach (string reference in settings.References)
        {
            string p = Dll(Path.GetFileNameWithoutExtension(reference));
            if (File.Exists(p)) Add(p); else report.Error($"RoslynCSharp settings reference {reference} not found in Managed");
        }
        return result;
    }
}
