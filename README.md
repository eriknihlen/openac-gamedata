# openac-gamedata

Generates a VTank-format game database, `gameinfodb.ugd`, from the open ACE
world database. The MossTank plugin for the OpenAC client reads this file to
choose ammunition, the damage element to use on each monster, and (later)
heal kits, grenades, drain spells and crafting recipes.

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
| `HealKits`, `GrenadeOptions`, `DrainSpellOptions`, `MartyrSpellOptions`, `CooldownIDs`, `SpellQualityOverrides` | empty for now | phase 2 |
| `CraftInteractions` | empty for now | phase 3 |

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

## Checking the output

`tests/` builds a small world database in memory and reads the generated file
back with a copy of MossTank's own reader. To compare with another game
database you have locally:

```
dotnet run --project src/OpenAC.GameData -c Release -- compare out/gameinfodb.ugd <other.ugd> --input .cache/<asset>
```

Never commit or publish another game database; compare against it locally only.
