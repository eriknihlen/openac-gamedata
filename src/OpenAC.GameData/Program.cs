using System.Text;
using OpenAC.GameData.Ace;
using OpenAC.GameData.Compare;
using OpenAC.GameData.GameInfo;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData;

internal static class Program
{
    private const string CacheDirectory = ".cache";
    private const string DefaultOutput = "out/gameinfodb.ugd";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
            return Usage();
        try
        {
            return args[0] switch
            {
                "fetch" => await Fetch(Options(args)),
                "build" => await Build(Options(args)),
                "compare" when args.Length >= 3 => Compare(args[1], args[2], Options(args[2..])),
                "show" when args.Length >= 3 => Show(args[1], args[2]),
                _ => Usage(),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or HttpRequestException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("""
            usage:
              openac-gamedata fetch [--pin ace-world.json]
                  download the pinned ACE world database into .cache/ and check its checksum
              openac-gamedata build [--pin ace-world.json] [--input <dump.sql|.zip>] [--output out/gameinfodb.ugd]
                  generate the game database
              openac-gamedata compare <ours.ugd> <theirs.ugd> [--input <dump.sql|.zip>]
                  report where two game databases agree and differ; with the world
                  data, weigh each differing monster pick by ACE's damage numbers
              openac-gamedata show <dump.sql|.zip> <name or class id>
                  print the weenies with that name or class id
            """);
        return 2;
    }

    private static Dictionary<string, string> Options(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i + 1 < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new FormatException($"Unexpected argument '{args[i]}'.");
            options[args[i][2..]] = args[i + 1];
        }
        return options;
    }

    private static async Task<int> Fetch(Dictionary<string, string> options)
    {
        AceWorldPin pin = AceWorldPin.Read(options.GetValueOrDefault("pin", AceWorldPin.FileName));
        string path = await pin.FetchAsync(CacheDirectory);
        Console.WriteLine(path);
        return 0;
    }

    private static async Task<int> Build(Dictionary<string, string> options)
    {
        AceWorldPin pin = AceWorldPin.Read(options.GetValueOrDefault("pin", AceWorldPin.FileName));
        string input = options.TryGetValue("input", out string? given) ? given : await pin.FetchAsync(CacheDirectory);
        string output = options.GetValueOrDefault("output", DefaultOutput);

        AceWorld world = AceWorld.Load(input);
        VtankDatabase database = GameInfoBuilder.Build(world, pin.Published);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (directory is not null)
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(output, database.Render(), new UTF8Encoding(false));
        foreach ((string name, VtankTable table) in database.Tables.OrderBy(static t => t.Name, StringComparer.Ordinal))
            Console.WriteLine($"{name,-24}{table.Rows.Count,6}");
        Console.WriteLine($"wrote {output}");
        return 0;
    }

    private static int Compare(string ours, string theirs, Dictionary<string, string> options)
    {
        VtankDatabase a = VtankDatabase.Parse(File.ReadAllText(ours));
        VtankDatabase b = VtankDatabase.Parse(File.ReadAllText(theirs));
        Func<string, IReadOnlyDictionary<int, double>?>? damageTaken = null;
        if (options.TryGetValue("input", out string? input))
        {
            Dictionary<string, IReadOnlyDictionary<int, double>> byName = MonsterTables.Monsters(AceWorld.Load(input))
                .ToDictionary(
                    static m => m.Name,
                    static m => (IReadOnlyDictionary<int, double>)m.Damage.ToDictionary(static p => (int)p.Key, static p => p.Value),
                    StringComparer.OrdinalIgnoreCase);
            damageTaken = name => byName.GetValueOrDefault(name);
        }
        Console.Write(GameInfoComparison.Report(a, b, damageTaken));
        return 0;
    }

    /// <summary>Prints every weenie whose name or class id matches, with its properties.</summary>
    private static int Show(string dump, string query)
    {
        AceWorld world = AceWorld.Load(dump);
        foreach (AceWeenie w in world.Weenies.Values)
        {
            bool match = uint.TryParse(query, out uint id)
                ? w.ClassId == id
                : string.Equals(w.Name, query, StringComparison.OrdinalIgnoreCase);
            if (!match)
                continue;
            Console.WriteLine($"{w} type={w.Type} placements={world.PlacementsOf(w.ClassId)} sold={world.Sources.IsSold(w.ClassId)} given={world.Sources.IsGiven(w.ClassId)} recipes={world.Sources.MadeBy(w.ClassId).Count}");
            Console.WriteLine("  int   " + string.Join(" ", w.Ints.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")));
            Console.WriteLine("  float " + string.Join(" ", w.Floats.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value:G6}")));
            Console.WriteLine("  bool  " + string.Join(" ", w.Bools.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")));
            Console.WriteLine("  attr  " + string.Join(" ", w.Attributes.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value.Base}")));
            Console.WriteLine("  vital " + string.Join(" ", w.Vitals.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value.Base}")));
            Console.WriteLine("  body  " + string.Join(" ", w.BodyParts.Select(p => $"{p.Key}:{p.BaseArmor}@{p.HitWeight:G3}")));
            foreach (AceRecipe recipe in world.Sources.MadeBy(w.ClassId))
            {
                Console.WriteLine($"  recipe {recipe.Id} skill {recipe.Skill} difficulty {recipe.Difficulty} makes {recipe.SuccessAmount}, from:");
                foreach (uint part in world.Sources.Ingredients(recipe.Id))
                {
                    string name = world.Weenies.TryGetValue(part, out AceWeenie? p) ? p.ToString() : part.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    Console.WriteLine($"    {name} sold={world.Sources.IsSold(part)} given={world.Sources.IsGiven(part)} recipes={world.Sources.MadeBy(part).Count}");
                }
            }
        }
        return 0;
    }
}
