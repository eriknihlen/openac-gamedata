using System.Globalization;
using System.Text;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.Compare;

/// <summary>
/// Compares a generated database with another VTank game database (for
/// example the one VTank's online service hands out) table by table, and
/// writes what agrees and what differs. Nothing from the other file is copied
/// anywhere: the report only counts and names differences.
/// </summary>
internal static class GameInfoComparison
{
    private const int ExampleLimit = 25;

    /// <param name="ours">The generated database.</param>
    /// <param name="theirs">The database to compare with.</param>
    /// <param name="worldItemNames">
    /// Optional: every item name in the world data. With it, a recipe of the
    /// other file that none of ours matches is checked against the items the
    /// world has at all.
    /// </param>
    /// <param name="damageTaken">
    /// Optional: for a monster name, the share of a hit of each element (VTank
    /// element number) that reaches it, by the world data. With it, every pick
    /// that differs is weighed: how much damage the other file's pick loses.
    /// </param>
    public static string Report(
        VtankDatabase ours,
        VtankDatabase theirs,
        Func<string, IReadOnlyDictionary<int, double>?>? damageTaken = null,
        IReadOnlyCollection<string>? worldItemNames = null,
        IReadOnlyCollection<string>? worldRecipeResults = null)
    {
        var sb = new StringBuilder();
        TableSizes(sb, ours, theirs);
        SpeciesMembers(sb, ours, theirs);
        MonsterDamage(sb, ours, theirs, damageTaken);
        Immunities(sb, ours, theirs);
        Ammunition(sb, ours, theirs);
        KeyedRows(sb, ours, theirs, "HealKits", [0]);
        KeyedRows(sb, ours, theirs, "GrenadeOptions", [0]);
        KeyedRows(sb, ours, theirs, "DrainSpellOptions", [0]);
        KeyedRows(sb, ours, theirs, "MartyrSpellOptions", [0]);
        KeyedRows(sb, ours, theirs, "CooldownIDs", [0]);
        Crafts(sb, ours, theirs, worldItemNames, worldRecipeResults);
        return sb.ToString();
    }

