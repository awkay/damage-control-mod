using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;

namespace SheetBuilder;

/// <summary>
/// Merges the per-workstream row sources (data/&lt;area&gt;/*.json) into the shipped workbooks
/// (mod/Data Sheets/&lt;Workbook&gt;.DamageControl.xlsx), following data/schema.json.
///
/// Usage: SheetBuilder [--data DIR] [--out DIR] [--check]
///   --check  build in memory and fail (exit 1) if any shipped workbook differs; write nothing.
/// Exit codes: 0 ok, 1 validation error or stale output, 2 usage/environment problem.
///
/// Output is deterministic (fixed zip timestamps, stable ordering), so unchanged sources give
/// byte-identical files and an unchanged file is never rewritten.
/// The xlsx layout targets the game's reader (FastExcel 2017): worksheets are xl/worksheets/sheetN.xml
/// in workbook order, every cell has an r attribute, and text is stored as inline strings (no shared
/// string table, whose duplicates that reader cannot handle).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string data = "data", output = "mod/Data Sheets";
        bool check = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data" when i + 1 < args.Length: data = args[++i]; break;
                case "--out" when i + 1 < args.Length: output = args[++i]; break;
                case "--check": check = true; break;
                default:
                    Console.WriteLine("usage: SheetBuilder [--data DIR] [--out DIR] [--check]");
                    return 2;
            }
        }

        var errors = new List<string>();
        Schema schema;
        try
        {
            schema = Schema.Load(Path.Combine(data, "schema.json"));
        }
        catch (Exception e)
        {
            Console.WriteLine("ERROR data/schema.json: " + e.Message);
            return 2;
        }

        var tables = new Dictionary<string, TableData>(StringComparer.Ordinal);
        string[] sources = Directory.GetFiles(data, "*.json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "schema.json")
            .OrderBy(f => Path.GetRelativePath(data, f).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();
        foreach (string file in sources)
            Sources.Read(file, Path.GetRelativePath(data, file).Replace('\\', '/'), schema, tables, errors);

        foreach (var t in tables.Values)
            Rules.Validate(t, errors);

        if (errors.Count > 0)
        {
            foreach (string e in errors) Console.WriteLine("ERROR " + e);
            Console.WriteLine($"== result: {errors.Count} error(s); no workbook written");
            return 1;
        }

        var produced = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var wb in schema.Workbooks)
        {
            var sheets = new List<(string Name, List<string[]> Rows)>();
            foreach (var sheet in wb.Sheets)
            {
                var rows = new List<string[]>();
                foreach (var group in sheet.Groups)
                {
                    if (!tables.TryGetValue(group.Address, out var t) || t.Rows.Count == 0) continue;
                    rows.Add(group.HeaderRow());
                    foreach (var r in t.Rows) rows.Add(r.Cells);
                }
                if (rows.Count > 0) sheets.Add((sheet.Name, rows));
            }
            if (sheets.Count > 0)
                produced[wb.Name + "." + schema.OutputSuffix + ".xlsx"] = Xlsx.Write(sheets);
        }

        int stale = 0;
        Directory.CreateDirectory(output);
        foreach (var (name, bytes) in produced)
        {
            string path = Path.Combine(output, name);
            bool same = File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes);
            if (same)
            {
                Console.WriteLine($"  ok    {name} (unchanged)");
                continue;
            }
            if (check)
            {
                Console.WriteLine($"  STALE {name}: differs from data/; run ./build-sheets");
                stale++;
                continue;
            }
            File.WriteAllBytes(path, bytes);
            Console.WriteLine($"  wrote {name} ({bytes.Length} bytes)");
        }
        foreach (string existing in Directory.GetFiles(output, "*.xlsx"))
        {
            string name = Path.GetFileName(existing);
            if (produced.ContainsKey(name)) continue;
            if (name.EndsWith("." + schema.OutputSuffix + ".xlsx", StringComparison.Ordinal))
            {
                if (check)
                {
                    Console.WriteLine($"  STALE {name}: no longer produced by data/");
                    stale++;
                }
                else
                {
                    File.Delete(existing);
                    Console.WriteLine($"  removed {name} (no rows left)");
                }
            }
            else
            {
                Console.WriteLine($"ERROR {name}: hand-made workbook in {output}; add its rows to data/ instead");
                errors.Add(name);
            }
        }

        int rowCount = tables.Values.Sum(t => t.Rows.Count);
        Console.WriteLine($"== {sources.Length} source file(s), {rowCount} row(s), {produced.Count} workbook(s)");
        if (errors.Count > 0 || stale > 0)
        {
            Console.WriteLine($"== result: FAIL ({errors.Count} error(s), {stale} stale)");
            return 1;
        }
        Console.WriteLine("== result: ok");
        return 0;
    }
}

