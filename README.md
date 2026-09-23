# openac-gamedata

Generates a VTank-format game database, `gameinfodb.ugd`, from the open ACE
world database. The MossTank plugin for the OpenAC client reads this file to
choose ammunition, the damage element to use on each monster, heal kits,
grenades, drain and martyr spells, and crafting recipes.

License: AGPL-3.0 (see `LICENSE` and `NOTICE`). The generated file is derived
from ACE's AGPL world data and is AGPL-3.0 too.

## Build it

Needs the .NET 10 SDK.

```
dotnet run --project src/OpenAC.GameData -c Release -- build
```

This downloads the ACE world-database release pinned in `ace-world.json` into
`.cache/` (checking its SHA-256), reads it directly from the SQL dump (no
database server), and writes `out/gameinfodb.ugd`. The same input always gives
the same file: `DBLastUpdateTime` is the pinned release's publication time.

Other commands:

| Command | Does |
|---|---|
| `fetch` | only download and verify the pinned input |
| `build --input <dump.sql or .zip> --output <file>` | build from another dump |
| `compare <ours.ugd> <other.ugd> [--input <dump>]` | report where two game databases agree and differ; with `--input`, weigh each differing monster pick by ACE's damage numbers |
| `show <dump> <name or class id>` | print a weenie's properties, sources and recipes |

To move to a newer ACE release, update `ace-world.json` (release, asset,
sha256, published).

## What is in the file

| Table | Status | Source and rule |
|---|---|---|
| `DBVersion` | done | 9, the version VTank's built-in database has; VTank discards a file with any other |
| `DBLastUpdateTime` | done | the pinned ACE release's publication time |
| `SpeciesMembers` | done | every attackable creature name: its `CreatureType` (-1 when it has none) and maximum health |
| `SpeciesDamages` | done | per species, all seven elements ordered by the median damage share of its members |
| `MonsterDamageOverrides` | done | a monster's own order, when following its species' order could cost more than 10% |
| `MonsterImmunities` | done | mask 3 for creatures immune to non-projectile magic |
| `AmmunitionOptions` | done | every arrow, bolt and atlatl dart a player can get |
| `HealKits` | done | every healing, stamina and mana kit: restore bonus, skill bonus, vital (health 1, stamina 2, mana 3) |
| `GrenadeOptions` | done | thrown items that cast their spell every time they land (phials): wield requirement, spell, spellcraft |
| `DrainSpellOptions` | done | health drains (target to caster) from ACE's spell table |
| `MartyrSpellOptions` | done | health martyr spells (caster's health spent to hurt the target) from ACE's spell table |
| `CooldownIDs` | done | every item with a shared use timer |
| `CraftInteractions` | done | every use of one item on another that makes a new item, from ACE's recipes |
| `SpellQualityOverrides` | empty on purpose | VTank's own corrections to its spell ranking; nothing in the world data says what they should be, and MossTank does not read the table |

### Monster rules

- **Which creature stands for a name.** ACE often has several creature
  weenies with one name. The one the world places most often (landblock
  placements plus generator entries) is used; ties go to the lowest class id.
- **Maximum health** = the health vital's points plus half of endurance,
  rounded half away from zero, as ACE computes it.
- **Damage share** of an element = the armor share (armor level A lets
  `66.7 / (A + 66.7)` of a hit through; A is each body part's base armor times
  the creature's armor modifier for the element, averaged over body parts by
  how often each is hit) times the creature's resistance to the element. This
  is ACE's melee and missile damage formula with everything that does not
  depend on the element left out.
- **Order**: most damage first. Elements within a millionth of each other keep
  the order slash, pierce, bludgeon, acid, lightning, cold, fire.
- **Override rule**: VTank uses the first element on the list that the player
  can deal, so a monster gets its own list when some pair of elements, in the
  species' order, puts the first more than 10% below the second on that monster.

### Ammunition rules

- **Launcher**: arrow 5, bolt 6, atlatl dart 7. **Element**: the ammunition's
  damage type; ammunition that takes the launcher's element is prismatic (100).
- **WieldReq**: the highest Missile Weapons requirement among the three wield
  requirement sets; **WieldReq2Skill/Value**: the first requirement on another skill.
- **Quality** (our ranking): average damage per shot in tenths of a point,
  `10 × damage × (1 − variance / 2)`. VTank only compares rows of the same
  launcher and element (and prismatic rows), and takes the later of two
  equal rows.
- **Special** (VTank's opt-in groups): 0 when the ammunition, or everything
  it is made from, can be bought for pyreals or found as loot; 1 when it takes
  something bought with an alternate currency; 2 otherwise (only handed out by
  NPCs, or no source in the data).
- **Which weenie stands for a name**: the one easiest to have, then the lowest
  class id. Ammunition that exists only as a creature's own wielded equipment
  is left out.

### Item, spell and recipe rules

- **Which weenie stands for a name** (kits, grenades, cooldown items): the
  easiest to have, then the lowest class id; creature-only gear is left out.
- **HealKits**: restore bonus is the kit's heal-kit modifier (1 when it has
  none), skill bonus its boost value, the vital from what it boosts.
- **GrenadeOptions**: thrown items with a spell, a spellcraft and a spell rate
  of 1. A thrown weapon that only sometimes casts is a weapon, not a grenade.
- **CooldownIDs**: an item's shared cooldown number, written as the signed
  16-bit spell id the game uses for the cooldown (`number - 32768`).
- **DrainSpellOptions**: spells whose source is the target's health and whose
  destination is the caster's. Drain factor = the spell's proportion; result
  multiplier = 1 - its loss percent; the transfer cap limits both what is taken
  and what is given, so the most taken is `cap / max(1, multiplier)`.
- **MartyrSpellOptions**: health spells with a drain percentage (the share of
  the caster's own health spent) and a damage ratio (the result multiplier).
- **Cast times** (both spell tables): the cast times known from play for
  these spells. A spell not among them gets an estimate: 500 ms per level (its
  rank by strength within the table), at most 2.5 s. The world data has none.
- **CraftInteractions**: one row per cook-book entry whose recipe makes a named
  item (the item used first, the item it is used on second, as the game only
  accepts that order). Difficulty is the recipe's own: the skill at which the
  craft succeeds half the time. The row id is the cook-book entry's id. Recipes
  that only change the target (tinkering, dyeing) make nothing new and are
  left out.

## Checking the output

`tests/` builds a small world database in memory and reads the generated file
back with a copy of MossTank's own reader. To compare with another game
database you have locally:

```
dotnet run --project src/OpenAC.GameData -c Release -- compare out/gameinfodb.ugd <other.ugd> --input .cache/<asset>
```

Never commit or publish another game database; compare against it locally only.

Note for MossTank: its update check asks VTank's online service for the
changes since the file's `DBLastUpdateTime` and merges them into the file it
loaded, so with this file in place, rows the service changed after the ACE
release date are merged in on top.
