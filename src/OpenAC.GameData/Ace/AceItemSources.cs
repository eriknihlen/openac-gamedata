using OpenAC.GameData.Sql;

namespace OpenAC.GameData.Ace;

/// <summary>One recipe: what it makes, and the skill check it asks for.</summary>
internal sealed record AceRecipe(
    uint Id,
    int Skill,
    int Difficulty,
    uint SuccessClassId,
    int SuccessAmount,
    string SuccessMessage,
    uint FailClassId,
    int FailAmount,
    string FailMessage);

/// <summary>One way to use a recipe: this source item on this target item.</summary>
internal readonly record struct AceCookBookEntry(uint RecipeId, uint SourceClassId, uint TargetClassId);

/// <summary>The cheapest way an item can be had, cheapest first.</summary>
internal enum AceAcquisition
{
    /// <summary>Bought for pyreals, found as loot or in a container, or made from such items.</summary>
    Ordinary = 0,

    /// <summary>Bought for an alternate currency (tokens, trophies), or made from such items.</summary>
    Currency = 1,

    /// <summary>Only handed out by an NPC, or made from such items.</summary>
    Given = 2,

    /// <summary>No source in the world data.</summary>
    None = 3,
}

/// <summary>
/// Where an item can come from in the world data: a vendor's shop list, an
/// NPC's gift (an emote that gives it), or a recipe.
/// </summary>
internal sealed class AceItemSources
{
    private const int ContainDestination = 0x1;
    private const int WieldDestination = 0x2;
    private const int ShopDestination = 0x4;
    private const int TreasureDestination = 0x8;
    private const int GiveEmote = 3;
    private const int VendorWeenieType = 12;
    private const int AlternateCurrencyDataId = 57;

    private readonly HashSet<uint> _sold;
    private readonly HashSet<uint> _soldForCurrency;
    private readonly HashSet<uint> _given;
    private readonly HashSet<uint> _found;
    private readonly HashSet<uint> _wielded;
    private readonly Dictionary<uint, AceAcquisition> _acquisition = [];
    private readonly ILookup<uint, uint> _ingredients;
    private readonly Dictionary<uint, List<AceRecipe>> _madeBy;

    private AceItemSources(
        HashSet<uint> sold,
        HashSet<uint> soldForCurrency,
        HashSet<uint> given,
        HashSet<uint> found,
        HashSet<uint> wielded,
        Dictionary<uint, AceRecipe> recipes,
        List<AceCookBookEntry> cookBook)
    {
        _sold = sold;
        _soldForCurrency = soldForCurrency;
        _given = given;
        _found = found;
        _wielded = wielded;
        Recipes = recipes;
        CookBook = cookBook;
        _madeBy = recipes.Values
            .Where(static r => r.SuccessClassId != 0)
            .GroupBy(static r => r.SuccessClassId)
            .ToDictionary(static g => g.Key, static g => g.ToList());
        _ingredients = cookBook
            .SelectMany(static e => new[] { (e.RecipeId, e.SourceClassId), (e.RecipeId, e.TargetClassId) })
            .Distinct()
            .ToLookup(static p => p.RecipeId, static p => p.Item2);
        SettleAcquisitions();
    }

    public IReadOnlyDictionary<uint, AceRecipe> Recipes { get; }

    public IReadOnlyList<AceCookBookEntry> CookBook { get; }

    /// <summary>Does some vendor sell this item for pyreals?</summary>
    public bool IsSold(uint classId) => _sold.Contains(classId);

    /// <summary>Does some vendor sell this item for an alternate currency (tokens, trophies)?</summary>
    public bool IsSoldForCurrency(uint classId) => _soldForCurrency.Contains(classId);

    /// <summary>Does some creature wield this item as its own equipment?</summary>
    public bool IsWielded(uint classId) => _wielded.Contains(classId);

    /// <summary>Does some NPC hand this item out?</summary>
    public bool IsGiven(uint classId) => _given.Contains(classId);

    /// <summary>The recipes whose success makes this item.</summary>
    public IReadOnlyList<AceRecipe> MadeBy(uint classId) =>
        _madeBy.TryGetValue(classId, out List<AceRecipe>? recipes) ? recipes : [];

    /// <summary>The items used in a recipe: every cook-book source and target.</summary>
    public IEnumerable<uint> Ingredients(uint recipeId) => _ingredients[recipeId];

    /// <summary>
    /// The cheapest way the item can be had: directly, or through a recipe
    /// whose ingredients can all be had that way (a recipe is as dear as its
    /// dearest ingredient).
    /// </summary>
    public AceAcquisition Acquisition(uint classId) =>
        _acquisition.TryGetValue(classId, out AceAcquisition known) ? known : AceAcquisition.None;

