using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>One <c>DrainSpellOptions</c> row.</summary>
internal sealed record DrainSpellOption(
    uint SpellId,
    int CastTimeMilliseconds,
    double EnemyDrainFactor,
    int EnemyDrainMaximumPoints,
    double ResultMultiplier);

/// <summary>One <c>MartyrSpellOptions</c> row.</summary>
internal sealed record MartyrSpellOption(
    uint SpellId,
    int CastTimeMilliseconds,
    double SelfDrainFactor,
    double ResultMultiplier);

/// <summary>
/// Builds <c>DrainSpellOptions</c> (spells that take health from the target
/// and give it to the caster) and <c>MartyrSpellOptions</c> (spells that spend
/// the caster's own health to hurt the target), from ACE's spell table.
/// </summary>
internal static class SpellTables
{
    private const int HealthVital = 2;
    private const int HealthDamage = 0x80;
    private const int TargetSourceCasterDestination = 0x2 | 0x4;

    /// <summary>
    /// How long each of these spells takes to cast, in milliseconds, as known
    /// from play. The world data has no cast times.
    /// </summary>
    private static readonly Dictionary<uint, int> KnownCastTimes = new()
    {
        [1237] = 500, [1238] = 1100, [1239] = 1550, [1240] = 2000,
        [1241] = 2400, [1242] = 2300, [2328] = 2400, [4643] = 2400,
        [2760] = 550, [2761] = 1150, [2762] = 1600, [2763] = 2050,
        [2764] = 2450, [2765] = 2350, [2766] = 2450, [3818] = 2850,
    };

    /// <summary>For a spell with no known cast time: about half a second per level, at most 2.5 seconds.</summary>
    public static int EstimatedCastTime(int level) => Math.Min(500 * level, 2500);

    public static void Build(AceWorld world, VtankDatabase database)
    {
        VtankTable drains = GameInfoSchema.NewTable("DrainSpellOptions");
        foreach (DrainSpellOption option in DrainOptions(world))
        {
            GameInfoSchema.AddRow(
                drains,
                VtankCell.Int(unchecked((int)option.SpellId)),
                VtankCell.Int(option.CastTimeMilliseconds),
                VtankCell.Double(option.EnemyDrainFactor),
                VtankCell.Int(option.EnemyDrainMaximumPoints),
                VtankCell.Double(option.ResultMultiplier));
        }
        database.Tables.Add(("DrainSpellOptions", drains));

        VtankTable martyrs = GameInfoSchema.NewTable("MartyrSpellOptions");
        foreach (MartyrSpellOption option in MartyrOptions(world))
        {
            GameInfoSchema.AddRow(
                martyrs,
                VtankCell.Int(unchecked((int)option.SpellId)),
                VtankCell.Int(option.CastTimeMilliseconds),
                VtankCell.Double(option.SelfDrainFactor),
                VtankCell.Double(option.ResultMultiplier));
        }
        database.Tables.Add(("MartyrSpellOptions", martyrs));
    }

    /// <summary>
    /// Health drains: the target's health is the source, the caster's the
    /// destination. The caster gains <c>1 - loss</c> of what is taken; the
    /// transfer cap limits both what is taken and what is given, so at most
    /// <c>cap / max(1, multiplier)</c> is taken. Ordered weakest first.
    /// </summary>
    public static IReadOnlyList<DrainSpellOption> DrainOptions(AceWorld world)
    {
        AceSpell[] spells = world.Spells.Values
            .Where(static s => s.Source == HealthVital
                && s.Destination == HealthVital
                && s.TransferFlags == TargetSourceCasterDestination
                && s.Proportion is > 0
                && s.TransferCap is > 0)
            .OrderBy(static s => s.TransferCap)
            .ThenBy(static s => s.Id)
            .ToArray();
        return spells
            .Select((s, rank) =>
            {
                double multiplier = Math.Round(1 - (s.LossPercent ?? 0), 6);
                return new DrainSpellOption(
                    s.Id,
                    CastTime(s.Id, rank + 1),
                    Math.Round(s.Proportion!.Value, 6),
                    (int)Math.Round(s.TransferCap!.Value / Math.Max(1, multiplier), MidpointRounding.AwayFromZero),
                    multiplier);
            })
            .ToList();
    }

    /// <summary>
    /// Health martyr spells: the caster spends <c>drain percentage</c> of its
    /// own health and the target takes <c>damage ratio</c> times that. Ordered
    /// weakest first.
    /// </summary>
    public static IReadOnlyList<MartyrSpellOption> MartyrOptions(AceWorld world)
    {
        AceSpell[] spells = world.Spells.Values
            .Where(static s => s.DamageType == HealthDamage && s.DrainPercentage is > 0 && s.DamageRatio is > 0)
            .OrderBy(static s => s.DrainPercentage!.Value * s.DamageRatio!.Value)
            .ThenBy(static s => s.Id)
            .ToArray();
        return spells
            .Select((s, rank) => new MartyrSpellOption(
                s.Id,
                CastTime(s.Id, rank + 1),
                Math.Round(s.DrainPercentage!.Value, 6),
                Math.Round(s.DamageRatio!.Value, 6)))
            .ToList();
    }

    private static int CastTime(uint spellId, int level) =>
        KnownCastTimes.TryGetValue(spellId, out int known) ? known : EstimatedCastTime(level);
}
