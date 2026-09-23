using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>One <c>CraftInteractions</c> row: using the first item on the second makes the result.</summary>
internal sealed record CraftInteraction(
    string UseItem,
    string OnItem,
    string Result,
    int ResultCount,
    string SuccessMessage,
    string FailMessage,
    int Skill,
    int Difficulty,
    int Id);

/// <summary>
/// Builds <c>CraftInteractions</c> from ACE's recipes: one row per way of using
/// one item on another that makes a new item. Recipes that only change the
/// target (tinkering, dyeing) make nothing new and are left out.
/// </summary>
internal static class CraftTable
{
    public static void Build(AceWorld world, VtankDatabase database)
    {
        VtankTable table = GameInfoSchema.NewTable("CraftInteractions");
        foreach (CraftInteraction craft in Interactions(world))
        {
            GameInfoSchema.AddRow(
                table,
                VtankCell.String(craft.UseItem),
                VtankCell.String(craft.OnItem),
                VtankCell.String(craft.Result),
                VtankCell.Int(craft.ResultCount),
                VtankCell.String(craft.SuccessMessage),
                VtankCell.String(craft.FailMessage),
                VtankCell.Int(craft.Skill),
                VtankCell.Int(craft.Difficulty),
                VtankCell.Int(craft.Id));
        }
        database.Tables.Add(("CraftInteractions", table));
    }

    /// <summary>
    /// Every cook-book entry whose recipe makes a named item, by entry id (the
    /// row's <c>ID</c>). Two entries with the same three names keep the first:
    /// a reader looks recipes up by name only.
    /// </summary>
    public static IReadOnlyList<CraftInteraction> Interactions(AceWorld world)
    {
        var seen = new HashSet<(string, string, string)>();
        var result = new List<CraftInteraction>();
        foreach (AceCookBookEntry entry in world.Sources.CookBook.OrderBy(static e => e.Id))
        {
            if (!world.Sources.Recipes.TryGetValue(entry.RecipeId, out AceRecipe? recipe)
                || Name(world, recipe.SuccessClassId) is not { } made
                || Name(world, entry.SourceClassId) is not { } source
                || Name(world, entry.TargetClassId) is not { } target
                || !seen.Add((source.ToUpperInvariant(), target.ToUpperInvariant(), made.ToUpperInvariant())))
            {
                continue;
            }
            result.Add(new CraftInteraction(
                source,
                target,
                made,
                recipe.SuccessAmount,
                recipe.SuccessMessage,
                recipe.FailMessage,
                recipe.Skill,
                recipe.Difficulty,
                checked((int)entry.Id)));
        }
        return result;
    }

    private static string? Name(AceWorld world, uint classId) =>
        classId != 0 && world.Weenies.TryGetValue(classId, out AceWeenie? weenie) && !string.IsNullOrWhiteSpace(weenie.Name)
            ? weenie.Name.Trim()
            : null;
}