    private static void TableSizes(StringBuilder sb, VtankDatabase ours, VtankDatabase theirs)
    {
        sb.AppendLine("## Table sizes (ours / theirs)");
        foreach (string name in ours.Tables.Select(static t => t.Name).Union(theirs.Tables.Select(static t => t.Name)).Order())
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {name}: {ours.Find(name)?.Rows.Count.ToString(CultureInfo.InvariantCulture) ?? "-"} / {theirs.Find(name)?.Rows.Count.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine();
    }

    private static Dictionary<string, (int Species, int Health)> Members(VtankDatabase db)
    {
        var result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (VtankRow row in db.Find("SpeciesMembers")?.Rows ?? [])
            result[row.Cells[0].AsString()] = (row.Cells[1].AsInt(), row.Cells[2].AsInt());
        return result;
    }

    private static void SpeciesMembers(StringBuilder sb, VtankDatabase ours, VtankDatabase theirs)
    {
        Dictionary<string, (int Species, int Health)> a = Members(ours), b = Members(theirs);
        string[] both = a.Keys.Where(b.ContainsKey).Order(StringComparer.Ordinal).ToArray();
        string[] onlyTheirs = b.Keys.Where(k => !a.ContainsKey(k)).Order(StringComparer.Ordinal).ToArray();
        string[] speciesDiffer = both.Where(k => a[k].Species != b[k].Species).ToArray();
        string[] healthDiffer = both.Where(k => a[k].Health != b[k].Health).ToArray();
        sb.AppendLine("## SpeciesMembers");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- names: ours {a.Count}, theirs {b.Count}, both {both.Length}, only theirs {onlyTheirs.Length}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- species agree for {both.Length - speciesDiffer.Length} of {both.Length}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- maximum health agrees exactly for {both.Length - healthDiffer.Length} of {both.Length}");
        Examples(sb, "only theirs", onlyTheirs);
        Examples(sb, "species differ (ours/theirs)", speciesDiffer.Select(k => $"{k} {a[k].Species}/{b[k].Species}"));
        Examples(sb, "health differs (ours/theirs)", healthDiffer.Select(k => $"{k} {a[k].Health}/{b[k].Health}"));
        sb.AppendLine();
    }

    /// <summary>A monster's element list as VTank resolves it: its override, else its species'.</summary>
    private static Dictionary<string, int[]> ResolvedDamage(VtankDatabase db)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (VtankRow row in db.Find("MonsterDamageOverrides")?.Rows ?? [])
            overrides[row.Cells[0].AsString()] = row.Cells[1].AsString();
        var species = new Dictionary<int, string>();
        foreach (VtankRow row in db.Find("SpeciesDamages")?.Rows ?? [])
            species[row.Cells[0].AsInt()] = row.Cells[1].AsString();
        var result = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, (int Species, int Health) member) in Members(db))
        {
            string? text = overrides.TryGetValue(name, out string? own) ? own
                : species.TryGetValue(member.Species, out string? shared) ? shared
                : null;
            if (text is not null)
                result[name] = ParseElements(text);
        }
        return result;
    }

    private static int[] ParseElements(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => int.TryParse(s, CultureInfo.InvariantCulture, out int v) ? v : -1)
            .Where(static v => v is >= 0 and <= 6)
            .Distinct()
            .ToArray();

    private static readonly int[] Physical = [0, 1, 2];
    private static readonly int[] Elemental = [3, 4, 5, 6];

    private static int? Pick(int[] order, IReadOnlyCollection<int> available)
    {
        foreach (int element in order)
        {
            if (available.Contains(element))
                return element;
        }
        return null;
    }

    private static void MonsterDamage(
        StringBuilder sb,
        VtankDatabase ours,
        VtankDatabase theirs,
        Func<string, IReadOnlyDictionary<int, double>?>? damageTaken)
    {
        Dictionary<string, int[]> a = ResolvedDamage(ours), b = ResolvedDamage(theirs);
        string[] both = a.Keys.Where(b.ContainsKey).Order(StringComparer.Ordinal).ToArray();
        sb.AppendLine("## Monster damage (override, else species), for monsters in both files");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- monsters compared: {both.Length}");
        foreach ((string label, int[] available) in new[]
                 {
                     ("all seven elements", new[] { 0, 1, 2, 3, 4, 5, 6 }),
                     ("physical only (slash/pierce/bludgeon)", Physical),
                     ("elemental only (acid/lightning/cold/fire)", Elemental),
                 })
        {
            // Only monsters where their list names an element of the set.
            string[] decided = both.Where(k => Pick(b[k], available) is not null).ToArray();
            string[] differ = decided.Where(k => Pick(a[k], available) != Pick(b[k], available)).ToArray();
            sb.AppendLine(CultureInfo.InvariantCulture, $"- pick with {label}: agree {decided.Length - differ.Length} of {decided.Length}");
            Examples(sb, $"differ, {label} (ours vs theirs)", differ.Select(k => $"{k}: {string.Join(';', a[k])} vs {string.Join(';', b[k])}"));
            if (damageTaken is not null)
                Weigh(sb, differ, k => (Pick(a[k], available)!.Value, Pick(b[k], available)!.Value), damageTaken);
        }
        sb.AppendLine();
    }

    /// <summary>
    /// For picks that differ, how much less damage the other file's pick does
    /// than ours by the world data, bucketed; and how often it does more.
    /// </summary>
    private static void Weigh(
        StringBuilder sb,
        IEnumerable<string> monsters,
        Func<string, (int Ours, int Theirs)> picks,
        Func<string, IReadOnlyDictionary<int, double>?> damageTaken)
    {
        int[] buckets = new int[4];
        var theirsBetter = new List<string>();
        var bigLosses = new List<(string Text, double Loss)>();
        foreach (string monster in monsters)
        {
            if (damageTaken(monster) is not { } damage)
                continue;
            (int mine, int other) = picks(monster);
            double loss = 1 - damage[other] / damage[mine];
            if (loss < -0.005)
                theirsBetter.Add(string.Create(CultureInfo.InvariantCulture, $"{monster} ({-loss:P0} more with {other} than {mine})"));
            else if (loss < 0.05)
                buckets[0]++;
            else if (loss < 0.15)
                buckets[1]++;
            else if (loss < 0.30)
                buckets[2]++;
            else
                buckets[3]++;
            if (loss >= 0.30)
                bigLosses.Add((string.Create(CultureInfo.InvariantCulture, $"{monster} ({other} does {loss:P0} less than {mine})"), loss));
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"    - by ACE's numbers their pick does less damage than ours: under 5% less {buckets[0]}, 5-15% {buckets[1]}, 15-30% {buckets[2]}, 30%+ {buckets[3]}; more than ours {theirsBetter.Count}");
        Examples(sb, "their pick better by ACE's numbers", theirsBetter);
        Examples(sb, "their pick 30%+ worse by ACE's numbers", bigLosses.OrderByDescending(static l => l.Loss).Select(static l => l.Text));
    }

    private static void Immunities(StringBuilder sb, VtankDatabase ours, VtankDatabase theirs)
    {
        static Dictionary<string, int> Read(VtankDatabase db) =>
            (db.Find("MonsterImmunities")?.Rows ?? [])
                .GroupBy(static r => r.Cells[0].AsString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static g => g.Key, static g => g.Last().Cells[1].AsInt(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> a = Read(ours), b = Read(theirs);
        sb.AppendLine("## MonsterImmunities");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- ours {a.Count}, theirs {b.Count}, same name and mask {b.Count(p => a.TryGetValue(p.Key, out int m) && m == p.Value)}");
        Examples(sb, "theirs missing from ours", b.Keys.Where(k => !a.ContainsKey(k)));
        sb.AppendLine();
    }

    private sealed record Ammo(string Name, int Launcher, int Wield, int Element, int Quality, int Special, int Skill2, int Value2);

    private static List<Ammo> ReadAmmo(VtankDatabase db) =>
        (db.Find("AmmunitionOptions")?.Rows ?? [])
            .Where(static r => r.Cells.Count >= 8)
            .Select(static r => new Ammo(
                r.Cells[0].AsString(), r.Cells[1].AsInt(), r.Cells[2].AsInt(), r.Cells[3].AsInt(),
                r.Cells[4].AsInt(), r.Cells[5].AsInt(), r.Cells[6].AsInt(), r.Cells[7].AsInt()))
            .ToList();

    /// <summary>VTank's choice with every listed ammunition in the pack.</summary>
    private static string? SelectAmmo(IEnumerable<Ammo> options, int launcher, int element, int missile, int secondSkillValue, bool allowPrismatic, int enabledSpecial)
    {
        Ammo? best = null;
        int bestQuality = int.MinValue;
        foreach (Ammo option in options)
        {
            if (option.Launcher != launcher)
                continue;
            int quality = option.Quality;
            if (option.Element != element)
            {
                if (option.Element != 100)
                    continue;
                if (!allowPrismatic)
                    quality -= 1000;
            }
            if (quality < bestQuality)
                continue;
            if (option.Wield > 0 && missile < option.Wield)
                continue;
            if (option.Skill2 != 0 && option.Value2 != 0 && secondSkillValue < option.Value2)
                continue;
            if (option.Special != 0 && (option.Special & enabledSpecial) == 0)
                continue;
            bestQuality = quality;
            best = option;
        }
        return best?.Name;
    }

    private static void Ammunition(StringBuilder sb, VtankDatabase ours, VtankDatabase theirs)
    {
        List<Ammo> a = ReadAmmo(ours), b = ReadAmmo(theirs);
        var byName = a.GroupBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);
        sb.AppendLine("## AmmunitionOptions");
        string[] missing = b.Where(x => !byName.ContainsKey(x.Name)).Select(static x => x.Name).ToArray();
        string[] extra = a.Where(x => !b.Any(y => string.Equals(y.Name, x.Name, StringComparison.OrdinalIgnoreCase))).Select(static x => x.Name).ToArray();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- rows: ours {a.Count}, theirs {b.Count}, theirs missing from ours {missing.Length}, ours not in theirs {extra.Length}");
        Examples(sb, "theirs missing from ours", missing);
        Examples(sb, "ours not in theirs", extra);
        var fieldDiffs = new List<string>();
        foreach (Ammo t in b)
        {
            if (!byName.TryGetValue(t.Name, out Ammo? o))
                continue;
            var parts = new List<string>();
            if (o.Launcher != t.Launcher) parts.Add($"launcher {o.Launcher}/{t.Launcher}");
            if (o.Wield != t.Wield) parts.Add($"wield {o.Wield}/{t.Wield}");
            if (o.Element != t.Element) parts.Add($"element {o.Element}/{t.Element}");
            if (o.Special != t.Special) parts.Add($"special {o.Special}/{t.Special}");
            if (o.Skill2 != t.Skill2 || o.Value2 != t.Value2) parts.Add($"req2 {o.Skill2}:{o.Value2}/{t.Skill2}:{t.Value2}");
            if (parts.Count > 0)
                fieldDiffs.Add($"{t.Name}: {string.Join(", ", parts)}");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"- rows in both with every fact column equal (launcher, wield, element, special, second requirement): {b.Count(t => byName.ContainsKey(t.Name)) - fieldDiffs.Count}");
        Examples(sb, "fact differences (ours/theirs)", fieldDiffs);

        // Picks: every launcher, element, missile skill and second-skill level.
        // "Shared" packs hold only names both files list, which isolates the
        // two quality rankings from the two name lists.
        var shared = new HashSet<string>(b.Select(static x => x.Name), StringComparer.OrdinalIgnoreCase);
        PickScenario(sb, "every ammunition of either file in the pack, special groups allowed", a, b, 3);
        PickScenario(sb, "shared names only, special groups allowed", a.Where(x => shared.Contains(x.Name)), b, 3);
        PickScenario(sb, "shared names only, special groups not allowed", a.Where(x => shared.Contains(x.Name)), b, 0);
        sb.AppendLine();
    }

    private static void PickScenario(StringBuilder sb, string label, IEnumerable<Ammo> ours, IEnumerable<Ammo> theirs, int enabledSpecial)
    {
        List<Ammo> a = ours.ToList(), b = theirs.ToList();
        int cases = 0;
        var pickDiffs = new List<string>();
        int[] missileLevels = [100, 230, 250, 270, 290, 300, 500];
        int[] secondLevels = [0, 250, 350, 375, 500];
        foreach (int launcher in new[] { 5, 6, 7 })
        foreach (int element in Enumerable.Range(0, 7))
        foreach (int missile in missileLevels)
        foreach (int second in secondLevels)
        foreach (bool prismatic in new[] { true, false })
        {
            string? mine = SelectAmmo(a, launcher, element, missile, second, prismatic, enabledSpecial);
            string? other = SelectAmmo(b, launcher, element, missile, second, prismatic, enabledSpecial);
            cases++;
            if (!string.Equals(mine, other, StringComparison.OrdinalIgnoreCase))
                pickDiffs.Add($"launcher {launcher} element {element} missile {missile} fletching {second} prismatic {(prismatic ? "yes" : "no")}: {mine ?? "-"} vs {other ?? "-"}");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"- picks agree for {cases - pickDiffs.Count} of {cases} cases ({label})");
        Examples(sb, "picks differ (ours vs theirs)", pickDiffs);
    }

    /// <summary>
    /// Rows matched by their key columns (compared without case): how many
    /// match, and for matched rows which other columns differ.
    /// </summary>
    private static void KeyedRows(
        StringBuilder sb,
        VtankDatabase ours,
        VtankDatabase theirs,
        string table,
        int[] key,
        int[]? ignore = null)
    {
        static Dictionary<string, VtankRow> Read(VtankDatabase db, string table, int[] key) =>
            (db.Find(table)?.Rows ?? [])
                .GroupBy(r => string.Join(" + ", key.Select(k => r.Cells[k].AsString())), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, VtankRow> a = Read(ours, table, key), b = Read(theirs, table, key);
        VtankTable? schema = ours.Find(table) ?? theirs.Find(table);
        var differences = new List<string>();
        foreach ((string name, VtankRow row) in b)
        {
            if (!a.TryGetValue(name, out VtankRow? mine))
                continue;
            var columns = new List<string>();
            for (int c = 0; c < Math.Min(row.Cells.Count, mine.Cells.Count); c++)
            {
                if (key.Contains(c) || (ignore?.Contains(c) ?? false))
                    continue;
                if (!string.Equals(mine.Cells[c].AsString(), row.Cells[c].AsString(), StringComparison.OrdinalIgnoreCase))
                    columns.Add($"{schema!.ColumnNames[c]} {mine.Cells[c].AsString()}/{row.Cells[c].AsString()}");
            }
            if (columns.Count > 0)
                differences.Add($"{name}: {string.Join(", ", columns)}");
        }
        string[] missing = b.Keys.Where(k => !a.ContainsKey(k)).ToArray();
        sb.AppendLine($"## {table}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- rows: ours {a.Count}, theirs {b.Count}; theirs missing from ours {missing.Length}; in both and equal {b.Count - missing.Length - differences.Count}");
        Examples(sb, "theirs missing from ours", missing);
        Examples(sb, "differ (ours/theirs)", differences);
        sb.AppendLine();
    }

    /// <summary>
    /// Recipes of the other file found among ours whatever the item order and
    /// however the names are worded (see <see cref="RecipeMatcher"/>); for the
    /// ones found, how the skill and difficulty compare; for the rest, whether
    /// the world data has those items at all.
    /// </summary>
    private static void Crafts(
        StringBuilder sb,
        VtankDatabase ours,
        VtankDatabase theirs,
        IReadOnlyCollection<string>? worldItemNames,
        IReadOnlyCollection<string>? worldRecipeResults)
    {
        static List<(RecipeMatcher.Recipe Recipe, int Skill, int Difficulty)> Read(VtankDatabase db) =>
            (db.Find("CraftInteractions")?.Rows ?? [])
                .Where(static r => r.Cells.Count >= 9)
                .Select(static r => (new RecipeMatcher.Recipe(r.Cells[0].AsString(), r.Cells[1].AsString(), r.Cells[2].AsString()), r.Cells[6].AsInt(), r.Cells[7].AsInt()))
                .ToList();
        var a = Read(ours);
        var b = Read(theirs);
        var mine = a.Select(static x => x.Recipe).ToList();
        var bySkill = a.GroupBy(static x => x.Recipe).ToDictionary(static g => g.Key, static g => g.First());
        var kinds = new Dictionary<RecipeMatch, int>();
        var variants = new List<string>();
        var missing = new List<RecipeMatcher.Recipe>();
        int skillEqual = 0, difficultyEqual = 0, theirsZero = 0, theirsTenthMore = 0;
        var difficultyOther = new List<string>();
        foreach ((RecipeMatcher.Recipe recipe, int skill, int difficulty) in b)
        {
            (RecipeMatch kind, RecipeMatcher.Recipe? ourRecipe) = RecipeMatcher.Find(recipe, mine);
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            if (ourRecipe is not { } match)
            {
                missing.Add(recipe);
                continue;
            }
            if (kind == RecipeMatch.Variant)
                variants.Add($"{recipe.Use} + {recipe.On} -> {recipe.Result}  ~  {match.Use} + {match.On} -> {match.Result}");
            (_, int ourSkill, int ourDifficulty) = bySkill[match];
            if (ourSkill == skill)
                skillEqual++;
            if (ourDifficulty == difficulty)
                difficultyEqual++;
            else if (difficulty == 0)
                theirsZero++;
            else if (difficulty == (int)Math.Round(ourDifficulty * 1.1, MidpointRounding.AwayFromZero))
                theirsTenthMore++;
            else
                difficultyOther.Add(string.Create(CultureInfo.InvariantCulture, $"{recipe.Result} {ourDifficulty}/{difficulty}"));
        }
        int found = b.Count - missing.Count;
        sb.AppendLine("## CraftInteractions");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- rows: ours {a.Count}, theirs {b.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- theirs found in ours: {found} of {b.Count} (same order {kinds.GetValueOrDefault(RecipeMatch.Exact)}, items the other way round {kinds.GetValueOrDefault(RecipeMatch.Swapped)}, names worded differently {kinds.GetValueOrDefault(RecipeMatch.Variant)}); not found {missing.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- of those found: same skill {skillEqual}; difficulty equal {difficultyEqual}, theirs 0 {theirsZero}, theirs ours x 1.1 {theirsTenthMore}, other {difficultyOther.Count}");
        Examples(sb, "worded differently (theirs ~ ours)", variants);
        Examples(sb, "difficulty differs otherwise (ours/theirs)", difficultyOther);
        if (worldItemNames is null)
        {
            Examples(sb, "not found", missing.Select(static r => $"{r.Use} + {r.On} -> {r.Result}"));
        }
        else
        {
            string Why(RecipeMatcher.Recipe r)
            {
                string[] unknown = new[] { r.Use, r.On, r.Result }
                    .Where(n => !worldItemNames.Any(w => RecipeMatcher.SameItem(w, n)))
                    .ToArray();
                if (unknown.Length > 0)
                    return "the world data has no item " + string.Join(", ", unknown);
                return worldRecipeResults is not null && !worldRecipeResults.Any(w => RecipeMatcher.SameItem(w, r.Result))
                    ? "no recipe in the world data makes " + r.Result
                    : "the world data makes " + r.Result + " another way";
            }
            Examples(sb, "not found, and why", missing.Select(r => $"{r.Use} + {r.On} -> {r.Result} [{Why(r)}]"));
        }
        sb.AppendLine();
    }

    private static void Examples(StringBuilder sb, string label, IEnumerable<string> items)
    {
        string[] list = items.ToArray();
        if (list.Length == 0)
            return;
        sb.AppendLine(CultureInfo.InvariantCulture, $"  - {label} ({list.Length}): {string.Join("; ", list.Take(ExampleLimit))}{(list.Length > ExampleLimit ? "; …" : string.Empty)}");
    }
}
