# Changelog

## 1.3.5-pre — UI improvements (pre-release)

- **UI:** Custom rename panel — Ignore / Show-on-display toggles + link grid (embedded PNG assets)
- **UI:** Station pull-filter menu uses the same link grid chrome; Done on the footer button
- **Hover:** Chest status lines (Ignore / Display ✓ / Link: N); station hover shows Link: N
- **Logic:** Ignore and link are mutually exclusive (Ignore clears link; links dim while Ignore is on)
- **Fix:** Dump after raids — refresh chest inventory before deposit so “Stored” / ping match reality

## 1.3.4-test — Pre-release

- **Feature (test):** Station links **l1–l9** (Alt+E, 3×3 toggles on kiln/smelter/cook). Chests tagged `[lN]` (legacy `[linkN]` still works). **l: none** = untagged chests only. Bright per-link colors on chest + station hover (not red/orange). Craft / build / Epic Loot unchanged.

## 1.3.3 — Hotfix

- **Epic Loot:** Enchanting Table (Sacrifice / Enchant / Upgrade / …) now sees and spends nearby chest items via Epic Loot’s inventory-provider API (custom UI does not use vanilla craft hooks)
- **Epic Loot:** enchant spend ignores `LeaveOneItem` so a single Runestone in a chest still counts
- **Displays:** Epic Loot type menu adds **Runestone** (incl. EtchedRunestone); runestones no longer sit under Boss / Rare

## 1.3.2 — Hotfix

- **Change:** build grab is **C + place** with the hammer (was Shift+place; Shift stays free for no-snap). Remap: `BuildGrabKey`
- **Feature:** chest prefix **`[H]`** — hide from dump / store / craft / auto-fill, but still count on Storage Displays (and search). `[I]` stays fully ignored
- **Fix:** Alt+R display-range menu header shows **Display range** instead of Skills
- **Change:** quieter BepInEx startup log (only the loaded line)

## 1.3.1 — Hotfix

- **Removed:** experimental remote dump (buddy near base) — dump is local-range only again
- **Perf:** Large Storage Displays — event-driven chest watch, single-pass ranking, less idle work
- **Perf:** Auto-fill — one chest snapshot per pulse; no scan when all stations are off
- **Perf:** Nearby chest index rescans less often while idle / walking
- **Fix:** Surtling core shows under Boss / Rare (not Ore)
- **Fix:** Suppress vanilla AOE “default spiky” warning spam that could hitch FPS

## 1.3.0 — Release

- **Storage Displays:** Small / Medium / Large — Small shows icon, count, and item name; Large dense category bands (up to 12); Food / Ingredients / Epic Loot expandable in the type menu
- **Shift+build grab:** hold Shift while placing to pull piece costs from nearby chests (no place); restock each click
- **Pull Stack:** Ctrl + Middle mouse fills the hovered inventory stack from nearby chests
- **Epic Loot:** supported (chest craft/build pull + displays)
- **Remote dump (Experimental):** dump can finish via a buddy near matching base chests (`RemoteDumpEnabled`)
- **Sort:** never packs items into the hotbar
- **Alt+R:** per-display chest scan range
- Fixes for Large label layout and Shift+build mats vanishing after grab

## 1.2.8 — Pre-release hotfix

- **Fix:** Shift+build grab — mats no longer vanish (cancel at `TryPlacePiece` so `ConsumeResources` does not eat the bag right after withdraw)
- **Fix:** Shift+build can pull another full piece-cost set each click, even if you already have those mats in the bag

## 1.2.7 — Pre-release hotfix

- **Fix:** Large display with many categories was unreadable (tiny icons / empty board) — bigger fonts, correct grid scale, label column + indented wrap
- **Fix:** Shift+build grab — while Shift is held, placing is blocked; mats are pulled from nearby chests into the bag

## 1.2.6 — Pre-release hotfix

- **Fix:** Harmony crash on load — `PlacePiece` is void; Shift+build grab no longer uses `__result`

## 1.2.5 — Pre-release Test

