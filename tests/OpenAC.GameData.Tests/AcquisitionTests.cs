using OpenAC.GameData.Ace;

namespace OpenAC.GameData.Tests;

public class AcquisitionTests
{
    private const int Generic = 1;

    [Fact]
    public void A_recipe_loop_still_settles_to_its_cheapest_way()
    {
        // Loose heads come from a recipe that unwraps wrapped heads; wrapped
        // heads come from a recipe that wraps loose ones; only the loose heads'
        // other recipe (from a sold kit) reaches the outside world.
        AceWorld world = new TestDump()
            .Weenie(1, Generic, "Loose Heads").Weenie(2, Generic, "Wrapped Heads")
            .Weenie(3, Generic, "Kit").Weenie(4, Generic, "Shafts").Weenie(5, Generic, "Arrow")
            .Recipe(100, makes: 2, source: 1, target: 1)
            .Recipe(101, makes: 1, source: 2, target: 2)
            .Recipe(102, makes: 1, source: 3, target: 3)
            .Recipe(103, makes: 5, source: 2, target: 4)
            .SoldBy(900, 3).SoldBy(900, 4)
            .World();

        Assert.Equal(AceAcquisition.Ordinary, world.Sources.Acquisition(1));
        Assert.Equal(AceAcquisition.Ordinary, world.Sources.Acquisition(2));
        Assert.Equal(AceAcquisition.Ordinary, world.Sources.Acquisition(5));
    }

    [Fact]
    public void A_recipe_is_as_dear_as_its_dearest_ingredient()
    {
        AceWorld world = new TestDump()
            .Weenie(1, Generic, "Token Heads").Weenie(2, Generic, "Gift Heads").Weenie(3, Generic, "Shafts")
            .Weenie(4, Generic, "Token Arrow").Weenie(5, Generic, "Gift Arrow")
            .SoldBy(900, 3).SoldBy(901, 1, currency: 77).GivenBy(1, 2)
            .Recipe(100, makes: 4, source: 1, target: 3)
            .Recipe(101, makes: 5, source: 2, target: 3)
            .World();

        Assert.Equal(AceAcquisition.Currency, world.Sources.Acquisition(4));
        Assert.Equal(AceAcquisition.Given, world.Sources.Acquisition(5));
    }

    [Fact]
    public void Loot_counts_as_ordinary_and_a_creatures_own_gear_does_not()
    {
        AceWorld world = new TestDump()
            .Creature(10, "Archer").Weenie(1, Generic, "Loot").Weenie(2, Generic, "Bow")
            .CreateList(10, 8, 1)
            .CreateList(10, 2, 2)
            .World();

        Assert.Equal(AceAcquisition.Ordinary, world.Sources.Acquisition(1));
        Assert.Equal(AceAcquisition.None, world.Sources.Acquisition(2));
        Assert.True(world.Sources.IsWielded(2));
    }
}
