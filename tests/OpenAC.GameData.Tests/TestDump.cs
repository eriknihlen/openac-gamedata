using System.Globalization;
using System.Text;
using OpenAC.GameData.Ace;
using OpenAC.GameData.Sql;

namespace OpenAC.GameData.Tests;

/// <summary>
/// Writes a small <c>mysqldump</c>-shaped world database in memory, with the
/// tables and columns the generator reads, so tests run the whole parse and
/// build path on data they set up.
/// </summary>
internal sealed class TestDump
{
    private static readonly string[] Quadrants =
        ["h_l_f", "m_l_f", "l_l_f", "h_r_f", "m_r_f", "l_r_f", "h_l_b", "m_l_b", "l_l_b", "h_r_b", "m_r_b", "l_r_b"];

    private static readonly Dictionary<string, string[]> Columns = new()
    {
        ["weenie"] = ["class_Id", "class_Name", "type", "last_Modified"],
        ["weenie_properties_int"] = ["id", "object_Id", "type", "value"],
        ["weenie_properties_float"] = ["id", "object_Id", "type", "value"],
        ["weenie_properties_bool"] = ["id", "object_Id", "type", "value"],
        ["weenie_properties_string"] = ["id", "object_Id", "type", "value"],
        ["weenie_properties_d_i_d"] = ["id", "object_Id", "type", "value"],
        ["weenie_properties_attribute"] = ["id", "object_Id", "type", "init_Level", "level_From_C_P", "c_P_Spent"],
        ["weenie_properties_attribute_2nd"] =
            ["id", "object_Id", "type", "init_Level", "level_From_C_P", "c_P_Spent", "current_Level"],
        ["weenie_properties_body_part"] =
        [
            "id", "object_Id", "key", "d_Type", "d_Val", "d_Var", "base_Armor",
            "armor_Vs_Slash", "armor_Vs_Pierce", "armor_Vs_Bludgeon", "armor_Vs_Cold", "armor_Vs_Fire",
            "armor_Vs_Acid", "armor_Vs_Electric", "armor_Vs_Nether", "b_h", .. Quadrants,
        ],
        ["landblock_instance"] = ["guid", "landblock", "weenie_Class_Id", "obj_Cell_Id", "last_Modified"],
        ["weenie_properties_generator"] = ["id", "object_Id", "probability", "weenie_Class_Id"],
        ["weenie_properties_create_list"] =
            ["id", "object_Id", "destination_Type", "weenie_Class_Id", "stack_Size", "palette", "shade", "try_To_Bond"],
        ["weenie_properties_emote_action"] = ["id", "emote_Id", "order", "type", "message", "weenie_Class_Id"],
        ["recipe"] =
        [
            "id", "unknown_1", "skill", "difficulty", "salvage_Type", "success_W_C_I_D", "success_Amount",
            "success_Message", "fail_W_C_I_D", "fail_Amount", "fail_Message",
        ],
        ["cook_book"] = ["id", "recipe_Id", "source_W_C_I_D", "target_W_C_I_D", "last_Modified"],
    };

    private readonly Dictionary<string, List<string>> _rows = Columns.Keys.ToDictionary(k => k, _ => new List<string>());
    private int _nextId = 1;

    public const int Vendor = 12;

    public TestDump Weenie(uint classId, int type, string? name = null)
    {
        Add("weenie", classId, Quote($"class{classId}"), type, Quote("2026-01-01 00:00:00"));
        if (name is not null)
            String(classId, AceProperty.String.Name, name);
        return this;
    }

    public TestDump Creature(uint classId, string name) => Weenie(classId, AceProperty.WeenieType.Creature, name);

    public TestDump Int(uint classId, int key, long value) => Keyed("weenie_properties_int", classId, key, value);

