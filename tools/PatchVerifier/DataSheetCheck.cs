using System.Reflection;

namespace PatchVerifier;

/// <summary>
/// Reads the mod's data sheets with the game's own xlsx reader (FastExcel.dll from the Managed folder,
/// executed here exactly as GameData uses it) and computes each row's data path the way GameData does:
/// "&lt;file stem up to its last dot&gt;/&lt;worksheet&gt;[/&lt;group&gt;]/&lt;column A&gt;", where a column-A header "/Name"
/// adds "/Name" unless it is "/", "/." or the worksheet path already ends with "Name"; "//" rows are
/// comments; rows with an empty column A are skipped; a "…" header expands "k = v; k = v" cells.
///
/// Then checks what the game's loaders would otherwise accept or drop silently:
/// - Crew/Actions rows whose type is in the DamageControl namespace: the type exists in the compiled mod,
///   is a public concrete CharacterAction, and has public (GameDataRow) and (Self, Activator) constructors;
///   contexts are valid. Rows naming other types must resolve in com.uboat.game (else the game skips them).
/// - General/Background Tasks rows of ours: the type implements IBackgroundTask, has a public parameterless
///   constructor, and is [Serializable] when started by CampaignStart (only those are saved).
/// - Locales rows have English text.
/// Rows that replace a base-game row (same path) are listed as info.
/// </summary>
public static class DataSheetCheck
{
    private static readonly string[] Contexts = { "Normal", "Queue", "NoCharacter", "Upgrade" };
    private static readonly string[] StartEvents = { "GameStart", "MenuStart", "GameplayStart", "CampaignStart" };

    public sealed class Row
    {
        public string Path;
        public string File;
        public string[] Keys;
        public string[] Values;

        public string Get(int i) => i < Values.Length ? Values[i] : "";

        public string Get(string key)
        {
            for (int i = 1; i < Keys.Length && i < Values.Length; i++)
                if (Keys[i] == key) return Values[i];
            return null;
        }
    }

    public static void Run(GameEnvironment env, string modDir, string modDll, Report report)
    {
        report.Section("data sheets (read with the game's FastExcel)");
        string sheetsDir = System.IO.Path.Combine(modDir, "Data Sheets");
        if (!Directory.Exists(sheetsDir))
        {
            report.Info("no Data Sheets folder");
            return;
        }
        var files = Directory.GetFiles(sheetsDir, "*.xlsx", SearchOption.AllDirectories).Where(f => !System.IO.Path.GetFileName(f).StartsWith("~$")).OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            report.Info("no .xlsx files");
            return;
        }

        var rows = new List<Row>();
        foreach (string f in files)
        {
            try
            {
                var fileRows = Read(f);
                rows.AddRange(fileRows);
                foreach (var g in fileRows.GroupBy(r => r.Path.Substring(0, r.Path.LastIndexOf('/'))))
                    report.Ok($"{System.IO.Path.GetFileName(f)}: {g.Key} ({g.Count()} rows)");
            }
            catch (Exception e)
            {
                report.Error($"{System.IO.Path.GetFileName(f)}: the game's reader fails on it: {e.GetType().Name}: {e.Message}");
            }
        }
        foreach (var dup in rows.GroupBy(r => r.Path).Where(g => g.Count() > 1))
            report.Warn($"{dup.Key}: defined {dup.Count()} times in the mod; the last one loaded wins");

        ReportOverrides(env, rows, report);