- **Large displays:** category bands (1–12) fill the board height; labels left; items wrap indented under icons (not under the name); max 12 categories with yellow message
- **Filters:** Food vs Ingredients exclusive; Hides/Parts soft-match (bear, chitin, ectoplasm); Epic Loot mats → Other materials
- **Epic Loot:** soft compatibility — displays categorize EL mats; enchanting/craft chest-pull uses the same paths as other stations when requirements are checked
- **Shift+build grab:** hold Shift while placing to pull piece costs from chests into your bag (no build)
- **Fix:** inventory sort never moves items into the hotbar row
- Pre-release — server + all clients need the **same** build

## 1.2.4 — Pre-release hotfix

- **Fix:** Storage Displays place again (1.2.3 trigger-collider / piece tweaks ate materials and spawned nothing)
- **Keys:** rename / station pull filter default back to **Alt+E** — remap in config if it clashes with cooking
- Displays keep solid colliders (placement-safe); depth softening only where a BoxCollider exists

## 1.2.3 — Pre-release Test

- **Large displays:** flowing category sections (any number of categories); empty selected categories still show with **0**
- **Hitbox:** displays are thin triggers (walk-through), need a workbench, shorter distant sync
- **Filters:** Food = all consumables; **Other materials** catch-all (Epic Loot reagents etc.); shorter Food expand list
- **Keys:** chest rename / station pull filter default **Alt+B** (was Alt+E; avoids cooking + F skills)
- Pre-release for testing — server + all clients need the **same** build

## 1.2.2 — Pre-release Test

- **Feature:** Small / Medium / Large Storage Displays (hotbar item on Small, category grid on Medium, 3-column Large)
- **Feature:** Food / Ingredients expand for single items; Medium sorts per category; category titles on the board
- **Feature:** Per-display **DisplayRange** via **Alt+R** (5–50 m, any ward access, no admin)
- **Feature:** Cooking spit / oven chest-pull filter (same hotkey as kiln/smelter)
- **Fix:** Auto-store when chest inventory was stale until opened (budgeted force-load on miss)
- **Fix:** Display filter labels and menu layout polish
- Pre-release for testing — server + all clients need the **same** build

## 1.2.1 — Cooking auto-drop + stone oven auto-fill

- **Feature:** look at a cooking spit, iron cooking station, or stone oven (inventory **closed**) and press **N** to toggle **auto-drop**. Finished food falls off as a ground drop so auto-store can put it in a chest. Per-station, saved on the piece. Hotkey `AutoDropKey` (default **N**)
- **Fix:** stone oven **B** auto-fill — hover on the oven door now shows the toggle; food is added via the station RPC (no longer blocked when the under-fire is out); free slots are topped up (not only when the oven is fully empty)
- **Fix:** workbench craft UI no longer looks like it spent mats twice when another player is nearby (PendingChestDebit cleared once the chest ZDO syncs). Real chest take was already 1×
- Server + all clients need **1.2.1**

## 1.2.0 — Auto-store if any player is in range

- **Fix:** ground loot stores when **one** player is in `StoreRange` of the pile, not only the person who owns it. Walk off a kiln / smelter / blast furnace and a buddy can stay; your coal still goes in the chest
- Server + all clients need **1.2.0**

## 1.1.19 — Auto-fill from chests, no bag spam

- **Fix:** auto-fill no longer pulls coal / ore / fuel into your inventory 1 at a time (blast furnace “out of coal” flash, then a single piece appearing). Chests are consumed in place and the station is filled directly. Inventory is still used first if you already hold the item

## 1.1.18 — Buddy station store + oven / fermenter auto-fill

- **Fix:** using a buddy’s kiln, smelter, blast furnace, or any other station that spawns ground loot no longer blocks auto-store until you walk back to the chests. Output you own is sent into the chest instead of fighting over drop ownership
- **Auto-fill:** stone oven / cooking stations and fermenters now have **B** auto-fill. Kiln / smelter / blast furnace also show it on the take-output hover

## 1.1.17 — Partial dump / hover-store

