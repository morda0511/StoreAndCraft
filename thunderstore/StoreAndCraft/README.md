# StoreAndCraft

**Your base runs with you.**  
Loot finds its chest. One key clears your pockets. Craft, upgrade, and build straight from storage. Feed kilns and smelters with **[E]** — or toggle **auto-fill with B**. Cooking stations can **auto-drop finished food with N** so chests pick it up. Tell each station which wood or ore it’s allowed to take. Storage Displays track your stock by type (pick several at once). Mark favorites with **F**, find a stack with **Y**, sort with **R**, skip a chest with **`[I]`**. Carts and ships count too.

**Required on the dedicated / hosted server and every PC client** (same version). Valheim 1.0 · BepInExPack 5.4.2350+. Console players via crossplay cannot load the mod.

**Bug reports:** https://github.com/morda0511/StoreAndCraft/issues/1  

**Discord:** https://discord.gg/aVKVVmyzj

---

## Features

### Auto-store
- Ground items are pulled into nearby chests automatically if **any one player** is in `StoreRange` of the pile. You can walk off; a buddy standing there is enough.
- Default: a chest only accepts an item if that type is **already inside** (`MustHaveExisting`).
- Carts (`Vagon`) and **ships** count as containers too.
- **Player-built chests only** for dump / auto-store (crypts, house spawns, and other world chests are skipped).
- Ward / private area rules still apply.

### Dump & quick store
- **`.`** — dump allowed inventory stacks into matching nearby **player-built** chests. If the chest cannot take the whole stack, it fills what fits and leaves the rest.
- **Middle mouse** (inventory open) — store only the hovered item.
- Hotbar can be skipped (`IgnoreHotbar`).
- Favorites are never dumped / hover-stored (see below).
- **Remote dump (experimental):** if nothing matches near you, dump can ask a **buddy who is near matching base chests** to finish the store (they must have that area loaded). Toggle `RemoteDumpEnabled` (default on). Cooldown / max stacks in config. Easy kill-switch — set false or remove `Network/RemoteDump.cs` + `BEGIN REMOTE_DUMP` blocks.

### Craft & build from chests
- Recipes and hammer pieces count nearby chest contents.
- When you craft / upgrade / build, missing mats are **taken from chests directly** (not dumped into your backpack first — that used to fill free slots and cancel the craft).
- Craft / upgrade button enables when mats are in **inventory or chests**.
- Yellow tint on requirement text when part of the count comes from a chest.
- `LeaveOneItem` (default on): one item stays in each chest so auto-store can keep refilling. That leftover item cannot be spent (craft, build, plant, or station [E]). Turn the setting off if you want to use the last item.
- **Shift + place:** hold **Shift** while confirming a build to **grab** that piece’s materials from nearby chests into your inventory (nothing is placed).
- **Epic Loot:** works alongside Epic Loot — craft/enchant chest-pull uses the same requirement path when the station checks mats; Storage Displays soft-match EL reagents/hides.

### Station refill `[E]`
Works on smelters, charcoal kilns, cooking stations, fires / torches, fermenters, turrets, etc.:
- Pulls fuel / ore / food from nearby chests when you don’t have it.
- **Inventory first:** if you already hold a valid item (e.g. deer meat), that is used before chest contents (e.g. boar meat).

### Kiln / smelter / cooking auto-fill
- Look at a kiln, smelter, blast furnace, cooking spit, iron cooking station, stone oven, fermenter, or torch / fire with the inventory **closed** → **B** toggles auto-fill for that station (saved on the station).
- When fuel or ore hits **0**, it sends a full load (smelter max, e.g. 10) from inventory first, then chests within `AutoFillRange`. Then it waits until that slot is empty again. If chests are empty too, the next chest check is after **20 seconds**.
- Cooking / ovens top up **free slots** (not only when fully empty). Stone oven door hover shows the toggle; food is added even if the under-fire is out (baking still needs fire to cook).
- No chest scans and no “it’s full” spam while the station is still running. Filter still applies (Wood OFF stays OFF).
- **F** with inventory open is still favorites. Manual **[E]** refill is unchanged.

### Cooking auto-drop
- Look at a cooking spit, iron cooking station, or stone oven (inventory **closed**) → **N** toggles auto-drop for that station (saved on the station).
- Finished food falls off as a ground drop so auto-store can put it in a matching chest.
- Pair with **B** auto-fill for hands-off cooking / baking (fire under the stone oven still required to cook).

### Kiln / smelter pull filter
- Look at a kiln, multi-input smelter, or grill → **Alt+E** (remap in config if needed).
- Skills-style list with **[+]** / **[-]** toggles.
- Choose which wood / ore types may be auto-pulled (e.g. turn **Wood** OFF, leave **Core wood** ON).
- Saved per station (ZDO). Manual use from inventory is not blocked for types you hold yourself; auto-select / chest pull respects the filter.

### Storage Display (Hammer)
- Build **Small / Medium / Large Storage Display**.
- **[E]** → type menu (**[+]** / **[-]**). Up to **12 categories** per board (yellow message if you try more).
- Large: one band per category, labels left, items wrap under the item columns (not under the name). Band height scales with how many categories you pick.
- Chests named with **`[I]`** are ignored.
- **Alt+R** → per-display chest scan range (5–50 m).

