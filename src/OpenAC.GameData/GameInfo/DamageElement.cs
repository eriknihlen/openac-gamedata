using OpenAC.GameData.Ace;

namespace OpenAC.GameData.GameInfo;

/// <summary>VTank's damage element numbers, as its game database writes them.</summary>
internal enum DamageElement
{
    Pierce = 0,
    Bludgeon = 1,
    Slash = 2,
    Acid = 3,
    Lightning = 4,
    Cold = 5,
    Fire = 6,

    /// <summary>The element number an ammunition row uses for "every element".</summary>
    PrismaticAmmunition = 100,
}

internal static class DamageElements
{
    /// <summary>
    /// The seven elements, in the order used to break a tie between two that
    /// are equally good: slash first, as VTank's own lists default to it.
    /// </summary>
    public static readonly DamageElement[] All =
    [
        DamageElement.Slash, DamageElement.Pierce, DamageElement.Bludgeon,
        DamageElement.Acid, DamageElement.Lightning, DamageElement.Cold, DamageElement.Fire,
    ];

    /// <summary>The single element of an ACE damage type, or null when it has none or several.</summary>
    public static DamageElement? FromAce(AceProperty.DamageType type) => type switch
    {
        AceProperty.DamageType.Slash => DamageElement.Slash,
        AceProperty.DamageType.Pierce => DamageElement.Pierce,
        AceProperty.DamageType.Bludgeon => DamageElement.Bludgeon,
        AceProperty.DamageType.Cold => DamageElement.Cold,
        AceProperty.DamageType.Fire => DamageElement.Fire,
        AceProperty.DamageType.Acid => DamageElement.Acid,
        AceProperty.DamageType.Electric => DamageElement.Lightning,
        _ => null,
    };

    /// <summary>A creature's armor-modifier property for the element.</summary>
    public static int ArmorModProperty(DamageElement element) => element switch
    {
        DamageElement.Slash => AceProperty.Float.ArmorModVsSlash,
        DamageElement.Pierce => AceProperty.Float.ArmorModVsPierce,
        DamageElement.Bludgeon => AceProperty.Float.ArmorModVsBludgeon,
        DamageElement.Cold => AceProperty.Float.ArmorModVsCold,
        DamageElement.Fire => AceProperty.Float.ArmorModVsFire,
        DamageElement.Acid => AceProperty.Float.ArmorModVsAcid,
        DamageElement.Lightning => AceProperty.Float.ArmorModVsElectric,
        _ => throw new ArgumentOutOfRangeException(nameof(element)),
    };

    /// <summary>A creature's natural-resistance property for the element.</summary>
    public static int ResistProperty(DamageElement element) => element switch
    {
        DamageElement.Slash => AceProperty.Float.ResistSlash,
        DamageElement.Pierce => AceProperty.Float.ResistPierce,
        DamageElement.Bludgeon => AceProperty.Float.ResistBludgeon,
        DamageElement.Cold => AceProperty.Float.ResistCold,
        DamageElement.Fire => AceProperty.Float.ResistFire,
        DamageElement.Acid => AceProperty.Float.ResistAcid,
        DamageElement.Lightning => AceProperty.Float.ResistElectric,
        _ => throw new ArgumentOutOfRangeException(nameof(element)),
    };

    /// <summary>A preference list as a database <c>DamageString</c>: <c>2;0;1</c>.</summary>
    public static string Format(IEnumerable<DamageElement> order) =>
        string.Join(';', order.Select(static e => ((int)e).ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
