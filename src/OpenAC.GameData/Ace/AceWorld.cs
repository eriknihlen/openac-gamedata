using OpenAC.GameData.Sql;

namespace OpenAC.GameData.Ace;

/// <summary>One body part of a creature: its armor and how often it is hit.</summary>
internal readonly record struct AceBodyPart(int Key, int BaseArmor, double HitWeight);

/// <summary>An attribute or vital as the world data stores it.</summary>
internal readonly record struct AceStat(uint InitLevel, uint Ranks)
{
    public uint Base => InitLevel + Ranks;
}

/// <summary>One weenie (object template) with the properties the generator reads.</summary>
internal sealed class AceWeenie(uint classId, string className, int type)
{
    public uint ClassId { get; } = classId;
    public string ClassName { get; } = className;
    public int Type { get; } = type;
    public Dictionary<int, long> Ints { get; } = [];
    public Dictionary<int, double> Floats { get; } = [];
    public Dictionary<int, bool> Bools { get; } = [];
    public Dictionary<int, string> Strings { get; } = [];
    public Dictionary<int, uint> DataIds { get; } = [];
    public Dictionary<int, AceStat> Attributes { get; } = [];
    public Dictionary<int, AceStat> Vitals { get; } = [];
    public List<AceBodyPart> BodyParts { get; } = [];

    public string? Name => Strings.GetValueOrDefault(AceProperty.String.Name);

    public long? Int(int key) => Ints.TryGetValue(key, out long value) ? value : null;

    public double? Float(int key) => Floats.TryGetValue(key, out double value) ? value : null;

    public bool? Bool(int key) => Bools.TryGetValue(key, out bool value) ? value : null;

    public override string ToString() => $"{ClassId} {ClassName} '{Name}'";
}

/// <summary>The part of ACE's world database the generator reads.</summary>
internal sealed class AceWorld
{
    /// <summary>The dump tables <see cref="Load"/> reads.</summary>
    public static readonly string[] Tables =
    [
        "weenie",
        "weenie_properties_int",
        "weenie_properties_float",
        "weenie_properties_bool",
        "weenie_properties_string",
        "weenie_properties_d_i_d",
        "weenie_properties_attribute",
        "weenie_properties_attribute_2nd",
        "weenie_properties_body_part",
        "landblock_instance",
        "weenie_properties_generator",
        "weenie_properties_create_list",
        "weenie_properties_emote_action",
        "recipe",
        "cook_book",
        "spell",
    ];

    private AceWorld(
        Dictionary<uint, AceWeenie> weenies,
        Dictionary<uint, int> placements,
        AceItemSources sources,
        Dictionary<uint, AceSpell> spells)
    {
        Spells = spells;
        Weenies = weenies;
        Placements = placements;
        Sources = sources;
    }

    /// <summary>ACE's spell table, by spell id.</summary>
    public IReadOnlyDictionary<uint, AceSpell> Spells { get; }

    /// <summary>Where items come from: vendors, NPC gifts and recipes.</summary>
    public AceItemSources Sources { get; }

    public IReadOnlyDictionary<uint, AceWeenie> Weenies { get; }

    /// <summary>
    /// How many times the world puts each weenie class somewhere: a landblock
    /// placement, or a generator profile that names it. A class absent here is
    /// only made some other way (a quest, a summons, a treasure roll) or never.
    /// </summary>
    public IReadOnlyDictionary<uint, int> Placements { get; }

    public static AceWorld Load(string dumpPath) => FromTables(SqlDump.Load(dumpPath, Tables));

