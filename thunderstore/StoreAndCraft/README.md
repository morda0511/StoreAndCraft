# StoreAndCraft

**Your base runs with you.**  
Loot finds its chest. One key clears your pockets. Craft, upgrade, and build straight from storage. Feed kilns and smelters with **[E]**, or toggle **auto-fill with B**. Finished bars / food / honey can **auto-store with N** into matching chests. Tell each station which wood or ore it is allowed to take. **F11** activity log. **Small / Medium / Large Storage Displays** with **Shift + left click** scale and **Shift + right click** Classic/Compact layout. Build a **Feed Trough** so tames eat when hungry. Mark favorites with **F**, fill a stack with **Ctrl + Middle mouse**, find a stack with **Y**, sort with **R**. **Alt+E** opens Settings for chests and stations. Carts and ships count too.

**Required on the dedicated / hosted server and every PC client** (same version). Valheim 1.0 · BepInExPack 5.4.2350+. Console players via crossplay cannot load the mod.

**Bug reports:** https://github.com/morda0511/StoreAndCraft/issues/1  

**Discord:** https://discord.gg/aVKVVmyzj

---

## Features

### Auto-store
- Ground items are pulled into nearby chests automatically if **any one player** is in range of the pile. You can walk off; a buddy standing there is enough.
- Toggle ground vacuum with chat **`/store disable`** / **`/store enable`**. Dump and middle-click keep working either way.
- Default: a chest only accepts an item if that type is **already inside**.
- Carts and **ships** count as containers too.
- **Player-built chests only** for dump / auto-store (crypts, house spawns, and other world chests are skipped).
- Ward / private area rules still apply.

### Dump & quick store
- **`.`**: dump allowed inventory stacks into matching nearby **player-built** chests. If the chest cannot take the whole stack, it fills what fits and leaves the rest.
- **Middle mouse** (inventory open): store only the hovered item.
- **Ctrl + Middle mouse** (inventory open): **Pull Stack**: fill the hovered stack from matching items in nearby chests.
- Hotbar can be skipped. Extra inventory rows and quick slots are never dumped or sorted.
- Favorites are never dumped / hover-stored.

### Craft & build from chests
- Recipes and hammer pieces count nearby chest contents.
- When you craft / upgrade / build, missing mats are **taken from chests directly**.
- Craft / upgrade button enables when mats are in **inventory or chests**.
- Yellow tint on requirement text when part of the count comes from a chest.
- One item stays in each chest by default so auto-store can keep refilling. That leftover cannot be spent.
- **Forge of Potential**: upgrades count and spend idols in nearby chests. A single idol in a chest is enough.
- **C + place (hammer):** hold **C** while confirming a hammer build to **grab** that piece's materials from nearby chests into your inventory (nothing is placed).

### Epic Loot
- **Supports Epic Loot**: chest craft/build pull, Storage Displays (Dust / Essence / Reagent / Shard / Runestone), and the Enchanting Table use nearby chest stock.
- Enchanting Table: a single Runestone in a chest still counts.

### Station refill `[E]`
Works on smelters, charcoal kilns, cooking stations, fires / torches, fermenters, turrets, etc.:
- Pulls fuel / ore / food from nearby chests when you don't have it.
- **Inventory first** on manual **[E]**: if you already hold a valid item, that is used before chest contents.

### Kiln / smelter / cooking auto-fill
- Look at a kiln, smelter, blast furnace, cooking spit, iron cooking station, stone oven, fermenter, or torch / fire with the inventory **closed** → **B** toggles auto-fill for that station.
- When fuel or ore hits **0**, it sends a full load from nearby chests. Auto-fill **never takes from your bag**; only chests (same link, or both unlinked). Ignored / hide-only chests are skipped. Same for Shift+[E] fill-to-max.
- Then it waits until that slot is empty again. If chests are empty too, the next chest check is after **20 seconds**.
- Cooking / ovens top up **free slots**. Stone oven door hover shows the toggle; food is added even if the under-fire is out (baking still needs fire to cook).
- Filter still applies (Wood OFF stays OFF).
- Manual **[E]** refill is unchanged.

### Station auto-store (N)
- Look at a kiln, smelter, blast furnace, cooking spit, iron cooking station, stone oven, or **beehive** with the inventory **closed** → **N** toggles auto-store. Independent from **B**.
- Finished bars / food / honey go into nearby chests: **matching link first**, then an **unlinked** chest that already holds the item; never a chest with a **different** link. Otherwise the item drops on the ground.
- Pair with **B** auto-fill for hands-off refining / cooking.

### Station Settings (Alt+E)
- Look at a kiln, multi-input smelter, cooking station, or similar → **Alt+E** → **Settings**.
- Choose which wood / ore / food types may be pulled (also blocked from your bag on that station).
- Same menu: **l1-l9** station link. Fill pulls only matching linked chests (or untagged when the station has no link).

