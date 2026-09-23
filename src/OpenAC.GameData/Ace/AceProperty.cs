namespace OpenAC.GameData.Ace;

/// <summary>The property ids and enum values the generator reads, as ACE numbers them.</summary>
internal static class AceProperty
{
    public static class Int
    {
        public const int ItemType = 1;
        public const int CreatureType = 2;
        public const int Level = 25;
        public const int Damage = 44;
        public const int DamageType = 45;
        public const int AmmoType = 50;
        public const int WieldRequirements = 158;
        public const int WieldSkillType = 159;
        public const int WieldDifficulty = 160;
        public const int ElementalDamageBonus = 204;
        public const int WieldRequirements2 = 270;
        public const int WieldSkillType2 = 271;
        public const int WieldDifficulty2 = 272;
        public const int WieldRequirements3 = 273;
        public const int WieldSkillType3 = 274;
        public const int WieldDifficulty3 = 275;
    }

    public static class Float
    {
        public const int ArmorModVsSlash = 13;
        public const int ArmorModVsPierce = 14;
        public const int ArmorModVsBludgeon = 15;
        public const int ArmorModVsCold = 16;
        public const int ArmorModVsFire = 17;
        public const int ArmorModVsAcid = 18;
        public const int ArmorModVsElectric = 19;
        public const int DamageVariance = 22;
        public const int DamageMod = 63;
        public const int ResistSlash = 64;
        public const int ResistPierce = 65;
        public const int ResistBludgeon = 66;
        public const int ResistFire = 67;
        public const int ResistCold = 68;
        public const int ResistAcid = 69;
        public const int ResistElectric = 70;
    }

    public static class Bool
    {
        public const int Attackable = 19;
        public const int NonProjectileMagicImmune = 103;
    }

    public static class String
    {
        public const int Name = 1;
    }

    public static class Attribute
    {
        public const int Endurance = 2;
    }

    public static class Vital
    {
        public const int MaxHealth = 1;
    }

    public static class WeenieType
    {
        public const int Ammunition = 5;
        public const int Creature = 10;
        public const int Cow = 15;
    }

    /// <summary>The <c>WieldRequirements</c> kinds the generator understands.</summary>
    public static class WieldRequirement
    {
        public const int Skill = 1;
        public const int RawSkill = 2;
        public const int Training = 8;
    }

    public static class AmmoType
    {
        public const int Arrow = 0x1;
        public const int Bolt = 0x2;
        public const int Atlatl = 0x4;
    }

    /// <summary>ACE's damage type flags.</summary>
    [Flags]
    public enum DamageType
    {
        None = 0,
        Slash = 0x1,
        Pierce = 0x2,
        Bludgeon = 0x4,
        Cold = 0x8,
        Fire = 0x10,
        Acid = 0x20,
        Electric = 0x40,

        /// <summary>No element of its own: the hit takes the launcher's.</summary>
        Base = 0x10000000,
    }

    public const int MissileWeaponsSkill = 47;
}
