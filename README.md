# openac-gamedata

Builds `gameinfodb.ugd`, the VTank-format game database the MossTank plugin
reads: ammunition, the damage element to use on each monster, monster health
and species, heal kits, grenades, drain and martyr spells, and crafting
recipes. Everything comes from ACE's open world database.

## Build

Needs the .NET 10 SDK.

```
dotnet run --project src/OpenAC.GameData -c Release -- build
```

Downloads the ACE release pinned in `ace-world.json` and writes
`out/gameinfodb.ugd`. The same pin always gives the same file.

`compare <ours.ugd> <other.ugd> --input <dump>` reports where two game
databases differ. The rule for each table is described in its source file under
`src/OpenAC.GameData/GameInfo/`.

## License

AGPL-3.0. The generated file is derived from ACE's AGPL world data. See `NOTICE`.