/// <summary>data/schema.json: which workbooks, sheets, groups and columns exist, in output order.</summary>
public sealed class Schema
{
    public string OutputSuffix;
    public string[] LocaleColumns;
    public List<WorkbookDef> Workbooks = new();
    public Dictionary<string, GroupDef> GroupsByAddress = new(StringComparer.Ordinal);

    public sealed class WorkbookDef
    {
        public string Name;
        public List<SheetDef> Sheets = new();
    }

    public sealed class SheetDef
    {
        public string Name;
        public List<GroupDef> Groups = new();
    }

    public sealed class GroupDef
    {
        /// <summary>"Workbook/Sheet" for the default group or a locale sheet, "Workbook/Sheet/Group" for a named group.</summary>
        public string Address;
        public string Workbook;
        public string Sheet;
        /// <summary>Column A header: "/" (default group), "/Name" (named group) or "Key" (locale sheet).</summary>
        public string KeyHeader;
        public string[] Columns;
        public bool IsLocale;

        public string[] HeaderRow() => new[] { KeyHeader }.Concat(Columns).ToArray();
    }

    public static Schema Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        var s = new Schema
        {
            OutputSuffix = root.GetProperty("outputSuffix").GetString(),
            LocaleColumns = root.GetProperty("localeColumns").EnumerateArray().Select(e => e.GetString()).ToArray(),
        };
        if (string.IsNullOrWhiteSpace(s.OutputSuffix) || s.OutputSuffix.Contains('.'))
            throw new InvalidDataException("outputSuffix must be a non-empty name without dots");
        foreach (var wbEl in root.GetProperty("workbooks").EnumerateArray())
        {
            var wb = new WorkbookDef { Name = wbEl.GetProperty("name").GetString() };
            foreach (var shEl in wbEl.GetProperty("sheets").EnumerateArray())
            {
                var sh = new SheetDef { Name = shEl.GetProperty("name").GetString() };
                if (shEl.TryGetProperty("locale", out var loc) && loc.GetBoolean())
                {
                    sh.Groups.Add(new GroupDef
                    {
                        Address = wb.Name + "/" + sh.Name, Workbook = wb.Name, Sheet = sh.Name,
                        KeyHeader = "Key", Columns = s.LocaleColumns, IsLocale = true,
                    });
                }
                else
                {
                    foreach (var gEl in shEl.GetProperty("groups").EnumerateArray())
                    {
                        string g = gEl.GetProperty("name").GetString();
                        if (g != "/" && (!g.StartsWith('/') || g.StartsWith("//") || g.Length < 2))
                            throw new InvalidDataException($"{wb.Name}/{sh.Name}: group \"{g}\" must be \"/\" or \"/Name\"");
                        sh.Groups.Add(new GroupDef
                        {
                            Address = wb.Name + "/" + sh.Name + (g == "/" ? "" : g),
                            Workbook = wb.Name, Sheet = sh.Name, KeyHeader = g,
                            Columns = gEl.GetProperty("columns").EnumerateArray().Select(e => e.GetString()).ToArray(),
                        });
                    }
                }
                wb.Sheets.Add(sh);
            }
            s.Workbooks.Add(wb);
        }
        foreach (var g in s.Workbooks.SelectMany(w => w.Sheets).SelectMany(sh => sh.Groups))
        {
            if (!s.GroupsByAddress.TryAdd(g.Address, g))
                throw new InvalidDataException("duplicate table " + g.Address);
        }
        return s;
    }
}

public sealed class SourceRow
{
    public string File;
    public string Key;
    public string[] Cells; // column A (key) first
    public Dictionary<string, string> ByColumn;
}

public sealed class TableData
{
    public Schema.GroupDef Group;
    public List<SourceRow> Rows = new();
    public Dictionary<string, SourceRow> ByKey = new(StringComparer.Ordinal);
}