### Favorites
- Inventory open → hover item → **F**.
- Gold tint, **★** badge, tooltip line.
- Skipped by dump and hover-store.
- Local file: `StoreAndCraft.favorites.txt` (not server-synced).

### Ignore chests
- Prefix the chest name with **`[I]`** → no auto-store, no craft pull, not counted on displays.
- Hover text turns **red** so ignored chests are easy to spot.
- Rename with **Alt+E** (or Shift+E alt-use; remap in config if needed).

### Search
- Inventory open → hover an item → press **Y**.
- Nearest chest that holds it **blinks 3 times** and gets a map ping.

### Sort
- Inventory open → **R**.
- Bag only: sorts your inventory. Open chest: sorts **that chest only** (not both).
- Favorites and the **hotbar stay put** — sort never packs items into hotbar slots.

### Multiplayer
- Install on **server + all clients**, same version.
- Server config sync (`LockConfig` default on).
- Safe chest transfers (no ownership steal while someone has a chest open).

---

## Keys (defaults)

| Key | Action |
|---|---|
| **.** | Dump inventory into matching nearby chests |
| **Middle mouse** | Store hovered inventory item (inventory open) |
| **Ctrl + Middle mouse** | Fill hovered stack from nearby chests |
| **Alt + E** | Rename looked-at chest **or** open kiln/smelter pull filter |
| **F** | Favorite / unfavorite hovered inventory item |
| **B** | Toggle auto-fill on the station you are looking at (inventory closed) |
| **N** | Toggle auto-drop on a cooking spit / iron station / stone oven (inventory closed) |
| **Y** | Hover an inventory item and press — nearest chest with that item blinks **3×** + map ping |
| **R** | Sort: bag open = inventory only; chest open = that chest only |
| **Shift + place** | Grab build materials from chests (do not place) |
| **Alt + R** | Storage Display range |

All hotkeys are configurable in the `.cfg` and stay **local** (not overwritten by server sync).

---

## Chat / console commands

Works in **chat** (`/…`) and in the **F5 console** (with or without leading `/`).

Changing ranges: **host / server admin only**. Anyone can view status / help.

<details>
<summary><b>Command list (click to expand)</b></summary>

| Command | Who | What it does |
|---|---|---|
| `/help store` | Everyone | Print help to console (F5) |
| `/help storeandcraft` | Everyone | Same as above |
| `/storehelp` | Everyone | Same as above |
| `/sac status` | Everyone | Show current Dump / Store / Storage / Craft / AutoFill ranges + LockConfig |
| `/dumprange <n>` | Host / admin | Set dump / middle-click range (meters) |
| `/storerange <n>` | Host / admin | Set auto-store ground→chest range |
| `/storagerange <n>` | Host / admin | Set take-stack / search / display range |
| `/craftrange <n>` | Host / admin | Set craft / build / station-pull range |
| `/autofillrange <n>` | Host / admin | Set auto-fill station + chest range |
| `/sac dump <n>` | Host / admin | Alias for `/dumprange` |
| `/sac store <n>` | Host / admin | Alias for `/storerange` |
| `/sac storage <n>` | Host / admin | Alias for `/storagerange` |
| `/sac craft <n>` | Host / admin | Alias for `/craftrange` |
| `/sac autofill <n>` | Host / admin | Alias for `/autofillrange` |

**Examples**

```
/sac status
/craftrange 25
/autofillrange 40
/sac dump 10
```

Ranges are saved to `com.morda.storeandcraft.cfg` and synced to clients when `LockConfig` is on. Valid range: **0–500**. Edit the `.cfg` on the host / server and save, or use the commands: **no restart**.

</details>

---

## Storage Display types

**Resources:** Wood · Ore · Metals · Stone · Fuel · Hides · Parts · Crops & Seeds · Raw Food · Gems & Coins · Boss / Rare  

**Other:** Food · Fish · Trophy · Ammo · Tools · Weapons · Armor · Utility · Misc  

---

## Config

**One file only** (no YAML):

`BepInEx/config/com.morda.storeandcraft.cfg`

| Setting | Meaning |
|---|---|
| `LockConfig` | `true` = server owns gameplay values; clients receive them on join |
| `ModEnabled` / `StoreEnabled` / `CraftEnabled` | Feature toggles |
| `MustHaveExisting` | Chest only accepts item types it already holds |
| `LeaveOneItem` | Leave 1 in chest when pulling |
| `IgnoreHotbar` | Dump skips hotbar row |
| `AutoStackEnabled` | Compact stacks inside chests (off by default) |
| `PlayerDumpRange` | Dump / middle-click (m) |
| `StoreRange` | Auto-store ground items (m) |
| `StorageRange` | Take-stack / search / displays (m) |
| `CraftRange` | Craft / build / station `[E]` (m) |
| `AutoFillRange` | Auto-fill: player → station and player → chests (m). Default 20 |
| `IntakeInterval` | Seconds between ground-item scans |
| Hotkeys (`DumpKey`, `FavoriteKey`, `AutoFillKey`, `SortKey`, …) | Local only |

**Dedicated:** edit the `.cfg` on the **server**, save — it reloads and syncs. Client gameplay edits are ignored while `LockConfig` is on.

---

## Install

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Drop `StoreAndCraft.dll` into `BepInEx/plugins/StoreAndCraft/` (or install via Thunderstore / r2modman).
3. On dedicated servers: install the same version on the **server** and every client.