    /// <summary>
    /// Settles every item's acquisition: start from the direct sources, then
    /// let each recipe offer its dearest ingredient's way to its result, until
    /// nothing gets cheaper. Recipe loops (wrapping and unwrapping) settle too.
    /// </summary>
    private void SettleAcquisitions()
    {
        foreach (uint id in _found.Concat(_sold))
            _acquisition[id] = AceAcquisition.Ordinary;
        foreach (uint id in _soldForCurrency)
            Offer(id, AceAcquisition.Currency);
        foreach (uint id in _given)
            Offer(id, AceAcquisition.Given);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (AceRecipe recipe in Recipes.Values)
            {
                if (recipe.SuccessClassId == 0)
                    continue;
                AceAcquisition dearest = Ingredients(recipe.Id).Select(Acquisition).DefaultIfEmpty(AceAcquisition.None).Max();
                changed |= Offer(recipe.SuccessClassId, dearest);
            }
        }
    }

    private bool Offer(uint classId, AceAcquisition way)
    {
        if (way >= Acquisition(classId))
            return false;
        _acquisition[classId] = way;
        return true;
    }

    public static AceItemSources FromTables(
        IReadOnlyDictionary<string, SqlTable> tables,
        IReadOnlyDictionary<uint, AceWeenie> weenies)
    {
        var sold = new HashSet<uint>();
        var soldForCurrency = new HashSet<uint>();
        var found = new HashSet<uint>();
        var wielded = new HashSet<uint>();
        SqlTable createList = tables["weenie_properties_create_list"];
        int owner = createList.Column("object_Id");
        int destination = createList.Column("destination_Type");
        int item = createList.Column("weenie_Class_Id");
        foreach (object?[] row in createList.Rows)
        {
            long where = (long)row[destination]!;
            // Items a creature wields are its own equipment, not something a
            // player finds; what it or a chest contains, or its treasure, is.
            if ((where & ShopDestination) == 0)
            {
                if ((where & (ContainDestination | TreasureDestination)) != 0)
                    found.Add(U(row[item]));
                else if ((where & WieldDestination) != 0)
                    wielded.Add(U(row[item]));
            }
            else if (weenies.TryGetValue(U(row[owner]), out AceWeenie? seller) && seller.Type == VendorWeenieType)
            {
                (seller.DataIds.ContainsKey(AlternateCurrencyDataId) ? soldForCurrency : sold).Add(U(row[item]));
            }
        }

        var given = new HashSet<uint>();
        SqlTable actions = tables["weenie_properties_emote_action"];
        int type = actions.Column("type");
        int gift = actions.Column("weenie_Class_Id");
        foreach (object?[] row in actions.Rows)
        {
            if ((long)row[type]! == GiveEmote && row[gift] is long classId)
                given.Add(unchecked((uint)classId));
        }

        var recipes = new Dictionary<uint, AceRecipe>();
        SqlTable recipe = tables["recipe"];
        int id = recipe.Column("id"), skill = recipe.Column("skill"), difficulty = recipe.Column("difficulty");
        int success = recipe.Column("success_W_C_I_D"), successAmount = recipe.Column("success_Amount");
        int successMessage = recipe.Column("success_Message");
        int fail = recipe.Column("fail_W_C_I_D"), failAmount = recipe.Column("fail_Amount");
        int failMessage = recipe.Column("fail_Message");
        foreach (object?[] row in recipe.Rows)
        {
            recipes[U(row[id])] = new AceRecipe(
                U(row[id]),
                (int)(long)row[skill]!,
                (int)(long)row[difficulty]!,
                U(row[success]),
                (int)(long)row[successAmount]!,
                row[successMessage] as string ?? string.Empty,
                U(row[fail]),
                (int)(long)row[failAmount]!,
                row[failMessage] as string ?? string.Empty);
        }

        var cookBook = new List<AceCookBookEntry>();
        SqlTable book = tables["cook_book"];
        int recipeId = book.Column("recipe_Id"), source = book.Column("source_W_C_I_D"), target = book.Column("target_W_C_I_D");
        foreach (object?[] row in book.Rows)
            cookBook.Add(new AceCookBookEntry(U(row[recipeId]), U(row[source]), U(row[target])));

        return new AceItemSources(sold, soldForCurrency, given, found, wielded, recipes, cookBook);
    }

    private static uint U(object? value) => unchecked((uint)(long)value!);
}
