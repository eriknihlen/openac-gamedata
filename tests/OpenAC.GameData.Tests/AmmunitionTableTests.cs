using OpenAC.GameData.Ace;
using OpenAC.GameData.GameInfo;

namespace OpenAC.GameData.Tests;

public class AmmunitionTableTests
{
    private const int Ammo = AceProperty.WeenieType.Ammunition;

    private static TestDump Arrow(TestDump dump, uint id, string name, long damageType, long damage, double variance, int ammoType = 1) =>
        dump.Weenie(id, Ammo, name)
            .Int(id, AceProperty.Int.AmmoType, ammoType)
            .Int(id, AceProperty.Int.DamageType, damageType)
            .Int(id, AceProperty.Int.Damage, damage)
            .Float(id, AceProperty.Float.DamageVariance, variance);

    [Fact]
    public void Reads_launcher_element_quality_and_both_wield_requirements()
    {
        var dump = new TestDump();
        Arrow(dump, 1, "Deadly Prismatic Quarrel", 0x10000000, 40, 0.3, ammoType: 2)
            .Int(1, AceProperty.Int.WieldRequirements, 8).Int(1, AceProperty.Int.WieldSkillType, 37).Int(1, AceProperty.Int.WieldDifficulty, 3)
            .Int(1, AceProperty.Int.WieldRequirements2, 2).Int(1, AceProperty.Int.WieldSkillType2, 37).Int(1, AceProperty.Int.WieldDifficulty2, 375)
            .Int(1, AceProperty.Int.WieldRequirements3, 2).Int(1, AceProperty.Int.WieldSkillType3, 47).Int(1, AceProperty.Int.WieldDifficulty3, 300)
            .SoldBy(900, 1);
        Arrow(dump, 2, "Fire Dart", 0x10, 20, 0.25, ammoType: 4).SoldBy(900, 2);

        IReadOnlyList<AmmunitionOption> options = AmmunitionTable.Options(dump.World());

        AmmunitionOption quarrel = options.Single(o => o.Name == "Deadly Prismatic Quarrel");
        Assert.Equal(AmmunitionTable.Crossbow, quarrel.LauncherType);
        Assert.Equal(DamageElement.PrismaticAmmunition, quarrel.Element);
        Assert.Equal(300, quarrel.WieldRequirement);
        Assert.Equal((37, 375), (quarrel.SecondSkill, quarrel.SecondRequirement));
        Assert.Equal(340, quarrel.Quality);

        AmmunitionOption dart = options.Single(o => o.Name == "Fire Dart");
        Assert.Equal((AmmunitionTable.Atlatl, DamageElement.Fire, 0, 175), (dart.LauncherType, dart.Element, dart.WieldRequirement, dart.Quality));
    }

    [Fact]
    public void Quality_keeps_kinds_apart_that_whole_points_would_tie()
    {
        // 21 damage: variance 0.33 averages 17.5, variance 0.25 averages 18.4.
        var dump = new TestDump();
        Arrow(dump, 1, "Barbed", 0x2, 21, 0.33).SoldBy(900, 1);
        Arrow(dump, 2, "Piercing", 0x2, 21, 0.25).SoldBy(900, 2);

        IReadOnlyList<AmmunitionOption> options = AmmunitionTable.Options(dump.World());

        Assert.True(options.Single(o => o.Name == "Piercing").Quality > options.Single(o => o.Name == "Barbed").Quality);
    }

    [Fact]
    public void Special_groups_follow_how_the_ammunition_is_had()
    {
        var dump = new TestDump()
            .Weenie(50, 1, "Shafts").Weenie(51, 1, "Raid Heads").Weenie(52, 1, "Quest Heads")
            .SoldBy(900, 50).SoldBy(901, 51, currency: 77).GivenBy(1, 52);
        Arrow(dump, 1, "Plain", 0x2, 10, 0.25).SoldBy(900, 1);
        Arrow(dump, 2, "Raid", 0x40, 40, 0.3).Recipe(100, makes: 2, source: 51, target: 50);
        Arrow(dump, 3, "Quest", 0x8, 40, 0.3).Recipe(101, makes: 3, source: 52, target: 50);
        Arrow(dump, 4, "Lost", 0x20, 40, 0.3);

        Dictionary<string, int> special = AmmunitionTable.Options(dump.World()).ToDictionary(o => o.Name, o => o.Special);

        Assert.Equal(0, special["Plain"]);
        Assert.Equal(AmmunitionTable.SpecialBoughtWithCurrency, special["Raid"]);
        Assert.Equal(AmmunitionTable.SpecialOther, special["Quest"]);
        Assert.Equal(AmmunitionTable.SpecialOther, special["Lost"]);
    }

    [Fact]
    public void A_creatures_own_ammunition_is_left_out_and_never_stands_for_a_players()
    {
        var dump = new TestDump().Creature(10, "Archer");
        Arrow(dump, 1, "Arrow", 0x2, 9, 0.25).SoldBy(900, 1);
        Arrow(dump, 2, "Arrow", 0x2, 300, 0.3).CreateList(10, 2, 2);
        Arrow(dump, 3, "Monster Arrow", 0x10, 400, 0.3).CreateList(10, 2, 3);

        IReadOnlyList<AmmunitionOption> options = AmmunitionTable.Options(dump.World());

        AmmunitionOption arrow = Assert.Single(options);
        Assert.Equal(1u, arrow.Weenie.ClassId);
    }
}