    public static AceWorld FromTables(IReadOnlyDictionary<string, SqlTable> tables)
    {
        var weenies = new Dictionary<uint, AceWeenie>();
        SqlTable weenie = tables["weenie"];
        int id = weenie.Column("class_Id"), name = weenie.Column("class_Name"), type = weenie.Column("type");
        foreach (object?[] row in weenie.Rows)
        {
            uint classId = U(row[id]);
            weenies[classId] = new AceWeenie(classId, (string)row[name]!, (int)(long)row[type]!);
        }

        ReadKeyed(tables["weenie_properties_int"], weenies, static (w, k, v) => w.Ints[k] = (long)v!);
        ReadKeyed(tables["weenie_properties_float"], weenies, static (w, k, v) => w.Floats[k] = D(v));
        ReadKeyed(tables["weenie_properties_bool"], weenies, static (w, k, v) => w.Bools[k] = Bit(v));
        ReadKeyed(tables["weenie_properties_string"], weenies, static (w, k, v) => w.Strings[k] = (string)v!);
        ReadKeyed(tables["weenie_properties_d_i_d"], weenies, static (w, k, v) => w.DataIds[k] = U(v));
        ReadStats(tables["weenie_properties_attribute"], weenies, static w => w.Attributes);
        ReadStats(tables["weenie_properties_attribute_2nd"], weenies, static w => w.Vitals);
        ReadBodyParts(tables["weenie_properties_body_part"], weenies);

        var placements = new Dictionary<uint, int>();
        SqlTable instances = tables["landblock_instance"];
        int wcid = instances.Column("weenie_Class_Id");
        foreach (object?[] row in instances.Rows)
            Count(placements, U(row[wcid]));
        SqlTable generators = tables["weenie_properties_generator"];
        int generated = generators.Column("weenie_Class_Id");
        foreach (object?[] row in generators.Rows)
            Count(placements, U(row[generated]));

        return new AceWorld(weenies, placements, AceItemSources.FromTables(tables, weenies), AceSpell.FromTable(tables["spell"]));
    }

    private static void ReadKeyed(
        SqlTable table,
        Dictionary<uint, AceWeenie> weenies,
        Action<AceWeenie, int, object?> set)
    {
        int owner = table.Column("object_Id"), key = table.Column("type"), value = table.Column("value");
        foreach (object?[] row in table.Rows)
        {
            if (weenies.TryGetValue(U(row[owner]), out AceWeenie? w))
                set(w, (int)(long)row[key]!, row[value]);
        }
    }

    private static void ReadStats(
        SqlTable table,
        Dictionary<uint, AceWeenie> weenies,
        Func<AceWeenie, Dictionary<int, AceStat>> target)
    {
        int owner = table.Column("object_Id"), key = table.Column("type");
        int init = table.Column("init_Level"), ranks = table.Column("level_From_C_P");
        foreach (object?[] row in table.Rows)
        {
            if (weenies.TryGetValue(U(row[owner]), out AceWeenie? w))
                target(w)[(int)(long)row[key]!] = new AceStat(U(row[init]), U(row[ranks]));
        }
    }

    private static void ReadBodyParts(SqlTable table, Dictionary<uint, AceWeenie> weenies)
    {
        int owner = table.Column("object_Id"), key = table.Column("key"), armor = table.Column("base_Armor");
        string[] quadrants =
            ["h_l_f", "m_l_f", "l_l_f", "h_r_f", "m_r_f", "l_r_f", "h_l_b", "m_l_b", "l_l_b", "h_r_b", "m_r_b", "l_r_b"];
        int[] quadrant = quadrants.Select(table.Column).ToArray();
        foreach (object?[] row in table.Rows)
        {
            if (!weenies.TryGetValue(U(row[owner]), out AceWeenie? w))
                continue;
            double weight = 0;
            foreach (int q in quadrant)
                weight += D(row[q]);
            w.BodyParts.Add(new AceBodyPart((int)(long)row[key]!, (int)(long)row[armor]!, weight));
        }
    }

    public int PlacementsOf(uint classId) => Placements.GetValueOrDefault(classId);

    private static void Count(Dictionary<uint, int> counts, uint classId) =>
        counts[classId] = counts.GetValueOrDefault(classId) + 1;

    private static uint U(object? value) => unchecked((uint)(long)value!);

    /// <summary>A <c>bit(1)</c> column: the dump writes it as a one-character string.</summary>
    private static bool Bit(object? value) => value switch
    {
        long whole => whole != 0,
        string { Length: 1 } text => text[0] != '\0',
        _ => throw new InvalidDataException($"Expected a bit value, found {value ?? "NULL"}."),
    };

    private static double D(object? value) => value switch
    {
        long whole => whole,
        double real => real,
        _ => throw new InvalidDataException($"Expected a number, found {value ?? "NULL"}."),
    };
}
