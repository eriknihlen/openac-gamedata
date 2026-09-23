using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>One monster name and the creature weenie chosen to stand for it.</summary>
internal sealed record MonsterEntry(
    string Name,
    AceWeenie Weenie,
    int Species,
    int MaximumHealth,
    bool ImmuneToMagic,
    IReadOnlyDictionary<DamageElement, double> Damage);

/// <summary>
/// Builds the monster tables: <c>SpeciesMembers</c>, <c>SpeciesDamages</c>,
/// <c>MonsterDamageOverrides</c> and <c>MonsterImmunities</c>.
/// </summary>
internal static class MonsterTables
{
    /// <summary>The species a monster has when its weenie names none, as the game reports it.</summary>
    public const int NoSpecies = -1;

    /// <summary>
    /// A monster gets its own damage list when following its species' list can
    /// cost more than this share of the damage the best choice would do.
    /// </summary>
    public const double OverrideLoss = 0.10;

    /// <summary>
    /// <c>MonsterImmunities</c> mask for a monster that non-projectile magic
    /// cannot affect. VTank tests the 2 bit (it will not drain such a monster);
    /// the 1 bit goes with it in every row the format is known to carry.
    /// </summary>
    public const int ImmuneToMagicMask = 3;

    /// <summary>
    /// The armor constant of the damage formula: an armor level of A lets
    /// <c>K / (A + K)</c> of the damage through.
    /// </summary>
    private const double ArmorConstant = 200.0 / 3.0;

    public static void Build(AceWorld world, VtankDatabase database)
    {
        IReadOnlyList<MonsterEntry> monsters = Monsters(world);
        Dictionary<int, DamageElement[]> speciesOrder = SpeciesOrders(monsters);

        VtankTable members = GameInfoSchema.NewTable("SpeciesMembers");
        VtankTable overrides = GameInfoSchema.NewTable("MonsterDamageOverrides");
        VtankTable immunities = GameInfoSchema.NewTable("MonsterImmunities");
        foreach (MonsterEntry monster in monsters)
        {
            GameInfoSchema.AddRow(
                members,
                VtankCell.String(monster.Name),
                VtankCell.Int(monster.Species),
                VtankCell.Int(monster.MaximumHealth));
            if (NeedsOverride(monster.Damage, speciesOrder[monster.Species]))
            {
                GameInfoSchema.AddRow(
                    overrides,
                    VtankCell.String(monster.Name),
                    VtankCell.String(DamageElements.Format(Order(monster.Damage))));
            }
            if (monster.ImmuneToMagic)
                GameInfoSchema.AddRow(immunities, VtankCell.String(monster.Name), VtankCell.Int(ImmuneToMagicMask));
        }

        VtankTable species = GameInfoSchema.NewTable("SpeciesDamages");
        foreach ((int id, DamageElement[] order) in speciesOrder.OrderBy(static s => s.Key))
            GameInfoSchema.AddRow(species, VtankCell.Int(id), VtankCell.String(DamageElements.Format(order)));

        database.Tables.Add(("SpeciesMembers", members));
        database.Tables.Add(("SpeciesDamages", species));
        database.Tables.Add(("MonsterDamageOverrides", overrides));
        database.Tables.Add(("MonsterImmunities", immunities));
    }