- **Fix:** dump and middle-click store no longer require the whole stack to fit. Wood 28 into a chest at 26/50 now fills to 50 and leaves the rest in your bag (was “No matching chest”)

## 1.1.16 — Kiln / smelter / torch auto-fill

- **Auto-fill:** look at a kiln, smelter, or torch / fire (inventory **closed**) and press **B** to toggle per station. Filter still applies. Inventory first, then chests. A buddy in `AutoFillRange` can fill it; you do not have to stand there yourself
- Fills when that slot hits **0**, dumps a full load (smelter max, e.g. 10), then waits until empty again. No chest scans while it is running. If chests are empty too, the next chest check is after **20 seconds**
- **Hotkey:** `AutoFillKey` default **B** (local). Inventory-open **F** is still favorites. Manual **[E]** is unchanged
- **Range:** `AutoFillRange` (default 20m) is player → station and player → chests for auto-fill only. Independent from `CraftRange` / `[E]`
- Chat: `/autofillrange 40` or `/sac autofill 40` (host / admin). `/sac status` shows it. Edit the `.cfg` and save: ranges apply **without a server restart**
- **Fix:** listen-server clients no longer craft many items while the chest only lost materials for one
- **Fix:** start crash `Undefined target method` on auto-fill message silence
- Server + all clients need **1.1.16** (config protocol 5)

## 1.1.15 — Auto-fill Harmony crash

- **Fix:** game start no longer errors `Undefined target method` on `AutoFillSilencePatch` (Message lives on Character, not Humanoid). Auto-fill load + silent fill toasts work again

## 1.1.14 — Auto-fill B, fill when empty, no spam

- **Auto-fill hotkey is B** (inventory closed, looking at kiln / smelter / torch). F stays favorites. Valheim already uses F
- **Fill when empty:** when fuel or ore hits 0, dump a full load (e.g. 10 into a smelter) in one go, then wait until it is empty again. Chests are not scanned while the station is still running. If the station is empty and chests have nothing, the next chest check is after **20 seconds**
- **Fix:** no yellow “added” toasts and no repeating “it’s full” from auto-fill. Toggle on/off still shows a message. Manual [E] is unchanged

## 1.1.13 — Station auto-fill + client craft consume

- **Feature:** look at a kiln, smelter, or torch / fire (inventory closed) and press **F** to toggle auto-fill. It uses inventory first, then chests, and still respects that station’s pull filter. Inventory-open **F** is still favorites. `[E]` refill is unchanged
- **Fix (listen server / friends host):** clients could craft many items while the chest only lost materials for one. Consume RPCs now reserve those mats locally so the next craft cannot run on a stale chest count
- **Note:** auto-store range is `StoreRange` in `com.morda.storeandcraft.cfg` (default 10m). YAML rules files are not used since 1.1.4

## 1.1.12 — Auto-store while a buddy is in range

- **Fix:** ground pickup only ran for the chest owner. Kiln coal / chicken eggs stayed on the floor when you walked away and a buddy was there. Server now intakes around every player, and chest owners retry after taking drop ownership
- **Fix:** torch / fireplace `[E]` no longer flashes “out of resin” when the fuel is coming from a chest. Inventory is still used first

## 1.1.11 — Smelter pull world level

- **Fix:** coal / iron scrap pulled from chests into the bag ([E] on smelter) could appear as unusable “false” items (worldLevel 0 vs current world). Player grants now keep at least the current world level

## 1.1.10 — Auto-store log + LeaveOneItem

- **Fix:** auto-store no longer spams `InvalidOperationException` (collection modified) in the BepInEx log — ground-item list is snapshotted before storing
- **Fix:** that crash aborted the rest of the pickup pass, so auto-store looked broken (works when a buddy walks over, then dies), especially around a busy smelter
- **Fix:** dedicated auto-store could see zero chests (nearby index required a local player and wiped the cache)
- **Fix:** `LeaveOneItem` now applies to craft / build / plant counts, not only chest pulls. The leftover sapling / ore cannot be spent (smelter [E] already behaved this way)

## 1.1.9 — Sort + craft counts

