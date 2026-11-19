using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DamageControl.Tests.Support
{
    /// <summary>
    /// Read-only access to the repository's data sources (data/**/*.json, the input of ./build-sheets)
    /// and shipped icons (mod/Assets/Icons/DamageControl), for completeness tests such as
    /// "every keyword an action returns has an English locale row" (04 §8 #10, #11).
    /// </summary>
    public static class RepoData
    {
        private static readonly Lazy<string> root = new Lazy<string>(FindRoot);

        public static string Root => root.Value;

        /// <summary>All rows of a table address (e.g. "Locales/Activators", "Crew/Actions") across every source file, as column -> value (key under "key").</summary>
        public static List<Dictionary<string, string>> Rows(string tableAddress)
        {
            var result = new List<Dictionary<string, string>>();
            string data = Path.Combine(Root, "data");
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            foreach (string file in Directory.GetFiles(data, "*.json", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != "schema.json"))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file), options);
                if (!doc.RootElement.TryGetProperty("tables", out var tables) || !tables.TryGetProperty(tableAddress, out var rows))
                    continue;
                foreach (var row in rows.EnumerateArray())
                {
                    var d = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var p in row.EnumerateObject())
                        d[p.Name == "..." ? "…" : p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
                    result.Add(d);
                }
            }
            return result;
        }

        /// <summary>English text of a locale key in a Locales sheet, or null.</summary>
        public static string English(string sheet, string key)
        {
            var row = Rows("Locales/" + sheet).FirstOrDefault(r => r.TryGetValue("key", out var k) && k == key);
            return row != null && row.TryGetValue("English", out var en) ? en : null;
        }

        /// <summary>True if some area ships Icons/DamageControl/&lt;area&gt;/&lt;category&gt;/&lt;iconName&gt;.png.</summary>
        public static bool IconExists(string category, string iconName)
        {
            string icons = Path.Combine(Root, "mod", "Assets", "Icons", "DamageControl");
            if (!Directory.Exists(icons))
                return false;
            return Directory.GetDirectories(icons).Any(area => File.Exists(Path.Combine(area, category, iconName + ".png")));
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "verify")) && Directory.Exists(Path.Combine(dir.FullName, "data")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("repository root (folder with ./verify and data/) not found above " + AppContext.BaseDirectory);
        }
    }
}