    /// <summary>
    /// One entry per monster name, sorted by name. Where several creature
    /// weenies share a name, the one the world places most often stands for it
    /// (ties: the lowest class id).
    /// </summary>
    public static IReadOnlyList<MonsterEntry> Monsters(AceWorld world) =>
        world.Weenies.Values
            .Where(IsMonster)
            .GroupBy(static w => w.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(w => world.PlacementsOf(w.ClassId))
                .ThenBy(static w => w.ClassId)
                .First())
            .Select(Entry)
            .OrderBy(static m => m.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>A creature a player can attack.</summary>
    public static bool IsMonster(AceWeenie weenie) =>
        weenie.Type is AceProperty.WeenieType.Creature or AceProperty.WeenieType.Cow
        && !string.IsNullOrWhiteSpace(weenie.Name)
        && weenie.Bool(AceProperty.Bool.Attackable) != false;

    private static MonsterEntry Entry(AceWeenie weenie) => new(
        weenie.Name!.Trim(),
        weenie,
        (int?)weenie.Int(AceProperty.Int.CreatureType) ?? NoSpecies,
        MaximumHealth(weenie),
        weenie.Bool(AceProperty.Bool.NonProjectileMagicImmune) == true,
        DamageTaken(weenie));

    /// <summary>
    /// Maximum health: the health vital's own points plus half the creature's
    /// endurance, rounded half away from zero.
    /// </summary>
    public static int MaximumHealth(AceWeenie weenie)
    {
        uint own = weenie.Vitals.GetValueOrDefault(AceProperty.Vital.MaxHealth).Base;
        uint endurance = weenie.Attributes.GetValueOrDefault(AceProperty.Attribute.Endurance).Base;
        return (int)(own + (uint)Math.Round(endurance / 2.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// How much of a hit of each element reaches the creature: the armor share
    /// (averaged over body parts by how often each is hit) times its natural
    /// resistance to the element.
    /// </summary>
    public static Dictionary<DamageElement, double> DamageTaken(AceWeenie weenie)
    {
        var result = new Dictionary<DamageElement, double>();
        double totalWeight = weenie.BodyParts.Sum(static p => p.HitWeight);
        foreach (DamageElement element in DamageElements.All)
        {
            double armorMod = weenie.Float(DamageElements.ArmorModProperty(element)) ?? 1.0;
            double throughArmor;
            if (weenie.BodyParts.Count == 0)
            {
                throughArmor = 1.0;
            }
            else if (totalWeight > 0)
            {
                throughArmor = weenie.BodyParts.Sum(p => p.HitWeight * ArmorShare(p.BaseArmor * armorMod)) / totalWeight;
            }
            else
            {
                throughArmor = weenie.BodyParts.Average(p => ArmorShare(p.BaseArmor * armorMod));
            }
            double resist = weenie.Float(DamageElements.ResistProperty(element)) ?? 1.0;
            result[element] = throughArmor * resist;
        }
        return result;
    }

    /// <summary>The share of a hit that armor of level <paramref name="armor"/> lets through.</summary>
    public static double ArmorShare(double armor) => armor switch
    {
        > 0 => ArmorConstant / (armor + ArmorConstant),
        < 0 => 1.0 - armor / ArmorConstant,
        _ => 1.0,
    };

    /// <summary>Every element, most damage first; ties keep <see cref="DamageElements.All"/>'s order.</summary>
    public static DamageElement[] Order(IReadOnlyDictionary<DamageElement, double> damage) =>
        DamageElements.All.OrderByDescending(e => Math.Round(damage[e], 6)).ToArray();

    /// <summary>
    /// Each species' list, from the median over its monsters of the damage each
    /// element does.
    /// </summary>
    public static Dictionary<int, DamageElement[]> SpeciesOrders(IEnumerable<MonsterEntry> monsters) =>
        monsters
            .GroupBy(static m => m.Species)
            .ToDictionary(
                static g => g.Key,
                static g => Order(DamageElements.All.ToDictionary(
                    e => e,
                    e => Median(g.Select(m => m.Damage[e])))));

    /// <summary>
    /// True when some pair of elements a player could have puts the species
    /// list's pick more than <see cref="OverrideLoss"/> behind the better one.
    /// VTank uses the first listed element the player has, so two elements
    /// decide every case.
    /// </summary>
    public static bool NeedsOverride(IReadOnlyDictionary<DamageElement, double> damage, DamageElement[] speciesOrder)
    {
        for (int first = 0; first < speciesOrder.Length; first++)
        {
            for (int later = first + 1; later < speciesOrder.Length; later++)
            {
                if (damage[speciesOrder[first]] < (1 - OverrideLoss) * damage[speciesOrder[later]])
                    return true;
            }
        }
        return false;
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
