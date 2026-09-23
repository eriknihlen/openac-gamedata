using OpenAC.GameData.Ace;
using OpenAC.GameData.Vtank;

namespace OpenAC.GameData.GameInfo;

/// <summary>Assembles the whole game database from ACE's world data.</summary>
internal static class GameInfoBuilder
{
    /// <param name="world">The world data.</param>
    /// <param name="lastUpdate">
    /// The <c>DBLastUpdateTime</c> to write, in seconds since 1970. Passing the
    /// input's own date keeps the output the same for the same input.
    /// </param>
    public static VtankDatabase Build(AceWorld world, DateTimeOffset lastUpdate)
    {
        ArgumentNullException.ThrowIfNull(world);
        var database = new VtankDatabase();

        VtankTable time = GameInfoSchema.NewTable("DBLastUpdateTime");
        GameInfoSchema.AddRow(time, VtankCell.Int(0), VtankCell.Int(checked((int)lastUpdate.ToUnixTimeSeconds())));
        database.Tables.Add(("DBLastUpdateTime", time));

        VtankTable version = GameInfoSchema.NewTable("DBVersion");
        GameInfoSchema.AddRow(version, VtankCell.Int(GameInfoSchema.DatabaseVersion));
        database.Tables.Add(("DBVersion", version));

        AmmunitionTable.Build(world, database);
        MonsterTables.Build(world, database);
        ItemTables.Build(world, database);
        SpellTables.Build(world, database);
        CraftTable.Build(world, database);

        // Tables not generated yet are still written, empty, so a reader that
        // expects every table finds it.
        foreach (string name in GameInfoSchema.TableNames)
        {
            if (database.Find(name) is null)
                database.Tables.Add((name, GameInfoSchema.NewTable(name)));
        }
        return database;
    }
}