        using var types = new ModTypes(env, modDll);
        foreach (var r in rows.Where(r => r.Path.StartsWith("Crew/Actions/", StringComparison.Ordinal)))
            CheckAction(r, types, env, report);
        foreach (var r in rows.Where(r => r.Path.StartsWith("General/Background Tasks/", StringComparison.Ordinal)))
            CheckTask(r, types, report);
        foreach (var r in rows.Where(r => r.Path.StartsWith("Locales/", StringComparison.Ordinal)))
        {
            if (r.Get(0) == "Key" && r.Get(1) == "English") continue; // the sheet's language header row
            if (string.IsNullOrWhiteSpace(r.Get(1))) report.Error($"{r.Path}: no English text ({r.File})");
        }
    }

    // ------------------------------------------------------------------ reading

    /// <summary>All rows of one workbook, with GameData's path rules.</summary>
    public static List<Row> Read(string file)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(file);
        int dot = stem.LastIndexOf('.');
        string ns = dot >= 0 ? stem.Substring(0, dot) : stem;
        var result = new List<Row>();

        using var excel = new FastExcel.FastExcel(new FileInfo(file), true);
        foreach (var sheet in excel.Worksheets)
        {
            sheet.Read();
            string sheetPath = ns + "/" + sheet.Name;
            string[] header = null;
            foreach (var row in sheet.Rows)
            {
                if (row.Cells == null) continue;
                var values = new List<string>();
                int col = 0;
                foreach (var cell in row.Cells)
                {
                    while (++col < cell.ColumnNumber) values.Add("");
                    string text = cell.Value?.ToString() ?? "";
                    values.Add(string.IsNullOrWhiteSpace(text) ? text : text.Trim());
                }
                if (values.Count == 0 || values[0].Length == 0) continue;
                string a = values[0];
                if (a.StartsWith('/'))
                {
                    if (!a.StartsWith("//")) header = values.ToArray();
                    continue;
                }
                string[] keys = header ?? Array.Empty<string>();
                string[] vals = values.ToArray();
                int ell = Array.IndexOf(keys, "…");
                if (ell >= 0)
                {
                    var k2 = keys.ToList();
                    var v2 = vals.ToList();
                    while (v2.Count < k2.Count) v2.Add("");
                    k2.RemoveAt(ell);
                    string packed = v2[ell];
                    v2.RemoveAt(ell);
                    int at = ell;
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(packed, "([^=]+)=([^;]+)(?:;|$)"))
                    {
                        k2.Insert(at, m.Groups[1].Value.Trim());
                        v2.Insert(at, m.Groups[2].Value.Trim());
                        at++;
                    }
                    keys = k2.ToArray();
                    vals = v2.ToArray();
                }
                string group = "";
                if (header != null)
                {
                    string h = header[0];
                    if (h != "/" && h != "/." && !sheetPath.EndsWith(h.Substring(1), StringComparison.Ordinal)) group = h;
                }
                result.Add(new Row { Path = sheetPath + group + "/" + a, File = System.IO.Path.GetFileName(file), Keys = keys, Values = vals });
            }
        }
        return result;
    }

    private static void ReportOverrides(GameEnvironment env, List<Row> rows, Report report)
    {
        string baseDir = System.IO.Path.Combine(env.DataDir, "Data Sheets");
        var namespaces = rows.Select(r => r.Path.Substring(0, r.Path.IndexOf('/'))).Distinct().ToList();
        var basePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ns in namespaces)
        {
            string f = System.IO.Path.Combine(baseDir, ns + ".xlsx");
            if (!File.Exists(f)) continue;
            try
            {
                foreach (var r in Read(f)) basePaths.Add(r.Path);
            }
            catch (Exception e)
            {
                report.Warn($"base sheet {ns}.xlsx could not be read for the override report: {e.Message}");
            }
        }
        foreach (var r in rows.Where(r => basePaths.Contains(r.Path) && !(r.Path.StartsWith("Locales/", StringComparison.Ordinal) && r.Get(0) == "Key")))
            report.Info($"{r.Path} replaces the base-game row ({r.File})");
    }

    // ------------------------------------------------------------------ checks

    private static void CheckAction(Row r, ModTypes types, GameEnvironment env, Report report)
    {
        string typeName = r.Get(1).Trim();
        var ctx = r.Get(5).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        foreach (string c in ctx.Where(c => !Contexts.Contains(c)))
            report.Error($"{r.Path}: unknown context \"{c}\"; the game ignores it");
        if (ctx.Length == 0) report.Error($"{r.Path}: no contexts; the action can never be offered");

        if (!typeName.StartsWith("DamageControl.", StringComparison.Ordinal))
        {
            bool inGame = env.TypeIndex.TryGetValue(typeName, out var dlls) && dlls.Any(d => System.IO.Path.GetFileName(d) == "com.uboat.game.dll");
            if (inGame) report.Ok($"{r.Path}: game type {typeName}");
            else report.Warn($"{r.Path}: type \"{typeName}\" is neither ours nor in com.uboat.game; the game skips this row");
            return;
        }
        var t = types.Mod(typeName);
        if (t == null)
        {
            report.Error($"{r.Path}: type {typeName} not found in the compiled mod");
            return;
        }
        var baseType = types.Game("UBOAT.Game.Scene.Characters.Actions.CharacterAction");
        var rowType = types.Game("UBOAT.Game.Core.Data.GameDataRow");
        var activatorType = types.Game("UBOAT.Game.Scene.Characters.Activators.Activator");
        bool ok = true;
        if (!t.IsPublic || t.IsAbstract || !baseType.IsAssignableFrom(t))
        {
            report.Error($"{r.Path}: {typeName} must be a public, non-abstract CharacterAction");
            ok = false;
        }
        if (t.GetConstructor(new[] { rowType }) == null)
        {
            report.Error($"{r.Path}: {typeName} needs a public ({rowType.Name}) constructor (the registration creates the template with it)");
            ok = false;
        }
        bool copyCtor = t.GetConstructors().Any(c =>
        {
            var p = c.GetParameters();
            return p.Length == 2 && p[0].ParameterType.IsAssignableFrom(t) && p[0].ParameterType != types.Game("System.Object") && p[1].ParameterType == activatorType;
        });
        if (!copyCtor)
        {
            report.Error($"{r.Path}: {typeName} needs a public ({t.Name} source, Activator activator) constructor (Duplicate uses it)");
            ok = false;
        }
        if (!t.IsSerializable) report.Warn($"{r.Path}: {typeName} is not [Serializable]; queued actions are saved with the game");
        if (ok) report.Ok($"{r.Path}: {typeName} resolves in the mod; contexts {string.Join(", ", ctx)}");
    }

    private static void CheckTask(Row r, ModTypes types, Report report)
    {
        string typeName = r.Get(1).Trim();
        string ev = r.Get(2).Trim();
        if (!StartEvents.Contains(ev)) report.Error($"{r.Path}: start event \"{ev}\" is never fired (use {string.Join(", ", StartEvents)})");
        if (!typeName.StartsWith("DamageControl.", StringComparison.Ordinal)) return;
        var t = types.Mod(typeName);
        if (t == null)
        {
            report.Error($"{r.Path}: type {typeName} not found in the compiled mod");
            return;
        }
        bool ok = true;
        if (!types.Game("UBOAT.Game.Core.IBackgroundTask").IsAssignableFrom(t) || t.IsAbstract)
        {
            report.Error($"{r.Path}: {typeName} must be a concrete IBackgroundTask (subclass UBOAT.Game.Core.BackgroundTaskBase)");
            ok = false;
        }
        if (t.GetConstructor(Type.EmptyTypes) == null)
        {
            report.Error($"{r.Path}: {typeName} needs a public parameterless constructor");
            ok = false;
        }
        if (ev == "CampaignStart" && !t.IsSerializable)
        {
            report.Error($"{r.Path}: {typeName} starts on CampaignStart (saved with the game) but is not [Serializable]");
            ok = false;
        }
        if (ok) report.Ok($"{r.Path}: {typeName} on {ev}{(ev == "CampaignStart" ? " (saved per campaign)" : "")}");
    }

    /// <summary>Metadata-only view of the compiled mod and the game assemblies.</summary>
    private sealed class ModTypes : IDisposable
    {
        private readonly MetadataLoadContext mlc;
        private readonly Assembly mod;

        public ModTypes(GameEnvironment env, string modDll)
        {
            var paths = Directory.GetFiles(env.ManagedDir, "*.dll").Append(System.IO.Path.GetFullPath(modDll));
            mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
            mod = mlc.LoadFromAssemblyPath(System.IO.Path.GetFullPath(modDll));
        }

        public Type Mod(string fullName) => mod.GetType(fullName, false);

        public Type Game(string fullName)
        {
            if (fullName.StartsWith("System.", StringComparison.Ordinal)) return mlc.CoreAssembly.GetType(fullName, true);
            var asm = mlc.LoadFromAssemblyName("com.uboat.game");
            return asm.GetType(fullName, true);
        }

        public void Dispose() => mlc.Dispose();
    }
}
