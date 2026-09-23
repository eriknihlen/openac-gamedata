using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>One <c>HealKits</c> row.</summary>
internal sealed record HealKitOption(string Name, double RestoreBonus, int SkillBonus, int Vital);

/// <summary>One <c>GrenadeOptions</c> row.</summary>
internal sealed record GrenadeOption(
    string Name,
    int WieldRequirementType,
    int WieldRequirementSkill,
    int WieldRequirementValue,
    uint SpellId,
    int Spellcraft);

/// <summary>
/// Builds the item tables: <c>HealKits</c>, <c>GrenadeOptions</c> (thrown
/// items that cast a spell where they land) and <c>CooldownIDs</c> (items that
/// share a use timer).
/// </summary>
internal static class ItemTables
{
    private const int HealerWeenieType = 28;
    private const int MissileWeenieType = 4;
    private const int HealkitMod = 100;
    private const int BoosterEnum = 89;
    private const int BoostValue = 90;
    private const int ItemSpellcraft = 106;
    private const int SharedCooldown = 280;
    private const int ProcSpellDataId = 55;
    private const int ProcSpellRate = 156;

    /// <summary>VTank's vital numbers for a heal kit.</summary>
    public const int Health = 1, Stamina = 2, Mana = 3;

    /// <summary>
    /// A shared cooldown shows up in the game as an enchantment whose spell id
    /// is the cooldown number with the top bit of a 16-bit id set; VTank keeps
    /// that id as a signed number.
    /// </summary>
    private const int CooldownBase = -0x8000;

    public static void Build(AceWorld world, VtankDatabase database)
    {
        VtankTable kits = GameInfoSchema.NewTable("HealKits");
        foreach (HealKitOption kit in HealKits(world))
        {
            GameInfoSchema.AddRow(
                kits,
                VtankCell.String(kit.Name),
                VtankCell.Double(kit.RestoreBonus),
                VtankCell.Int(kit.SkillBonus),
                VtankCell.Int(kit.Vital));
        }
        database.Tables.Add(("HealKits", kits));

        VtankTable grenades = GameInfoSchema.NewTable("GrenadeOptions");
        foreach (GrenadeOption grenade in Grenades(world))
        {
            GameInfoSchema.AddRow(
                grenades,
                VtankCell.String(grenade.Name),
                VtankCell.Int(grenade.WieldRequirementType),
                VtankCell.Int(grenade.WieldRequirementSkill),
                VtankCell.Int(grenade.WieldRequirementValue),
                VtankCell.Int(unchecked((int)grenade.SpellId)),
                VtankCell.Int(grenade.Spellcraft));
        }
        database.Tables.Add(("GrenadeOptions", grenades));

        VtankTable cooldowns = GameInfoSchema.NewTable("CooldownIDs");
        foreach ((string name, int id) in Cooldowns(world))
            GameInfoSchema.AddRow(cooldowns, VtankCell.String(name), VtankCell.Int(id));
        database.Tables.Add(("CooldownIDs", cooldowns));
    }

    /// <summary>Healing, stamina and mana kits: how much they add to the skill and to what is restored.</summary>
    public static IReadOnlyList<HealKitOption> HealKits(AceWorld world) =>
        Representatives(world, static w => w.Type == HealerWeenieType && KitVital(w) != 0)
            .Select(static w => new HealKitOption(
                w.Name!.Trim(),
                Math.Round(w.Float(HealkitMod) ?? 1.0, 6),
                (int)(w.Int(BoostValue) ?? 0),
                KitVital(w)))
            .ToList();

    /// <summary>
    /// Thrown items that cast their spell every time they land (they carry a
    /// spell, the spellcraft it is cast at, and a spell rate of 1; a thrown
    /// weapon that only sometimes casts is a weapon), with the wield
    /// requirement a player must meet.
    /// </summary>
    public static IReadOnlyList<GrenadeOption> Grenades(AceWorld world) =>
        Representatives(
                world,
                static w => w.Type == MissileWeenieType
                    && w.DataIds.GetValueOrDefault(ProcSpellDataId) != 0
                    && w.Int(ItemSpellcraft) is > 0
                    && w.Float(ProcSpellRate) is >= 1)
            .Select(static w => new GrenadeOption(
                w.Name!.Trim(),
                (int)(w.Int(AceProperty.Int.WieldRequirements) ?? 0),
                (int)(w.Int(AceProperty.Int.WieldSkillType) ?? 0),
                (int)(w.Int(AceProperty.Int.WieldDifficulty) ?? 0),
                w.DataIds[ProcSpellDataId],
                (int)w.Int(ItemSpellcraft)!.Value))
            .ToList();

    /// <summary>Items that share a use timer, with VTank's id for the timer.</summary>
    public static IReadOnlyList<(string Name, int Id)> Cooldowns(AceWorld world) =>
        Representatives(world, static w => w.Int(SharedCooldown) is > 0)
            .Select(static w => (w.Name!.Trim(), CooldownBase + (int)w.Int(SharedCooldown)!.Value))
            .ToList();

    /// <summary>The vital a kit restores, in VTank's numbers, or 0 when it restores none.</summary>
    public static int KitVital(AceWeenie weenie) => weenie.Int(BoosterEnum) switch
    {
        2 => Health,
        4 => Stamina,
        6 => Mana,
        _ => 0,
    };

    /// <summary>
    /// One weenie per name among those matching: the easiest to have, then the
    /// lowest class id; names only a creature wields are left out. Sorted by name.
    /// </summary>
    private static IEnumerable<AceWeenie> Representatives(AceWorld world, Func<AceWeenie, bool> match) =>
        world.Weenies.Values
            .Where(w => !string.IsNullOrWhiteSpace(w.Name) && match(w))
            .Where(w => world.Sources.Acquisition(w.ClassId) != AceAcquisition.None || !world.Sources.IsWielded(w.ClassId))
            .GroupBy(static w => w.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(w => world.Sources.Acquisition(w.ClassId)).ThenBy(static w => w.ClassId).First())
            .OrderBy(static w => w.Name!.Trim(), StringComparer.Ordinal);
}
