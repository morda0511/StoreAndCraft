# StoreAndCraft

**Your base runs with you.**  
Loot finds its chest. One key clears your pockets. Craft, upgrade, and build straight from storage. Feed kilns and smelters with **[E]** — or toggle **auto-fill with B**. Cooking stations can **auto-drop finished food with N** so chests pick it up. Tell each station which wood or ore it’s allowed to take. **Small / Medium / Large Storage Displays** with **Shift + left click** scale / layout. Build a **Feed Trough** so tames eat when hungry. Mark favorites with **F**, fill a stack with **Ctrl + Middle mouse**, find a stack with **Y**, sort with **R**. Rename chests with **Alt+E** (Ignore / Show on Display toggles). Carts and ships count too.

**Required on the dedicated / hosted server and every PC client** (same version). Valheim 1.0 · BepInExPack 5.4.2350+. Console players via crossplay cannot load the mod.

**Bug reports:** https://github.com/morda0511/StoreAndCraft/issues/1  

**Discord:** https://discord.gg/aVKVVmyzj

---

## Features

### Auto-store
- Ground items are pulled into nearby chests automatically if **any one player** is in `StoreRange` of the pile. You can walk off; a buddy standing there is enough.
- Toggle ground vacuum with **`AutoIntakeEnabled`** or chat **`/store disable`** / **`/store enable`** — dump and middle-click keep working either way.
- Default: a chest only accepts an item if that type is **already inside** (`MustHaveExisting`).
- Carts (`Vagon`) and **ships** count as containers too.
- **Player-built chests only** for dump / auto-store (crypts, house spawns, and other world chests are skipped).
- Ward / private area rules still apply.

### Dump & quick store
- **`.`** — dump allowed inventory stacks into matching nearby **player-built** chests. If the chest cannot take the whole stack, it fills what fits and leaves the rest.
- **Middle mouse** (inventory open) — store only the hovered item.
- **Ctrl + Middle mouse** (inventory open) — **Pull Stack**: fill the hovered stack from matching items in nearby chests.
- Hotbar can be skipped (`IgnoreHotbar`).
- Favorites are never dumped / hover-stored (see below).

### Craft & build from chests
- Recipes and hammer pieces count nearby chest contents.
- When you craft / upgrade / build, missing mats are **taken from chests directly** (not dumped into your backpack first — that used to fill free slots and cancel the craft).
- Craft / upgrade button enables when mats are in **inventory or chests**.
- Yellow tint on requirement text when part of the count comes from a chest.
- `LeaveOneItem` (default on): one item stays in each chest so auto-store can keep refilling. That leftover item cannot be spent (craft, build, plant, or station [E]). Turn the setting off if you want to use the last item.
- **C + place (hammer):** hold **C** while confirming a hammer build to **grab** that piece’s materials from nearby chests into your inventory (nothing is placed). Each grab-click can pull another full piece-cost set. Remap with `BuildGrabKey`. Shift stays free for vanilla no-snap.

### Epic Loot
- **Supports Epic Loot** — chest craft/build pull, Storage Displays (Dust / Essence / Reagent / Shard / Runestone), and the Enchanting Table (Sacrifice / Enchant / Upgrade / …) use nearby chest stock.
- Enchanting Table spend does not apply `LeaveOneItem` (a single Runestone in a chest still counts). Vanilla craft / smelter leave-one is unchanged.

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
- Look at a kiln, multi-input smelter, cooking station, or similar → **Alt+E**.
- Skills-style list with **[+]** / **[-]** toggles.
- Choose which wood / ore / food types may be auto-pulled (e.g. turn **Wood** OFF, leave **Core wood** ON).
- Same menu: **l1–l9** station link (3×3). Chests with matching **`[lN]`** are preferred; **l: none** = untagged chests only.
- Saved per station (ZDO). Manual use from inventory is not blocked for types you hold yourself; auto-select / chest pull respects the filter.

### Storage Display (Hammer)
- Build **Small**, **Medium**, or **Large Storage Display**.
- **Small:** assign with hotbar **1–8** while looking at it. **Shift + left click** cycles **Name + Amount** → **No name** → **Icon only** (sprite centers when alone). **Alt+E** still opens the Name / Amount toggle panel if you prefer.
- **Medium / Large:** **[E]** → type menu (**[+]** / **[-]**). Pick several types at once. **Shift + left click** cycles **Display Scale** (−10…+10) on **that board only** — icons and counts grow together; fewer items per row at higher scale. Hover shows e.g. `Display Scale : +3`.
- **Food**, **Ingredients**, and **Epic Loot** expand for subs.
- Up to **12 categories** per Large board (yellow message if you try more).
- Large: category bands with labels on the left and dense item rows; overflow shows **+** when there are more stacks than fit.
- Chests set to **Ignore** (without Show on Display) are not counted.
- Place several displays with the same selection next to each other → shared pages `(1/2)`, `(2/2)`, …
- **Alt+R** → per-display chest scan range (5–50 m).

