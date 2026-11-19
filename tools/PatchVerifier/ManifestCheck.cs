using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PatchVerifier;

/// <summary>
/// Validates the mod folder the way ModManager / Mod (2026.1) consume it:
/// - ModManager reads "&lt;mod&gt;/manifest.json" with UnityEngine.JsonUtility into ModManifest
///   { name, version, description, author, supportedGameVersions[], assemblyName, permissions[], steamFileId }.
/// - permissions parse with Enum.TryParse&lt;SecurityOptions&gt;(ignoreCase); unknown values silently become 0.
/// - assemblyName is sanitized with [^A-Za-z0-9 _\-] removed (falls back to the folder name when blank).
/// - Source/**/*.cs (top-level Source only) is compiled; Data Sheets/**/*.xlsx, Bundles/*, Assets/** are
///   loaded from the mod root and from compatible "Version X" / "Version X-Y" subfolders.
/// </summary>
public static class ManifestCheck
{
    public sealed class Result
    {
        public LoaderModel.SecurityOptions Permissions;
        public string AssemblyName;
        public string Name;
    }

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "name", "version", "description", "author", "supportedGameVersions", "assemblyName", "permissions", "steamFileId",
    };

    public static Result Run(string modDir, string gameVersion, Report report)
    {
        report.Section("manifest and folder layout");
        var result = new Result();
        string manifestPath = Directory.GetFiles(modDir)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), "manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestPath == null)
        {
            report.Error("no manifest.json in mod folder (ModManager skips the mod: \"Mod has no JSON manifest file\")");
            return result;
        }
        if (Path.GetFileName(manifestPath) != "manifest.json")
            report.Info($"{Path.GetFileName(manifestPath)}: the loader asks for \"manifest.json\"; fine on Windows/CrossOver (case-insensitive file system)");

        JsonDocument doc;
        try
        {
            // Strict JSON: JsonUtility does not accept comments or trailing commas reliably.
            doc = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException e)
        {
            report.Error($"manifest is not strict JSON: {e.Message}");
            return result;
        }

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            report.Error("manifest root must be a JSON object");
            return result;
        }
        foreach (var prop in root.EnumerateObject())
        {
            if (!KnownKeys.Contains(prop.Name))
            {
                var ci = KnownKeys.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (ci != null) report.Error($"manifest key \"{prop.Name}\" must be spelled \"{ci}\" (JsonUtility is case-sensitive; value would be ignored)");
                else report.Warn($"manifest key \"{prop.Name}\" is not read by the loader");
            }
        }

        string GetString(string key, bool required)
        {
            if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                if (required) report.Error($"manifest \"{key}\" missing");
                return null;
            }
            if (v.ValueKind != JsonValueKind.String)
            {
                report.Error($"manifest \"{key}\" must be a string");
                return null;
            }
            return v.GetString();
        }

        string[] GetStringArray(string key)
        {
            if (!root.TryGetProperty(key, out var v)) return null;
            if (v.ValueKind != JsonValueKind.Array || v.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
            {
                report.Error($"manifest \"{key}\" must be an array of strings");
                return null;
            }
            return v.EnumerateArray().Select(e => e.GetString()).ToArray();
        }

        result.Name = GetString("name", true);
        if (string.IsNullOrWhiteSpace(result.Name)) report.Error("manifest \"name\" is empty");
        else report.Ok($"name \"{result.Name}\"");
        GetString("version", false);
        GetString("description", false);
        GetString("author", false);

        string asm = GetString("assemblyName", false);
        string folderName = Path.GetFileName(Path.GetFullPath(modDir).TrimEnd('/'));
        string effective = Regex.Replace(!string.IsNullOrWhiteSpace(asm) ? asm : folderName, "[^A-Za-z0-9 _\\-]", "");
        if (string.IsNullOrWhiteSpace(asm)) report.Warn($"no assemblyName; the loader would use the install folder name (here \"{effective}\")");
        else if (effective != asm) report.Error($"assemblyName \"{asm}\" contains characters the loader strips (becomes \"{effective}\")");
        else report.Ok($"assemblyName \"{asm}\" (compiled to <persistentDataPath>/Temp/{asm}.dll; must be unique across mods)");
        if (effective.Length == 0) report.Error("effective assembly name is empty");
        result.AssemblyName = effective;

        var versions = GetStringArray("supportedGameVersions");
        if (versions == null || versions.Length == 0) report.Error("supportedGameVersions missing or empty");
        else if (!versions.Contains(gameVersion)) report.Error($"supportedGameVersions [{string.Join(", ", versions)}] does not include {gameVersion}");
        else report.Ok($"supportedGameVersions includes {gameVersion} (informational for the launcher; the loader itself does not gate on it)");

        var perms = GetStringArray("permissions") ?? Array.Empty<string>();
        foreach (string p in perms)
        {
            // Mirror Enum.TryParse(ignoreCase) but refuse what would silently become 0 or surprise us.
            if (Enum.TryParse<LoaderModel.SecurityOptions>(p, true, out var opt) && opt != LoaderModel.SecurityOptions.None
                && Enum.GetNames<LoaderModel.SecurityOptions>().Any(n => string.Equals(n, p.Trim(), StringComparison.OrdinalIgnoreCase)))
                result.Permissions |= opt;
            else
                report.Error($"permission \"{p}\" is not one of IO, Reflection, Net, NativeAccess (the loader silently ignores it)");
        }
        report.Ok($"permissions: {(result.Permissions == 0 ? "none" : result.Permissions.ToString())}");

        if (root.TryGetProperty("steamFileId", out var sid) && sid.ValueKind != JsonValueKind.Number)
            report.Error("steamFileId must be a number");

        CheckLayout(modDir, report);
        return result;
    }

    private static void CheckLayout(string modDir, Report report)
    {
        string source = Path.Combine(modDir, "Source");
        if (!Directory.Exists(source)) report.Warn("no Source folder: the mod has no code");
        else
        {
            int n = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories).Length;
            if (n == 0) report.Warn("Source has no .cs files: the loader returns no assembly");
            else report.Ok($"Source: {n} .cs files");
            foreach (string f in Directory.GetFiles(source, "*.dll", SearchOption.AllDirectories))
                report.Error($"{Rel(modDir, f)}: prebuilt DLLs are not loaded; only Source/**/*.cs is compiled");
        }

        var roots = new List<string> { modDir };
        roots.AddRange(Directory.GetDirectories(modDir).Where(d => Path.GetFileName(d).StartsWith("Version ", StringComparison.OrdinalIgnoreCase)));
        foreach (string r in roots)
        {
            if (r != modDir && Directory.Exists(Path.Combine(r, "Source")))
                report.Warn($"{Rel(modDir, r)}/Source is ignored: only <mod>/Source is compiled (version folders apply to Data Sheets, Bundles, Assets)");
            CheckDataSheets(modDir, Path.Combine(r, "Data Sheets"), report);
            string bundles = Path.Combine(r, "Bundles");
            if (Directory.Exists(bundles) && Directory.GetDirectories(bundles).Length > 0)
                report.Warn($"{Rel(modDir, bundles)}: subfolders are not scanned (Bundles loads top-level files only)");
        }
    }

    private static void CheckDataSheets(string modDir, string dir, Report report)
    {
        if (!Directory.Exists(dir)) return;
        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(f);
            if (name.StartsWith('.')) continue;
            string rel = Rel(modDir, f);
            if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                report.Warn($"{rel}: not .xlsx, ignored by the loader");
                continue;
            }
            if (name.StartsWith("~$"))
            {
                report.Error($"{rel}: Excel lock file; the loader would try to parse it");
                continue;
            }
            try
            {
                using var zip = ZipFile.OpenRead(f);
                var wb = zip.GetEntry("xl/workbook.xml");
                if (wb == null)
                {
                    report.Error($"{rel}: not a valid xlsx (no xl/workbook.xml)");
                    continue;
                }
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                using var s = wb.Open();
                var sheets = XDocument.Load(s).Descendants(ns + "sheet").Select(e => (string)e.Attribute("name")).ToList();
                if (sheets.Count == 0) report.Error($"{rel}: workbook has no sheets");
                var app = zip.GetEntry("docProps/app.xml");
                if (app != null)
                {
                    using var a = new StreamReader(app.Open());
                    string text = a.ReadToEnd();
                    if (text.Contains("LibreOffice", StringComparison.OrdinalIgnoreCase))
                        report.Warn($"{rel}: saved by LibreOffice; such files are reported to break the game's xlsx reader");
                }
                report.Ok($"{rel}: sheets [{string.Join(", ", sheets)}]");
            }
            catch (InvalidDataException e)
            {
                report.Error($"{rel}: not a zip/xlsx: {e.Message}");
            }
        }
    }

    private static string Rel(string modDir, string path) => Path.GetRelativePath(modDir, path);
}
