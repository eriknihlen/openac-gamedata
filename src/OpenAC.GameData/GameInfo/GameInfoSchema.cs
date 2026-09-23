using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>
/// The tables of a VTank game database and their columns, in the order and
/// with the index flags VTank's own built-in (empty) database has. VTank
/// discards a profile-directory database whose <c>DBVersion</c> differs from
/// its built-in one, so the version is fixed too.
/// </summary>
internal static class GameInfoSchema
{
    public const int DatabaseVersion = 9;

    private static readonly Dictionary<string, (string Column, bool Index)[]> Columns = new(StringComparer.Ordinal)
    {
        ["AmmunitionOptions"] =
        [
            ("AmmoName", true), ("LauncherType", false), ("WieldReq", false), ("Element", false),
            ("Quality", false), ("Special", false), ("WieldReq2Skill", false), ("WieldReq2Value", false),
        ],
        ["CooldownIDs"] = [("Itemname", true), ("CooldownID", false)],
        ["CraftInteractions"] =
        [
            ("UseItem1", false), ("UseItem2", false), ("ResultItem", false), ("ResultCount", false),
            ("SuccessMsg", false), ("FailMsg", false), ("ReqSkill", false), ("ReqDiff", false), ("ID", true),
        ],
        ["DBLastUpdateTime"] = [("Zero", false), ("Time", false)],
        ["DBVersion"] = [("VersionInt", false)],
        ["DrainSpellOptions"] =
        [
            ("SpellID", true), ("CastTimeMilliseconds", false), ("EnemyDrainFactor", false),
            ("EnemyDrainMaximumPoints", false), ("ResultMultiplier", false),
        ],
        ["GrenadeOptions"] =
        [
            ("GrenName", true), ("WieldReqType", false), ("WieldReqAttribute", false),
            ("WieldReqValue", false), ("Spell", false), ("Spellcraft", false),
        ],
        ["HealKits"] = [("KitName", true), ("RestoreBonus", false), ("SkillBonus", false), ("WhichVital", false)],
        ["MartyrSpellOptions"] =
        [
            ("SpellID", true), ("CastTimeMilliseconds", false), ("SelfDrainFactor", false), ("ResultMultiplier", false),
        ],
        ["MonsterDamageOverrides"] = [("Monster", true), ("DamageString", false)],
        ["MonsterImmunities"] = [("Monster", true), ("ImmunityMask", false)],
        ["SpeciesDamages"] = [("Species", true), ("DamageString", false)],
        ["SpeciesMembers"] = [("Monster", true), ("Species", false), ("MaximumHealth", false)],
        ["SpellQualityOverrides"] = [("SpellID", true), ("Valid", false), ("NewQuality", false), ("NewFamily", false)],
    };

    public static IEnumerable<string> TableNames => Columns.Keys;

    /// <summary>An empty table with <paramref name="name"/>'s columns.</summary>
    public static VtankTable NewTable(string name)
    {
        var table = new VtankTable();
        foreach ((string column, bool index) in Columns[name])
        {
            table.ColumnNames.Add(column);
            table.IndexFlags.Add(index);
        }
        return table;
    }

    /// <summary>Adds a row, checking it has one cell per column.</summary>
    public static void AddRow(VtankTable table, params VtankCell[] cells)
    {
        if (cells.Length != table.ColumnNames.Count)
        {
            throw new ArgumentException(
                $"Row has {cells.Length} cells; the table has {table.ColumnNames.Count} columns.",
                nameof(cells));
        }
        var row = new VtankRow();
        row.Cells.AddRange(cells);
        table.Rows.Add(row);
    }
}
