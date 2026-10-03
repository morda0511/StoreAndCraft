# StoreAndCraft

**Your base runs with you.**
Loot finds its chest. One key clears your pockets. Craft, upgrade and build straight from storage. Stations refill themselves from chests and put their output back. Storage displays, feed trough, scarecrow and more.

**Required on the server and on every client (same version).** Valheim 1.0 · BepInExPack 5.4.2350+. Console players cannot load the mod.

**Bug reports:** https://github.com/morda0511/StoreAndCraft/issues/1
**Discord:** https://discord.gg/aVKVVmyzj

---

## Features

### Store
- **Auto-store:** ground items go into matching chests (one player in range is enough)
- Chest takes an item only if the type is already inside (switch in F10)
- Carts and ships count as chests. Player-built chests only. Ward rules apply
- `/store disable` / `/store enable` turns ground pickup off / on
- **Dump** (**.**): inventory into matching chests, hotbar and favorites stay
- **Middle mouse** (inventory open): store the hovered item
- **Ctrl + middle mouse**: fill the hovered stack from chests
- **Sort** and **Stack** buttons on inventory and chest panels
- **Favorites** (**F**): never dumped or stored
- **Search** (**Y**): nearest chest with the item blinks, map ping

### Craft and build from chests
- Recipes, upgrades and hammer pieces use chest contents
- Missing materials are taken from chests directly
- Yellow requirement text = part comes from a chest
- 1 item stays in each chest (switch in F10)
- **C + place**: grab a piece's materials from chests, nothing is placed
- Epic Loot: craft, enchanting table, Forge of Potential use chest stock

### Stations
- **[E]** refill: fuel, ore, food from chests (inventory first)
- **B** auto-fill: refills when empty, never from your bag
- **N** auto-store: finished bars, food, honey, mead go into chests
- Works on kiln, smelter, blast furnace, cooking stations, oven, fermenter, turrets, fires, torches; **N** also on beehive and sap extractor
- **Alt+E** settings: allowed items, link **l1-l9**
- **Links:** station fills from, and outputs to, chests with the same link
- **Torches** (F10): auto-fill for new torches, World Override for all torches
- **Station capacities** (F10, admin): kiln, smelter, blast furnace, eitr refinery, beehive
- **Fermenter:** mead filter, add the same base in the first 60 s (batch up to 5)
- Red label above a linked station when its chests ran out
- **Catch-up while away** (off by default): stations finish the time their area was unloaded

### Storage displays (hammer, Storage tab)
- Carved boards: small, medium, large, upright and wide. Vanilla sign displays too
- **E:** pick categories (small: one category or up to 4 items), search box
- **Right click** an item on the display (or in that menu): camera flies to the chest that holds it, the chest blinks (again = next chest)
- **Shift + right click:** switch layout
- **Shift + left click:** display scale
- **Alt+E** on small: name / amount. **Alt+R:** scan range per display
- Displays with the same selection next to each other share pages
- Chests set to Ignore are not counted

### Feed trough (hammer)
- Food filter, links, auto-fill (**Alt+E**)
- Hungry tames walk to it and eat

### Scarecrow (hammer)
- Plants a crop grid, harvests when all is ripe, replants
- **Shift / Ctrl + wheel:** width / length. **E:** crop, harvest / plant, auto-fill, links
- Needs cultivated ground. Unlocks with the Cultivator recipe

### Armor stand
- **E** swaps your armor with the stand's
- **Alt+E**: pick a preset item from your bag per weapon / shield / tool slot. **E** swaps it with the stand's piece, back and forth
- Without a preset, **E** puts the stand's weapon into your bag
- Switch in F10

### Chest settings (**Alt+E**)
- **Ignore:** skipped by everything
- **Show on display:** ignored chest still counts on displays
- **Manual fill:** nothing goes in automatically, stations can still take from it
- **Only stations:** no dump, no ground pickup, stations and links still fill it
- **l1-l9:** link for stations
- Settings are saved on the chest, not in its name

### F10 mod manager
- **F10** opens the list of all your mods. Click one to change its settings (switches, sliders, numbers, lists, hotkeys), **Save**, back arrow = list
- StoreAndCraft opens its own panel (below)
- Activity log, all ranges, all on/off switches, station capacities
- Hotkey of each feature next to its slider: click, press new key (Esc cancel, Backspace unbind)
- Servers: only admins from adminlist.txt can save

---

## Keys (defaults, change in F10)

| Key | Action |
|---|---|
| **.** | Dump |
| **Middle mouse** | Store hovered item |
| **Ctrl + middle mouse** | Fill hovered stack |
| **F** | Favorite |
| **Y** | Find item in chests |
| **B** | Auto-fill on the station you look at |
| **N** | Auto-store on the station you look at |
| **Alt + E** | Settings: chest, station, display, trough |
| **Alt + R** | Display range |
| **C + place** | Grab materials |
| **F10** | Panel |
| **Shift + wheel / Ctrl + wheel** | Scarecrow width / length |

---

## Commands

Chat (`/...`) or F5 console. Ranges: host / server admin only.

| Command | What |
|---|---|
| `/help store` | Help |
| `/store enable` / `disable` / `status` | Ground auto-store |
| `/sac status` | Current ranges |
| `/dumprange` `/storerange` `/storagerange` `/craftrange` `/autofillrange` `/autofillchestrange` `<m>` | Set a range (0-1000) |
| `/torchautofill` `/torchautofilloverride` `on` / `off` | Torch auto-fill |

Same things in F10, no restart needed.

---

## Config

`BepInEx/config/com.morda.storeandcraft.cfg` (server owns gameplay values when `LockConfig` is on)

| Setting | Meaning |
|---|---|
| `LockConfig` | Server values replace client values |
| `ModEnabled` / `StoreEnabled` / `CraftEnabled` | Feature switches |
| `AutoIntakeEnabled` / `MustHaveExisting` / `LeaveOneItem` / `IgnoreHotbar` / `AutoStackEnabled` | Store behavior |
| `PlayerDumpRange` / `StoreRange` / `StorageRange` / `CraftRange` | Ranges (m) |
| `AutoFillRange` / `AutoFillChestRange` | Station ranges (m) |
| `TorchAutoFillDefault` / `TorchAutoFillOverride` | Torches |
| `ArmorStandSwap` | Armor stand swap |
| `CatchUpWhileAway` | Catch-up |
| `FermenterBatch` and station capacities | Station sizes |
| `ShowReleaseNews` | Hugin news |
| Hotkeys | Local only |

---

## Install

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
2. Put `StoreAndCraft.dll` in `BepInEx/plugins/StoreAndCraft/` (or use Thunderstore / r2modman)
3. Servers: same version on the server and every client