    public TestDump Float(uint classId, int key, double value) =>
        Keyed("weenie_properties_float", classId, key, value.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>A bool the way the dump writes a <c>bit(1)</c>: a one-character string.</summary>
    public TestDump Bool(uint classId, int key, bool value) =>
        Keyed("weenie_properties_bool", classId, key, value ? "'\u0001'" : "'\\0'");

    public TestDump String(uint classId, int key, string value) => Keyed("weenie_properties_string", classId, key, Quote(value));

    public TestDump DataId(uint classId, int key, uint value) => Keyed("weenie_properties_d_i_d", classId, key, value);

    public TestDump Attribute(uint classId, int type, uint init, uint ranks = 0) =>
        Add("weenie_properties_attribute", _nextId++, classId, type, init, ranks, 0);

    public TestDump Vital(uint classId, int type, uint init, uint ranks = 0) =>
        Add("weenie_properties_attribute_2nd", _nextId++, classId, type, init, ranks, 0, 0);

    /// <summary>A body part; its whole hit weight goes in one quadrant.</summary>
    public TestDump BodyPart(uint classId, int key, int baseArmor, double hitWeight)
    {
        var values = new List<object> { _nextId++, classId, key, 1, 1, 0.5, baseArmor, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
        values.Add(hitWeight.ToString("R", CultureInfo.InvariantCulture));
        values.AddRange(Enumerable.Repeat<object>(0, Quadrants.Length - 1));
        return Add("weenie_properties_body_part", [.. values]);
    }

    public TestDump Placed(uint classId) => Add("landblock_instance", _nextId++, 0, classId, 0, Quote("2026-01-01 00:00:00"));

    public TestDump Generated(uint generator, uint classId) => Add("weenie_properties_generator", _nextId++, generator, 1, classId);

    public TestDump CreateList(uint owner, int destination, uint classId) =>
        Add("weenie_properties_create_list", _nextId++, owner, destination, classId, 1, 0, 0, "'\\0'");

    /// <summary>A vendor (optionally trading in an alternate currency) that sells the item.</summary>
    public TestDump SoldBy(uint vendor, uint classId, uint? currency = null)
    {
        Weenie(vendor, Vendor, $"Vendor {vendor}");
        if (currency is { } item)
            DataId(vendor, 57, item);
        return CreateList(vendor, 4, classId);
    }

    public TestDump GivenBy(uint emote, uint classId) =>
        Add("weenie_properties_emote_action", _nextId++, emote, 0, 3, "NULL", classId);

    public TestDump Recipe(uint id, uint makes, uint source, uint target, int skill = 37, int difficulty = 0)
    {
        Add("recipe", id, 0, skill, difficulty, 0, makes, 100, Quote("made"), 0, 0, Quote("failed"));
        return Add("cook_book", _nextId++, id, source, target, Quote("2026-01-01 00:00:00"));
    }

    public string Sql()
    {
        var sb = new StringBuilder("-- test dump\n");
        foreach ((string table, string[] columns) in Columns)
        {
            sb.Append("CREATE TABLE `").Append(table).Append("` (\n");
            foreach (string column in columns)
                sb.Append("  `").Append(column).Append("` int(10) NOT NULL,\n");
            sb.Append("  PRIMARY KEY (`").Append(columns[0]).Append("`)\n) ENGINE=InnoDB;\n");
            List<string> rows = _rows[table];
            if (rows.Count == 0)
                continue;
            // One table uses the named-column form the real dump uses for it.
            string names = table == "landblock_instance"
                ? " (" + string.Join(", ", columns.Select(c => $"`{c}`")) + ")"
                : string.Empty;
            sb.Append("INSERT INTO `").Append(table).Append('`').Append(names).Append(" VALUES ")
                .Append(string.Join(',', rows)).Append(";\n");
        }
        return sb.ToString();
    }

    public AceWorld World() => AceWorld.FromTables(SqlDump.Read(new StringReader(Sql()), AceWorld.Tables));

    private TestDump Keyed(string table, uint classId, int key, object value) => Add(table, _nextId++, classId, key, value);

    private TestDump Add(string table, params object[] values)
    {
        if (values.Length != Columns[table].Length)
            throw new ArgumentException($"{table} has {Columns[table].Length} columns; got {values.Length} values.");
        _rows[table].Add("(" + string.Join(',', values.Select(Format)) + ")");
        return this;
    }

    private static string Format(object value) => value switch
    {
        string text => text,
        double real => real.ToString("R", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };

    private static string Quote(string text) =>
        "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";
}
