using OpenAC.GameData.Ace;
using OpenAC.GameData.GameInfo;
using OpenAC.GameData.Tests.MossTankReader;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.Tests;

/// <summary>
/// The generated file read back by MossTank's own reader: what the plugin
/// will see.
/// </summary>
public class GameInfoOutputTests
{
    private static readonly DateTimeOffset Published = new(2026, 9, 17, 5, 54, 52, TimeSpan.Zero);

    private static AceWorld World()
    {
        var dump = new TestDump()
            .Creature(1, "Olthoi Slasher")
            .Int(1, AceProperty.Int.CreatureType, 1)
            .Vital(1, AceProperty.Vital.MaxHealth, 3000).Attribute(1, AceProperty.Attribute.Endurance, 380)
            .Bool(1, AceProperty.Bool.NonProjectileMagicImmune, true)
            .BodyPart(1, 0, 350, 1.0)
            .Float(1, AceProperty.Float.ArmorModVsBludgeon, 0.6)
            .Float(1, AceProperty.Float.ResistElectric, 0.25)
            .Creature(2, "Olthoi Grub")
            .Int(2, AceProperty.Int.CreatureType, 1)
            .Vital(2, AceProperty.Vital.MaxHealth, 100)
            .BodyPart(2, 0, 350, 1.0)
            .Float(2, AceProperty.Float.ArmorModVsBludgeon, 0.6)
            .Creature(3, "Olthoi Oddity")
            .Int(3, AceProperty.Int.CreatureType, 1)
            .BodyPart(3, 0, 350, 1.0)
            .Float(3, AceProperty.Float.ResistBludgeon, 0.1)
            .Weenie(10, AceProperty.WeenieType.Ammunition, "Fire Arrow")
            .Int(10, AceProperty.Int.AmmoType, 1).Int(10, AceProperty.Int.DamageType, 0x10)
            .Int(10, AceProperty.Int.Damage, 12).Float(10, AceProperty.Float.DamageVariance, 0.25)
            .SoldBy(900, 10)
            .Weenie(20, 28, "Healing Kit").Int(20, 89, 2).Int(20, 90, 10).SoldBy(900, 20)
            .Weenie(21, 4, "Iron Phial of Fester")
            .Int(21, AceProperty.Int.WieldRequirements, 2).Int(21, AceProperty.Int.WieldSkillType, 38)
            .Int(21, AceProperty.Int.WieldDifficulty, 75).Int(21, 106, 100).DataId(21, 55, 172).Float(21, 156, 1.0).SoldBy(900, 21)
            .Weenie(22, 38, "Gold Medal of Vigor").Int(22, 280, 7).GivenBy(1, 22)
            .DrainSpell(1237, "Drain Health Other I", 0.25, -1, 60)
            .MartyrSpell(3818, "Curse of Raven Fury", 0.5, 2)
            .Weenie(30, 1, "Wrapped Bundle of Fire Arrowheads").Weenie(31, 1, "Wrapped Bundle of Arrowshafts")
            .Recipe(500, makes: 10, source: 30, target: 31, skill: 37, difficulty: 90);
        return dump.World();
    }

    private static VtankGameInfoDatabase Generated() =>
        VtankGameInfoDatabase.Parse(GameInfoBuilder.Build(World(), Published).Render());

    [Fact]
    public void The_reader_takes_the_header_the_reference_client_requires()
    {
        VtankGameInfoDatabase db = Generated();

        Assert.True(db.IsLoaded);
        Assert.Equal(GameInfoSchema.DatabaseVersion, db.Version);
        Assert.Equal((int)Published.ToUnixTimeSeconds(), db.LastUpdateTime);
    }

    [Fact]
    public void The_reader_sees_species_health_immunity_and_damage_lists()
    {
        VtankGameInfoDatabase db = Generated();

        Assert.Equal(1, db.SpeciesOf("Olthoi Slasher"));
        Assert.Equal(3190, db.MaximumHealthOf("Olthoi Slasher"));
        Assert.True(db.IsImmuneToMagic("Olthoi Slasher"));
        Assert.False(db.IsImmuneToMagic("Olthoi Grub"));
        // The species list puts bludgeon first (the armor is weakest there);
        // the oddity resists bludgeon, so it gets its own list.
        Assert.Equal(MonsterDamageType.Bludgeon, db.DamagePreferences("Olthoi Grub")[0]);
        Assert.Equal(MonsterDamageType.Electric, db.DamagePreferences("Olthoi Slasher")[^1]);
        Assert.NotEqual(MonsterDamageType.Bludgeon, db.DamagePreferences("Olthoi Oddity")[0]);
        Assert.Equal(7, db.DamagePreferences("Olthoi Oddity").Count);
    }

    [Fact]
    public void The_reader_sees_the_ammunition_rows()
    {
        VtankAmmunitionOption arrow = Assert.Single(Generated().AmmunitionOptions);

        Assert.Equal(new VtankAmmunitionOption("Fire Arrow", 5, 0, 6, 105, 0, 0u, 0), arrow);
    }

    [Fact]
    public void The_reader_sees_kits_grenades_drains_martyrs_and_recipes()
    {
        VtankGameInfoDatabase db = Generated();

        Assert.Equal(new VtankHealKit("Healing Kit", 1.0, 10, 1), Assert.Single(db.HealKits));
        Assert.Equal(new VtankGrenadeOption("Iron Phial of Fester", 2, 38, 75, 172u, 100), Assert.Single(db.GrenadeOptions));
        Assert.Equal(new VtankDrainSpellOption(1237u, 500, 0.25, 30, 2), Assert.Single(db.DrainSpellOptions));
        Assert.Equal(new VtankMartyrSpellOption(3818u, 2850, 0.5, 2), Assert.Single(db.MartyrSpellOptions));
        VtankCraftRecipe recipe = Assert.Single(db.Crafts.ForResult("Fire Arrow"));
        Assert.Equal(
            ("Wrapped Bundle of Fire Arrowheads", "Wrapped Bundle of Arrowshafts", 37u, 90),
            (recipe.FirstItem, recipe.SecondItem, recipe.RequiredSkill, recipe.Difficulty));
    }

    [Fact]
    public void Every_table_is_written_with_its_columns_even_when_empty()
    {
        VtankDatabase db = VtankDatabase.Parse(GameInfoBuilder.Build(World(), Published).Render());

        Assert.Equal(GameInfoSchema.TableNames.Order(StringComparer.Ordinal), db.Tables.Select(t => t.Name));
        foreach ((string name, VtankTable table) in db.Tables)
        {
            VtankTable expected = GameInfoSchema.NewTable(name);
            Assert.Equal(expected.ColumnNames, table.ColumnNames);
            Assert.Equal(expected.IndexFlags, table.IndexFlags);
            Assert.All(table.Rows, row => Assert.Equal(expected.ColumnNames.Count, row.Cells.Count));
        }
    }

    [Fact]
    public void The_same_input_builds_the_same_file()
    {
        Assert.Equal(
            GameInfoBuilder.Build(World(), Published).Render(),
            GameInfoBuilder.Build(World(), Published).Render());
    }
}