### Feed Trough (Hammer)
- Build a **Feed Trough** (Storage tab) — narrow bed-shaped trough for animal food.
- Put carrots, cloudberries, etc. inside; nearby hungry tameables eat matching food automatically.
- Not used by dump, auto-store, craft pull, or Storage Displays.
- Config: `FeedTroughEnabled`, `FeedTroughRange`.

### Favorites
- Inventory open → hover item → **F**.
- Gold tint, **★** badge, tooltip line.
- Skipped by dump and hover-store.
- Local file: `StoreAndCraft.favorites.txt` (not server-synced).

### Rename / ignore / hide / link chests
- Look at a chest → **Alt+E** (or Shift+E alt-use) opens rename.
- **Ignore** and **Show on Display** use the new on/off toggle buttons (not typing prefixes by hand):
  - **Ignore** on → chest is skipped by dump / auto-store / craft / auto-fill (stored as **`[I]`**).
  - **Ignore** + **Show on Display** → still skipped by dump/store/craft, but **counted on Storage Displays** (stored as **`[H]`**).
- **l1–l9** link grid on the same panel → tag the chest for a station link (same colors on chest + station hover).
- Empty renamed chests keep their custom name.

### Search
- Inventory open → hover an item → press **Y**.
- Nearest chest that holds it **blinks 3 times** and gets a map ping.

### Sort
- Inventory open → **R**.
- Bag only: sorts your inventory. Open chest: sorts **that chest only** (not both).
- Favorites stay put. Sort **never moves items into the hotbar** (even when `IgnoreHotbar` is off).

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
| **Ctrl + Middle mouse** | **Pull Stack** — fill hovered stack from nearby chests |
| **Alt + E** | Rename looked-at chest (Ignore / Show on Display toggles + l1–l9) **or** station pull filter / Small Display options |
| **F** | Favorite / unfavorite hovered inventory item |
| **B** | Toggle auto-fill on the station you are looking at (inventory closed) |
| **N** | Toggle auto-drop on a cooking spit / iron station / stone oven (inventory closed) |
| **Y** | Hover an inventory item and press — nearest chest with that item blinks **3×** + map ping |
| **R** | Sort: bag open = inventory only; chest open = that chest only |
| **C + place (hammer)** | Grab build materials from chests (do not place). Remap: `BuildGrabKey` |
| **Alt + R** | Storage Display range (while looking at a display) |
| **Shift + Left click** | Storage Display: Medium/Large cycle scale (−10…+10, that board only); Small cycle Name+Amount / No name / Icon only |

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
| `/store enable` | Everyone | Turn **ground** auto-store on (host/admin also saves + syncs) |
| `/store disable` | Everyone | Turn **ground** auto-store off; dump / middle-click still work |
| `/store status` | Everyone | Show ground auto-store on/off |
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

**Resources:** Wood · Ore · Metals · Stone · Fuel · Hides · Parts · Crops & Seeds · Raw Food · Ingredients · Gems & Coins · Boss / Rare · Other materials  

**Epic Loot (expandable):** Dust · Essence · Reagent · Shard · Runestone  

**Other:** Food · Fish · Trophy · Ammo · Tools · Weapons · Armor · Utility · Misc  

---

## Config

**One file only** (no YAML):

`BepInEx/config/com.morda.storeandcraft.cfg`

| Setting | Meaning |
|---|---|
| `LockConfig` | `true` = server owns gameplay values; clients receive them on join |
| `ModEnabled` / `StoreEnabled` / `CraftEnabled` | Feature toggles (`StoreEnabled` = dump / middle-click / take-stack) |
| `AutoIntakeEnabled` | Ground items auto-store into matching chests (independent of middle-click) |
| `MustHaveExisting` | Chest only accepts item types it already holds |
| `LeaveOneItem` | Leave 1 in chest when pulling |
| `IgnoreHotbar` | Dump skips hotbar row |
| `AutoStackEnabled` | Compact stacks inside chests (off by default) |
| `PlayerDumpRange` | Dump / middle-click / pull-stack (m) |
| `StoreRange` | Auto-store ground items (m) |
| `StorageRange` | Take-stack / search / displays (m) |
| `CraftRange` | Craft / build / station `[E]` (m) |
| `AutoFillRange` | Auto-fill: player → station and player → chests (m). Default 20 |
| `IntakeInterval` | Seconds between ground-item scans |
| Hotkeys (`DumpKey`, `TakeStackKey`, `FavoriteKey`, `AutoFillKey`, `SortKey`, …) | Local only |

**Dedicated:** edit the `.cfg` on the **server**, save — it reloads and syncs. Client gameplay edits are ignored while `LockConfig` is on.

---

## Install

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Drop `StoreAndCraft.dll` into `BepInEx/plugins/StoreAndCraft/` (or install via Thunderstore / r2modman).
3. On dedicated servers: install the same version on the **server** and every client.
