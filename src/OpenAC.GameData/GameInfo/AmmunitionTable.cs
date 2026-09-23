using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>One <c>AmmunitionOptions</c> row.</summary>
internal sealed record AmmunitionOption(
    string Name,
    int LauncherType,
    int WieldRequirement,
    DamageElement Element,
    int Quality,
    int Special,
    int SecondSkill,
    int SecondRequirement,
    AceWeenie Weenie);

/// <summary>
/// Builds <c>AmmunitionOptions</c>: every arrow, bolt and atlatl dart a player
/// can get, with what it needs to be used and how good it is.
/// </summary>
internal static class AmmunitionTable
{
    /// <summary>VTank's launcher numbers.</summary>
    public const int Bow = 5, Crossbow = 6, Atlatl = 7;

    /// <summary>
    /// <c>Special</c> groups, which VTank uses only when the player opts in:
    /// ammunition that cannot be had for pyreals or as loot. 1: it (or its
    /// parts) is bought with an alternate currency (raid tokens and the like).
    /// 2: only NPCs hand it (or its parts) out.
    /// </summary>
    public const int SpecialBoughtWithCurrency = 1, SpecialOther = 2;

    private static readonly (int Kind, int Skill, int Difficulty)[] RequirementSets =
    [
        (AceProperty.Int.WieldRequirements, AceProperty.Int.WieldSkillType, AceProperty.Int.WieldDifficulty),
        (AceProperty.Int.WieldRequirements2, AceProperty.Int.WieldSkillType2, AceProperty.Int.WieldDifficulty2),
        (AceProperty.Int.WieldRequirements3, AceProperty.Int.WieldSkillType3, AceProperty.Int.WieldDifficulty3),
    ];

    public static void Build(AceWorld world, VtankDatabase database)
    {
        VtankTable table = GameInfoSchema.NewTable("AmmunitionOptions");
        foreach (AmmunitionOption option in Options(world))
        {
            GameInfoSchema.AddRow(
                table,
                VtankCell.String(option.Name),
                VtankCell.Int(option.LauncherType),
                VtankCell.Int(option.WieldRequirement),
                VtankCell.Int((int)option.Element),
                VtankCell.Int(option.Quality),
                VtankCell.Int(option.Special),
                VtankCell.Int(option.SecondSkill),
                VtankCell.Int(option.SecondRequirement));
        }
        database.Tables.Add(("AmmunitionOptions", table));
    }

    /// <summary>
    /// One option per ammunition name a player can get (see
    /// <see cref="AceItemSources.Acquisition"/>). Where several weenies share a name, the lowest class id
    /// among the obtainable ones stands for it. Rows are ordered by launcher,
    /// element, quality and name; VTank takes the later of two rows of equal
    /// quality.
    /// </summary>
    public static IReadOnlyList<AmmunitionOption> Options(AceWorld world) =>
        world.Weenies.Values
            .Where(static w => w.Type == AceProperty.WeenieType.Ammunition && !string.IsNullOrWhiteSpace(w.Name))
            .Where(static w => Launcher(w) != 0 && Element(w) is not null)
            .Where(w => !IsCreatureGearOnly(world.Sources, w))
            .GroupBy(static w => w.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(w => world.Sources.Acquisition(w.ClassId)).ThenBy(static w => w.ClassId).First())
            .Select(w => Option(world.Sources, w))
            .OrderBy(static o => o.LauncherType)
            .ThenBy(static o => o.Element)
            .ThenBy(static o => o.Quality)
            .ThenBy(static o => o.Name, StringComparer.Ordinal)
            .ToList();

    private static AmmunitionOption Option(AceItemSources sources, AceWeenie weenie)
    {
        int missile = 0, secondSkill = 0, secondValue = 0;
        foreach ((int kindKey, int skillKey, int difficultyKey) in RequirementSets)
        {
            long? kind = weenie.Int(kindKey);
            if (kind is not (AceProperty.WieldRequirement.Skill or AceProperty.WieldRequirement.RawSkill))
                continue;
            int skill = (int)(weenie.Int(skillKey) ?? 0);
            int difficulty = (int)(weenie.Int(difficultyKey) ?? 0);
            if (skill == AceProperty.MissileWeaponsSkill)
            {
                missile = Math.Max(missile, difficulty);
            }
            else if (secondSkill == 0)
            {
                secondSkill = skill;
                secondValue = difficulty;
            }
        }
        return new AmmunitionOption(
            weenie.Name!.Trim(),
            Launcher(weenie),
            missile,
            Element(weenie)!.Value,
            Quality(weenie),
            Special(sources, weenie),
            secondSkill,
            secondValue,
            weenie);
    }

    public static int Launcher(AceWeenie weenie) => weenie.Int(AceProperty.Int.AmmoType) switch
    {
        AceProperty.AmmoType.Arrow => Bow,
        AceProperty.AmmoType.Bolt => Crossbow,
        AceProperty.AmmoType.Atlatl => Atlatl,
        _ => 0,
    };

    /// <summary>The ammunition's element; one that takes the launcher's element is prismatic.</summary>
    public static DamageElement? Element(AceWeenie weenie)
    {
        var type = (AceProperty.DamageType)(weenie.Int(AceProperty.Int.DamageType) ?? 0);
        return type == AceProperty.DamageType.Base
            ? DamageElement.PrismaticAmmunition
            : DamageElements.FromAce(type);
    }

    /// <summary>
    /// Our ranking: the average damage of one shot, in tenths of a point, so
    /// that two kinds a whole point apart in average damage still rank apart.
    /// Only rows of the same launcher and element (and prismatic ones) are ever
    /// compared, and VTank subtracts 1000 from a prismatic row it is told to
    /// avoid, which has to outweigh any real difference.
    /// </summary>
    public static int Quality(AceWeenie weenie)
    {
        double damage = weenie.Int(AceProperty.Int.Damage) ?? 0;
        double variance = weenie.Float(AceProperty.Float.DamageVariance) ?? 0;
        return (int)Math.Round(10 * damage * (1 - variance / 2), MidpointRounding.AwayFromZero);
    }

    public static int Special(AceItemSources sources, AceWeenie weenie) => sources.Acquisition(weenie.ClassId) switch
    {
        AceAcquisition.Ordinary => 0,
        AceAcquisition.Currency => SpecialBoughtWithCurrency,
        _ => SpecialOther,
    };

    /// <summary>
    /// Ammunition that exists only as a creature's own wielded equipment: no
    /// player can have it, and its damage is set for the creature.
    /// </summary>
    private static bool IsCreatureGearOnly(AceItemSources sources, AceWeenie weenie) =>
        sources.Acquisition(weenie.ClassId) == AceAcquisition.None && sources.IsWielded(weenie.ClassId);
}
