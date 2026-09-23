using System.Text;

namespace OpenAC.GameData.Compare;

/// <summary>How one recipe of the other file was found among ours.</summary>
internal enum RecipeMatch
{
    /// <summary>Same three names, same order.</summary>
    Exact,

    /// <summary>Same names, the two items the other way round.</summary>
    Swapped,

    /// <summary>A name differs only in spelling or wording (see <see cref="RecipeMatcher.SameItem"/>).</summary>
    Variant,

    /// <summary>No recipe of ours makes it.</summary>
    Missing,
}

/// <summary>
/// Finds a recipe of ours for a recipe named in another game database, when
/// the two name the same items differently: the two items in the other order,
/// a plural, <c>&amp;</c> for <c>and</c>, small words left out, a one- or
/// two-letter spelling difference, or a more specific item (a coloured rat
/// tail for "Rat Tail").
/// </summary>
internal static class RecipeMatcher
{
    private static readonly HashSet<string> SmallWords = ["of", "and", "the", "a", "an"];

    public readonly record struct Recipe(string Use, string On, string Result);

    /// <summary>Our recipe that matches, and how; <see cref="RecipeMatch.Missing"/> when none does.</summary>
    public static (RecipeMatch Kind, Recipe? Ours) Find(Recipe theirs, IReadOnlyList<Recipe> ours)
    {
        foreach (Recipe mine in ours)
        {
            if (Same(mine.Result, theirs.Result) && Same(mine.Use, theirs.Use) && Same(mine.On, theirs.On))
                return (RecipeMatch.Exact, mine);
        }
        foreach (Recipe mine in ours)
        {
            if (Same(mine.Result, theirs.Result) && Same(mine.Use, theirs.On) && Same(mine.On, theirs.Use))
                return (RecipeMatch.Swapped, mine);
        }
        foreach (Recipe mine in ours)
        {
            if (!SameItem(mine.Result, theirs.Result))
                continue;
            if ((SameItem(mine.Use, theirs.Use) && SameItem(mine.On, theirs.On))
                || (SameItem(mine.Use, theirs.On) && SameItem(mine.On, theirs.Use)))
            {
                return (RecipeMatch.Variant, mine);
            }
        }
        return (RecipeMatch.Missing, null);
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Do two names mean the same item? Equal after normalising (case, plurals,
    /// <c>&amp;</c>, small words, spaces); or within two letters of each other;
    /// or every word of theirs is in ours (ours names a more specific item, as
    /// "Black Rat Tail" for "Rat Tail"; never the other way round).
    /// </summary>
    public static bool SameItem(string ours, string theirs)
    {
        string[] a = Words(ours), b = Words(theirs);
        if (a.Length == 0 || b.Length == 0)
            return false;
        string joinedA = string.Concat(a), joinedB = string.Concat(b);
        if (joinedA == joinedB || Distance(joinedA, joinedB) <= 2)
            return true;
        return b.All(a.Contains);
    }

    /// <summary>Lower-case words without small words, each made singular.</summary>
    public static string[] Words(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name.Replace("&", " and ", StringComparison.Ordinal).ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static w => !SmallWords.Contains(w))
            .Select(Singular)
            .ToArray();
    }

    private static string Singular(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;

    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 2)
            return int.MaxValue;
        int[] previous = Enumerable.Range(0, b.Length + 1).ToArray();
        int[] current = new int[b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
