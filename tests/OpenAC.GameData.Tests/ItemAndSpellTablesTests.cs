using OpenAC.GameData.Ace;
using OpenAC.GameData.GameInfo;

namespace OpenAC.GameData.Tests;

public class ItemAndSpellTablesTests
{
    private const int Healer = 28;
    private const int Missile = 4;
    private const int Gem = 38;

    [Fact]
    public void Heal_kits_carry_their_bonuses_and_vital_in_the_reference_numbers()
    {
        AceWorld world = new TestDump()
            .Weenie(1, Healer, "Treated Healing Kit").Int(1, 89, 2).Int(1, 90, 25).Float(1, 100, 2.0).SoldBy(900, 1)
            .Weenie(2, Healer, "Mana Kit").Int(2, 89, 6).Int(2, 90, 10).SoldBy(900, 2)
            .Weenie(3, Healer, "Broken Kit").SoldBy(900, 3)
            .World();

        IReadOnlyList<HealKitOption> kits = ItemTables.HealKits(world);

        Assert.Equal(
            [new HealKitOption("Mana Kit", 1.0, 10, ItemTables.Mana), new HealKitOption("Treated Healing Kit", 2.0, 25, ItemTables.Health)],
            kits);
    }

    [Fact]
    public void Grenades_are_thrown_items_that_carry_a_spell_and_its_spellcraft()
    {
        AceWorld world = new TestDump()
            .Weenie(1, Missile, "Iron Phial of Imperil")
            .Int(1, AceProperty.Int.WieldRequirements, 2).Int(1, AceProperty.Int.WieldSkillType, 38)
            .Int(1, AceProperty.Int.WieldDifficulty, 75).Int(1, 106, 100).DataId(1, 55, 1323).Float(1, 156, 1.0).SoldBy(900, 1)
            .Weenie(2, Missile, "Throwing Axe").SoldBy(900, 2)
            .Weenie(3, Missile, "Enchanted Dart").DataId(3, 55, 99).SoldBy(900, 3)
            .Weenie(4, Missile, "Channeling Hatchet").DataId(4, 55, 98).Int(4, 106, 800).Float(4, 156, 0.1).SoldBy(900, 4)
            .World();

        GrenadeOption grenade = Assert.Single(ItemTables.Grenades(world));

        Assert.Equal(new GrenadeOption("Iron Phial of Imperil", 2, 38, 75, 1323, 100), grenade);
    }

    [Fact]
    public void A_shared_cooldown_is_written_as_its_signed_sixteen_bit_spell_id()
    {
        AceWorld world = new TestDump()
            .Weenie(1, Gem, "Asheron's Benediction").Int(1, 280, 2).GivenBy(1, 1)
            .Weenie(2, Gem, "Plain Gem").GivenBy(1, 2)
            .World();

        Assert.Equal([("Asheron's Benediction", -32766)], ItemTables.Cooldowns(world));
    }

    [Fact]
    public void Drains_give_the_caster_one_minus_the_loss_and_take_at_most_the_cap_over_the_multiplier()
    {
        AceWorld world = new TestDump()
            .DrainSpell(1237, "Drain Health Other I", 0.25, -1, 60)
            .DrainSpell(1242, "Drain Health Other VI", 0.4, 0.25, 150)
            .DrainSpell(9001, "Drain Health Other VII", 0.5, 0.5, 170)
            .MartyrSpell(2760, "Martyr's Hecatomb I", 0.25, 0.75)
            .MartyrSpell(9002, "Stamina Sacrifice", 0.25, 0.75, damageType: 0x100)
            .World();

        IReadOnlyList<DrainSpellOption> drains = SpellTables.DrainOptions(world);

        Assert.Equal(
            [
                new DrainSpellOption(1237, 500, 0.25, 30, 2),
                new DrainSpellOption(1242, 2300, 0.4, 150, 0.75),
                new DrainSpellOption(9001, SpellTables.EstimatedCastTime(3), 0.5, 170, 0.5),
            ],
            drains);
        Assert.Equal([new MartyrSpellOption(2760, 550, 0.25, 0.75)], SpellTables.MartyrOptions(world));
    }

    [Fact]
    public void Crafts_list_each_use_of_one_item_on_another_that_makes_a_new_item()
    {
        var dump = new TestDump()
            .Weenie(1, 1, "Wrapped Bundle of Arrowheads").Weenie(2, 1, "Wrapped Bundle of Arrowshafts")
            .Weenie(3, 5, "Arrow").Weenie(4, 1, "Salvage")
            .Recipe(100, makes: 3, source: 1, target: 2, skill: 37, difficulty: 50)
            .Recipe(101, makes: 0, source: 4, target: 3);

        CraftInteraction craft = Assert.Single(CraftTable.Interactions(dump.World()));

        Assert.Equal(("Wrapped Bundle of Arrowheads", "Wrapped Bundle of Arrowshafts", "Arrow"), (craft.UseItem, craft.OnItem, craft.Result));
        Assert.Equal((100, "made", "failed", 37, 50), (craft.ResultCount, craft.SuccessMessage, craft.FailMessage, craft.Skill, craft.Difficulty));
    }
}
