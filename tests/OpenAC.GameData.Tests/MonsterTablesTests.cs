using OpenAC.GameData.Ace;
using OpenAC.GameData.GameInfo;

namespace OpenAC.GameData.Tests;

public class MonsterTablesTests
{
    private const int Endurance = AceProperty.Attribute.Endurance;
    private const int MaxHealth = AceProperty.Vital.MaxHealth;

    [Fact]
    public void Maximum_health_is_the_vital_plus_half_endurance_rounded_away_from_zero()
    {
        AceWorld world = new TestDump()
            .Creature(1, "Adept").Vital(1, MaxHealth, 400, 21).Attribute(1, Endurance, 180, 5)
            .World();

        Assert.Equal(514, MonsterTables.MaximumHealth(world.Weenies[1]));
    }

    [Fact]
    public void Damage_taken_is_the_armor_share_times_the_resistance()
    {
        // Armor 200/3 lets half a hit through; the cold armor modifier doubles
        // the armor (a third through); fire resistance halves what gets through.
        AceWorld world = new TestDump()
            .Creature(1, "Beast").BodyPart(1, 0, 200, 1.0).BodyPart(1, 1, 0, 3.0)
            .Float(1, AceProperty.Float.ArmorModVsCold, 2.0)
            .Float(1, AceProperty.Float.ResistFire, 0.5)
            .World();
        // The armored part is hit a quarter of the time; the bare part lets all through.
        Dictionary<DamageElement, double> damage = MonsterTables.DamageTaken(world.Weenies[1]);

        double armored = MonsterTables.ArmorShare(200);
        Assert.Equal(66.666 / 266.666, armored, 3);
        Assert.Equal((armored + 3) / 4, damage[DamageElement.Slash], 9);
        Assert.Equal((MonsterTables.ArmorShare(400) + 3) / 4, damage[DamageElement.Cold], 9);
        Assert.Equal((armored + 3) / 4 * 0.5, damage[DamageElement.Fire], 9);
    }

    [Fact]
    public void Equal_elements_keep_the_tie_order_slash_first()
    {
        Dictionary<DamageElement, double> flat = DamageElements.All.ToDictionary(e => e, _ => 1.0);
        flat[DamageElement.Fire] = 1.5;

        Assert.Equal("6;2;0;1;3;4;5", DamageElements.Format(MonsterTables.Order(flat)));
    }

    [Fact]
    public void A_monster_gets_its_own_list_only_when_the_species_list_costs_more_than_a_tenth()
    {
        DamageElement[] species = [DamageElement.Slash, DamageElement.Fire];
        var close = new Dictionary<DamageElement, double> { [DamageElement.Slash] = 0.95, [DamageElement.Fire] = 1.0 };
        var far = new Dictionary<DamageElement, double> { [DamageElement.Slash] = 0.85, [DamageElement.Fire] = 1.0 };

        Assert.False(MonsterTables.NeedsOverride(close, species));
        Assert.True(MonsterTables.NeedsOverride(far, species));
    }

    [Fact]
    public void Monsters_are_attackable_creatures_one_per_name_the_most_placed_standing_for_it()
    {
        AceWorld world = new TestDump()
            .Creature(1, "Drudge").Int(1, AceProperty.Int.CreatureType, 3).Vital(1, MaxHealth, 10)
            .Creature(2, "Drudge").Int(2, AceProperty.Int.CreatureType, 3).Vital(2, MaxHealth, 20).Placed(2).Generated(99, 2)
            .Creature(3, "Drudge").Int(3, AceProperty.Int.CreatureType, 3).Vital(3, MaxHealth, 30).Placed(3)
            .Creature(4, "Town Crier").Bool(4, AceProperty.Bool.Attackable, false)
            .Creature(5, "Odd Thing")
            .World();

        IReadOnlyList<MonsterEntry> monsters = MonsterTables.Monsters(world);

        Assert.Equal(["Drudge", "Odd Thing"], monsters.Select(m => m.Name));
        Assert.Equal(20, monsters[0].MaximumHealth);
        Assert.Equal(3, monsters[0].Species);
        Assert.Equal(MonsterTables.NoSpecies, monsters[1].Species);
    }
}