/// <summary>Reads one row-source file: { "tables": { "&lt;address&gt;": [ { "key": "...", "&lt;Column&gt;": "..." } ] } }.</summary>
public static class Sources
{
    public static void Read(string path, string rel, Schema schema, Dictionary<string, TableData> tables, List<string> errors)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            errors.Add($"{rel}: not valid JSON: {e.Message}");
            return;
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tables", out var tablesEl) || tablesEl.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{rel}: expected an object with a \"tables\" object");
                return;
            }
            foreach (var prop in root.EnumerateObject())
                if (prop.Name != "tables" && !prop.Name.StartsWith('$'))
                    errors.Add($"{rel}: unknown top-level key \"{prop.Name}\" (only \"tables\" and \"$...\" comments)");

            foreach (var tableProp in tablesEl.EnumerateObject())
            {
                string address = tableProp.Name;
                if (!schema.GroupsByAddress.TryGetValue(address, out var group))
                {
                    errors.Add($"{rel}: unknown table \"{address}\"; known: {string.Join(", ", schema.GroupsByAddress.Keys)}");
                    continue;
                }
                if (tableProp.Value.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{rel}: table \"{address}\" must be an array of row objects");
                    continue;
                }
                if (!tables.TryGetValue(address, out var table))
                    tables[address] = table = new TableData { Group = group };
                int index = 0;
                foreach (var rowEl in tableProp.Value.EnumerateArray())
                {
                    index++;
                    var row = ReadRow(rowEl, group, $"{rel}: {address} row {index}", errors);
                    if (row == null) continue;
                    row.File = rel;
                    if (table.ByKey.TryGetValue(row.Key, out var other))
                    {
                        errors.Add($"{rel}: {address} key \"{row.Key}\" is also defined in {other.File}; each row has one owner");
                        continue;
                    }
                    table.ByKey[row.Key] = row;
                    table.Rows.Add(row);
                }
            }
        }
    }

    private static SourceRow ReadRow(JsonElement el, Schema.GroupDef group, string where, List<string> errors)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{where}: must be an object");
            return null;
        }
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string key = null;
        bool ok = true;
        foreach (var p in el.EnumerateObject())
        {
            if (p.Name.StartsWith('$')) continue; // comment
            string value = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Null => "",
                _ => null,
            };
            if (value == null)
            {
                errors.Add($"{where}: \"{p.Name}\" must be a string, number or boolean");
                ok = false;
                continue;
            }
            if (p.Name == "key")
            {
                key = value;
                continue;
            }
            string column = p.Name == "..." ? "…" : p.Name;
            if (Array.IndexOf(group.Columns, column) < 0)
            {
                errors.Add($"{where}: unknown column \"{p.Name}\"; columns are: {string.Join(", ", group.Columns)}");
                ok = false;
                continue;
            }
            values[column] = value;
        }
        if (string.IsNullOrEmpty(key))
        {
            errors.Add($"{where}: missing \"key\"");
            return null;
        }
        if (!ok) return null;
        var cells = new string[group.Columns.Length + 1];
        cells[0] = key;
        for (int i = 0; i < group.Columns.Length; i++)
            cells[i + 1] = values.TryGetValue(group.Columns[i], out var v) ? v : "";
        return new SourceRow { Key = key, Cells = cells, ByColumn = values };
    }
}

/// <summary>Content rules that the game's loaders would otherwise apply silently.</summary>
public static class Rules
{
    private static readonly string[] Contexts = { "Normal", "Queue", "NoCharacter", "Upgrade" };
    private static readonly string[] StartEvents = { "GameStart", "MenuStart", "GameplayStart", "CampaignStart" };

