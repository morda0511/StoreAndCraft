# Changelog

## 1.1.2 — Multiplayer Hotfix

- Multiplayer: dump / store no longer steals chest ownership (no more kicking the other player out of an open chest)
- Multiplayer: deposits go to the chest owner via RPC; failed deposits are refunded (fixes items vanishing)

## 1.1.1

- Dedicated / hosted servers: handshake uses the vanilla connection (ZRpc), no kick on slow load
- Dedicated auto-store runs around every player, not only a local character
- Config request targets the server peer

## 1.1.0

- Storage Display (Hammer): shows nearby chest totals by type; **[E]** opens the Valheim skills-style type list
- Types: Wood, Ore, Metals, Stone, Fuel, Hides, Parts, Crops & Seeds, Raw Food, Gems & Coins, Boss / Rare, Food, Fish, Trophy, Ammo, Tools, Weapons, Armor, Utility, Misc
- Several displays of the same type page together; ignored **`[I]`** chests are not counted
- **[E]** refill on smelters, kilns, ovens, torches, fires (and related stations) from nearby chests
- LeaveOneItem on every chest pull (craft, build, station, Ctrl+middle-click fill)
- Middle-click store / Ctrl+middle-click fill only while inventory is open
- Auto-stack, StorageRange, **`[I]`** ignore prefix with red hover text
- Safer chest withdraw / no double-apply on store

## 1.0.0

- Auto-store, dump, craft/build from nearby chests
- Rename chests, YAML rules, server config sync