### Storage Display (Hammer)
- Build **Small**, **Medium**, or **Large Storage Display**.
- **Small:** assign with hotbar **1-8** while looking at it. **Shift + left click** cycles **Name + Amount** → **No name** → **Icon only**.
- **Medium / Large:** **[E]** → type menu. Pick several types at once. **Shift + left click** cycles **Display Scale**. **Shift + right click** toggles **Layout** **Classic** ↔ **Compact**.
- **Food**, **Ingredients**, and **Epic Loot** expand for subs.
- Up to **12 categories** per Large board.
- Chests set to **Ignore** (without Show on Display) are not counted.
- Place several displays with the same selection next to each other → shared pages `(1/2)`, `(2/2)`, …
- **Alt+R** → per-display chest scan range.

### Feed Trough (Hammer)
- Build a **Feed Trough** (Storage tab).
- Put carrots, mushrooms, berries, etc. inside. Hungry tames **walk to the trough** and eat, like drops on the ground. A small sparkle shows when food is inside.
- Not used by dump, auto-store, craft pull, or Storage Displays.

### Favorites
- Inventory open → hover item → **F**.
- Star badge on the item.
- Skipped by dump and hover-store.

### Rename / ignore / hide / link chests
- Look at a chest → **Alt+E** opens **Settings**.
- **Ignore** and **Show on Display** use on/off toggles:
  - **Ignore** on → skipped by dump / auto-store / craft / auto-fill.
  - **Ignore** + **Show on Display** → still skipped by dump/store/craft, but **counted on Storage Displays**.
- **l1-l9** link grid on the same panel → tag the chest for a station link.
- Empty renamed chests keep their custom name.

### Activity log
- **F11** toggles a short log under the TopLeft status text. Shows recent chest ↔ station moves while you play.

### Search
- Inventory open → hover an item → press **Y**.
- Nearest chest that holds it **blinks 3 times** and gets a map ping.

### Sort
- Inventory open → **R**.
- Bag only: sorts your inventory. Open chest: sorts **that chest only**.
- Favorites stay put. Sort never moves items into the hotbar.

### Multiplayer
- Install on **server + all clients**, same version.
- Server config sync.
- Safe chest transfers (no ownership steal while someone has a chest open).

---

## Keys (defaults)

| Key | Action |
|---|---|
| **.** | Dump inventory into matching nearby chests |
| **Middle mouse** | Store hovered inventory item (inventory open) |
| **Ctrl + Middle mouse** | **Pull Stack**: fill hovered stack from nearby chests |
| **Alt + E** | **Settings** for chest or station |
| **F** | Favorite / unfavorite hovered inventory item |
| **B** | Toggle auto-fill on the looked-at station (inventory closed) |
| **N** | Toggle auto-store on kiln / smelter / cooking / beehive (inventory closed) |
| **F11** | Toggle activity log |
| **Y** | Find nearest chest with the hovered item (blink + map ping) |
| **R** | Sort bag or open chest |
| **C + place (hammer)** | Grab build materials from chests (do not place) |
| **Alt + R** | Storage Display range |
| **Shift + Left click** | Storage Display: scale (Medium/Large) or Small view mode |
| **Shift + Right click** | Storage Display Medium/Large: Classic ↔ Compact layout |

All hotkeys are configurable in the `.cfg` and stay **local**.

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
| `/store enable` | Everyone | Turn **ground** auto-store on |
| `/store disable` | Everyone | Turn **ground** auto-store off; dump / middle-click still work |
| `/store status` | Everyone | Show ground auto-store on/off |
| `/sac status` | Everyone | Show current ranges |
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

Ranges are saved to `com.morda.storeandcraft.cfg` and synced to clients when LockConfig is on. Valid range: **0-500**. Edit the `.cfg` on the host / server and save, or use the commands: **no restart**.

</details>

---

## Storage Display types

**Resources:** Wood · Ore · Metals · Stone · Fuel · Hides · Parts · Crops & Seeds · Raw Food · Ingredients · Gems & Coins · Boss / Rare · Other materials  

**Epic Loot (expandable):** Dust · Essence · Reagent · Shard · Runestone  

**Other:** Food · Fish · Trophy · Ammo · Tools · Weapons · Armor · Utility · Misc  

---

## Config

**One file only:**

`BepInEx/config/com.morda.storeandcraft.cfg`

| Setting | Meaning |
|---|---|
| `LockConfig` | Server owns gameplay values; clients receive them on join |
| `ModEnabled` / `StoreEnabled` / `CraftEnabled` | Feature toggles |
| `AutoIntakeEnabled` | Ground items auto-store into matching chests |
| `MustHaveExisting` | Chest only accepts item types it already holds |
| `LeaveOneItem` | Leave 1 in chest when pulling |
| `IgnoreHotbar` | Dump skips hotbar row |
| `AutoStackEnabled` | Compact stacks inside chests |
| `PlayerDumpRange` | Dump / middle-click / pull-stack (m) |
| `StoreRange` | Auto-store ground items (m) |
| `StorageRange` | Take-stack / search / displays (m) |
| `CraftRange` | Craft / build / station `[E]` (m) |
| `AutoFillRange` | Auto-fill range (m) |
| `IntakeInterval` | Seconds between ground-item scans |
| Hotkeys | Local only |

**Dedicated:** edit the `.cfg` on the **server**, save. It reloads and syncs.

---

## Install

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Drop `StoreAndCraft.dll` into `BepInEx/plugins/StoreAndCraft/` (or install via Thunderstore / r2modman).
3. On dedicated servers: install the same version on the **server** and every client.