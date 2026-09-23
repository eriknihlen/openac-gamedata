using OpenAC.GameData.Sql;

namespace OpenAC.GameData.Ace;

/// <summary>
/// One row of ACE's spell table, with the columns the generator reads. A
/// column the row leaves empty is null.
/// </summary>
internal sealed record AceSpell(
    uint Id,
    string Name,
    int? DamageType,
    double? DrainPercentage,
    double? DamageRatio,
    int? Source,
    int? Destination,
    double? Proportion,
    double? LossPercent,
    int? TransferCap,
    int? TransferFlags)
{
    public static Dictionary<uint, AceSpell> FromTable(SqlTable table)
    {
        int id = table.Column("id"), name = table.Column("name"), damageType = table.Column("e_Type");
        int drain = table.Column("drain_Percentage"), ratio = table.Column("damage_Ratio");
        int source = table.Column("source"), destination = table.Column("destination");
        int proportion = table.Column("proportion"), loss = table.Column("loss_Percent");
        int cap = table.Column("transfer_Cap"), flags = table.Column("transfer_Bitfield");
        var spells = new Dictionary<uint, AceSpell>();
        foreach (object?[] row in table.Rows)
        {
            uint spellId = unchecked((uint)(long)row[id]!);
            spells[spellId] = new AceSpell(
                spellId,
                row[name] as string ?? string.Empty,
                Int(row[damageType]),
                Real(row[drain]),
                Real(row[ratio]),
                Int(row[source]),
                Int(row[destination]),
                Real(row[proportion]),
                Real(row[loss]),
                Int(row[cap]),
                Int(row[flags]));
        }
        return spells;
    }

    private static int? Int(object? value) => value is long whole ? (int)whole : null;

    private static double? Real(object? value) => value switch
    {
        long whole => whole,
        double real => real,
        _ => null,
    };
}