    public static void Validate(TableData t, List<string> errors)
    {
        foreach (var r in t.Rows)
        {
            string where = $"{r.File}: {t.Group.Address} \"{r.Key}\"";
            if (r.Key != r.Key.Trim()) errors.Add($"{where}: key has surrounding spaces (the game trims cells)");
            if (r.Key.StartsWith('/')) errors.Add($"{where}: key must not start with '/' (that marks a header or comment row)");
            for (int i = 0; i < r.Cells.Length; i++)
            {
                string cell = r.Cells[i];
                if (cell.Any(c => char.IsControl(c) && c != '\n'))
                    errors.Add($"{where}: cell {i} contains a control character");
                if (!t.Group.IsLocale && cell.Contains('\n'))
                    errors.Add($"{where}: cell {i} contains a line break (only locale text may)");
            }

            string Get(string column) => r.ByColumn.TryGetValue(column, out var v) ? v.Trim() : "";

            if (t.Group.IsLocale)
            {
                if (string.IsNullOrWhiteSpace(Get("English"))) errors.Add($"{where}: English text is required");
            }
            else if (t.Group.Address == "Crew/Actions")
            {
                if (Get("Type").Length == 0) errors.Add($"{where}: Type is required");
                if (Get("Activator Tags").Length == 0) errors.Add($"{where}: Activator Tags is required");
                var ctx = Get("Contexts").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (ctx.Length == 0) errors.Add($"{where}: Contexts is required (Normal, Queue, NoCharacter, Upgrade)");
                foreach (string c in ctx.Where(c => !Contexts.Contains(c)))
                    errors.Add($"{where}: unknown context \"{c}\" (Normal, Queue, NoCharacter, Upgrade; case-sensitive)");
                CheckParameters(Get("…"), where, errors);
            }
            else if (t.Group.Address == "General/Background Tasks")
            {
                if (Get("Class").Length == 0) errors.Add($"{where}: Class is required");
                if (!StartEvents.Contains(Get("Start Event")))
                    errors.Add($"{where}: Start Event must be one of {string.Join(", ", StartEvents)}");
                CheckParameters(Get("…"), where, errors);
            }
        }
    }

    /// <summary>The "…" column holds "Name = value; Name = value".</summary>
    private static void CheckParameters(string text, string where, List<string> errors)
    {
        if (text.Length == 0) return;
        foreach (string part in text.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0 || eq == part.Length - 1 || part.Substring(0, eq).Trim().Length == 0 || part.Substring(eq + 1).Trim().Length == 0)
                errors.Add($"{where}: parameter \"{part}\" must look like \"Name = value\"");
        }
    }
}

/// <summary>Minimal, deterministic SpreadsheetML writer with inline strings.</summary>
public static class Xlsx
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly DateTimeOffset FixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Write(List<(string Name, List<string[]> Rows)> sheets)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var ct = new StringBuilder();
            ct.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
            ct.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            ct.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            ct.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            ct.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (int i = 1; i <= sheets.Count; i++)
                ct.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            ct.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            ct.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            ct.Append("</Types>");
            Add(zip, "[Content_Types].xml", ct.ToString());

            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"" + Rel + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"" + Rel + "/extended-properties\" Target=\"docProps/app.xml\"/>" +
                "</Relationships>");

            Add(zip, "docProps/app.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\">" +
                "<Application>Damage Control SheetBuilder</Application></Properties>");

            var wb = new StringBuilder();
            wb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
            wb.Append($"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>");
            for (int i = 0; i < sheets.Count; i++)
                wb.Append($"<sheet name=\"{Esc(sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            wb.Append("</sheets></workbook>");
            Add(zip, "xl/workbook.xml", wb.ToString());

            var rels = new StringBuilder();
            rels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
            rels.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= sheets.Count; i++)
                rels.Append($"<Relationship Id=\"rId{i}\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            rels.Append($"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"{Rel}/styles\" Target=\"styles.xml\"/>");
            rels.Append("</Relationships>");
            Add(zip, "xl/_rels/workbook.xml.rels", rels.ToString());

            Add(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                $"<styleSheet xmlns=\"{Main}\">" +
                "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                "</styleSheet>");

            for (int i = 0; i < sheets.Count; i++)
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i].Rows));
        }
        return ms.ToArray();
    }

    private static string Sheet(List<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append($"<worksheet xmlns=\"{Main}\"><sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            int rowNumber = r + 1;
            sb.Append($"<row r=\"{rowNumber}\">");
            string[] cells = rows[r];
            for (int c = 0; c < cells.Length; c++)
            {
                string v = cells[c] ?? "";
                if (v.Length == 0) continue;
                string space = v != v.Trim() || v.Contains('\n') ? " xml:space=\"preserve\"" : "";
                sb.Append($"<c r=\"{Column(c + 1)}{rowNumber}\" t=\"inlineStr\"><is><t{space}>{Esc(v)}</t></is></c>");
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    public static string Column(int number)
    {
        var s = new StringBuilder();
        while (number > 0)
        {
            int m = (number - 1) % 26;
            s.Insert(0, (char)('A' + m));
            number = (number - 1) / 26;
        }
        return s.ToString();
    }

    private static string Esc(string s) => SecurityElement.Escape(s);

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedTime;
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }
}