- **Sort:** hotkey **R** (config `SortKey`) — inventory only when bag is open; open chest sorts that chest only (not both)
- **Sort:** favorited items stay in place (also equipped + hotbar when IgnoreHotbar is on)
- **UI:** craft, upgrade, and build hammer show **have/need** (inventory + nearby chests), e.g. `12/10`; soft yellow when chests contribute

## 1.1.8 — Client consume + idle lag

- **Fix (multiplayer):** clients no longer craft/build for free from nearby chests — owner loads chest inventory before consuming (host was fine; clients were not charged)
- **Fix (FPS):** standing near many chests no longer causes ~1s lag spikes — inventory Load removed from the idle rescan loop (CraftEnabled off still spiked before)
- Chest inventory Load is throttled and only runs when dump/craft/search actually needs counts
- Idle nearby-chest rescan slowed down when standing still; OverlapSphere remains fallback only

## 1.1.7 — Chest discovery fix

- **Fix:** dump, craft-from-chests, and station pull work again after 1.1.6 broke chest discovery (empty nearby index)
- **Fix:** removed invalid `Container.OnDestroy` Harmony patch that crashed BepInEx on load
- Inventories refresh on rescan; OverlapSphere only as fallback if registration finds nothing
- Keeps 1.1.6 FPS improvements (no per-count inventory refresh spam in craft UI)

## 1.1.6 — FPS Hotfix

- **FPS:** opening inventory / stations near chests no longer tanks frame rate; session no longer degrades over time
- **FPS:** pickup / drop lag with `CraftEnabled` reduced (chest index no longer rebuilt with huge CraftRange physics scans)
- Chest index uses registered containers + frame count cache; dead containers pruned; chest-count flag reset each frame
- Dump / auto-store / search / Storage Displays share the index (no OverlapSphere rebuilds that wiped craft-range caches)

## 1.1.5

- **Upgrade button:** chest mats now enable craft/upgrade on **all** stations (workbench, forge, black forge, galdr, artisan, …). 1.1.4 fixed the workbench UI path; forge and other upgraders needed a matching `HaveRequirementItems` check
- **Config commands:** range changes (`/dumprange`, `/sac …`) apply on the client immediately and get a server confirmation (dump range was updating on the server only before)
- **World chests:** dump / auto-store / middle-click no longer fill crypts, house spawns, or other non-player chests (carts and ships still allowed)
- **Cooking station:** taking finished food with **[E]** no longer pulls an extra raw meat from chests (boar was first in the cook list)

## 1.1.4

- **Craft fix:** materials for craft / upgrade / build are consumed from chests **in place** instead of being moved into the backpack first. Staging filled free inventory slots (worse with EquipmentQuickSlots), so the craft result could not be added; mats appeared, craft “fizzled”; stacking onto an existing arrow stack still worked
- **Kiln / smelter pull filter:** Alt+E on multi-input stations to choose which inputs may be pulled (per station, synced)
- **Storage Display:** multi-select filters (same skills-style UI); boards with the same filter set cluster together
- **Search:** inventory open, hover item, press **Y** — nearest chest blinks 3× + map ping
- **Favorites:** hover an inventory item + **F** to protect it from dump / hover-store (local)
- Removed **Alt+P** (pause auto-store) and **Alt+O** (toggle chest pull); store/craft stay on while the mod is enabled
- Config: **YAML removed**; ranges live in `com.morda.storeandcraft.cfg`; `LockConfig` server sync; carts & ships count as storage
- Discord for support: https://discord.gg/aVKVVmyzj

## 1.1.3

- Craft-from-chests: pull real item stacks (quality / world level / crafter) instead of spawning blank prefabs — fixes materials stuck in inventory that could not finish crafting, be re-stored, or be used until traded to another player
- Config: create/save `com.morda.storeandcraft.cfg` reliably; log full config + rules paths on startup; hot-reload watches the real GUID `.cfg` (not only `StoreAndCraft.*`)
- Config: dump/take/display range no longer ignores `StoreAndCraft.rules.yml` (was capped by StorageRange alone)
- Docs: correct config filename for dedicated servers

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
