using OpenAC.GameData.Compare;
using Recipe = OpenAC.GameData.Compare.RecipeMatcher.Recipe;

namespace OpenAC.GameData.Tests;

public class RecipeMatcherTests
{
    private static readonly Recipe[] Ours =
    [
        new("Black Rat Tail", "Ground Meat", "Sausage"),
        new("Rice Dough", "Chicken", "Chicken Dumplings"),
        new("Skewer", "Steak", "Beef Kebab"),
        new("Raw Noodles", "Cheese", "Cragstone Farms Mac&Cheese"),
        new("Cocoa Mixture", "Honey", "Bar of Dark Chocolate"),
        new("Dough", "Pumpkin Pie Filling", "Pumpkin Pie"),
        new("Baking Pan", "Cake Batter", "Cake"),
    ];

    [Theory]
    [InlineData("Rat Tail", "Ground Meat", "Sausage", "Variant")]
    [InlineData("Rice Dough", "Chicken", "Chicken Dumpling", "Variant")]
    [InlineData("Skewer", "Steak", "Beef Kebob", "Variant")]
    [InlineData("Raw Noodles", "Cheese", "Cragstone Farms Mac and Cheese", "Variant")]
    [InlineData("Cocoa Mixture", "Honey", "Bar Dark Chocolate", "Variant")]
    [InlineData("Pumpkin Pie Filling", "Dough", "Pumpkin Pie", "Swapped")]
    [InlineData("Baking Pan", "Cake Batter", "Cake", "Exact")]
    public void Finds_the_same_recipe_worded_or_ordered_differently(string use, string on, string result, string expected)
    {
        Assert.Equal(Enum.Parse<RecipeMatch>(expected), RecipeMatcher.Find(new Recipe(use, on, result), Ours).Kind);
    }

    [Fact]
    public void A_more_specific_name_of_theirs_never_matches_a_general_one_of_ours()
    {
        Recipe theirs = new("Baking Pan", "Olthoi Chocolate Cake Batter", "Chocolate Olthoi Cake");

        Assert.Equal(RecipeMatch.Missing, RecipeMatcher.Find(theirs, Ours).Kind);
    }

    [Fact]
    public void Different_items_are_not_the_same()
    {
        Assert.False(RecipeMatcher.SameItem("Fish Kebab", "Beef Kebob"));
        Assert.False(RecipeMatcher.SameItem("Deadly Fire Arrow", "Deadly Frost Arrow"));
    }
}
