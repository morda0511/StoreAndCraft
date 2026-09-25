# StoreAndCraft – Regression Checklist

After any non-trivial change, manually verify the **requested** feature **and** related rows below that share code (see `Docs/Risks.md`).

Tests are in-game. A green Release build is not enough.

---

## Storage / auto-store

| Feature | How to test |
|---------|-------------|
| Auto-store into matching chest | Put Wood in bag + Wood chest in range; trigger store; bag empties, chest gains |
| Store range | Chest just outside range is ignored |
| `[I]` ignored | Chest named with `[I]` never receives auto-store / craft pull |
| Travel / unload safety | Store, run far (unload), return; chests not empty; no “stored” with items still in bag |
| Open chest MP | Second player has chest open; first player auto-stores elsewhere — no kick / wipe |

---

## LeaveOneItem

| Feature | How to test |
|---------|-------------|
| Reserve last mat | One stackable mat in chest, LeaveOne on; craft that needs it should respect reserve (cannot spend last reserved unit for normal mats) |
| LeaveOne off | With setting false, last unit is spendable |
| Upgrade forge idols | At Potential/upgrader forge, single idol in chest remains usable |
| Epic Loot enchant | Single runestone in chest still enchantable when EL present |

---

## Crafting from storage

| Feature | How to test |
|---------|-------------|
| UI shows chest mats | At workbench, mats only in chest → requirement row shows enough |
| Craft succeeds | Craft item with mats only in chests; result appears; mats deducted from chest |
| Full bag craft | Bag full except no room for staging dumps; craft that adds new stack still works (StagingPull) |
| Arrow / existing stack | Craft arrows onto existing stack still works |
| Build piece | Place piece with mats in chests (Shift/build grab paths if enabled) |

---

## Stack / hotkeys / sort

| Feature | How to test |
|---------|-------------|
| Pull stack (if enabled) | Bound key pulls expected stack from storage |
| Sort hotbar-safe | Sort does not scramble hotbar gear unexpectedly |
| Hover keys match | Station/chest hover hints match actual key binds |

---

## Stations

| Feature | How to test |
|---------|-------------|
| Smelter fill | Ore + fuel in chest; smelter gains inputs over Tick |
| Kiln / windmill / etc. | Same for supported stations you touched |
| Oven / cook | Food/cookables fill; stone oven: each add switch hover still readable |
| Linked / filtered pull | Filter excludes an item → that item not fed |
| Modded cook (Volture etc.) | Valid cook item in chest feeds iron station when IDs mapped |

---

## Storage Display

| Feature | How to test |
|---------|-------------|
| Board shows items | Place display near chests; icons/counts appear |
| No flicker | Watch board for several seconds / on open — no flash-empty loop |
| Scale / Layout text | Large + Medium: change Scale and Classic/Compact — category + amount text stay readable (not microscopic); Large category names fully visible (not "Wea…") |
| Filters / type menu | Select types; board respects filter |
| No chest wipe | With displays present, walk away/back; chests intact |

---

## Feed trough

| Feature | How to test |
|---------|-------------|
| Animals eat | Trough + food in range; animals feed as designed for current build |

---

## Multiplayer / networking

| Feature | How to test |
|---------|-------------|
| Version gate | Mismatched mod version behaves as designed (block/warn) |
| Config sync | Host changes synced toggle; client observes after sync |
| Remote transfer | Client without ownership: store/craft still moves items correctly via RPC |
| Queue budget | Many fills at once do not freeze; eventually complete |

---

## Remote automation

Removed in 1.3.43 — no tests required. Do not re-add without a new explicit feature request.

---

## Config / packaging

| Feature | How to test |
|---------|-------------|
| Protocol bump | After wire change, old client + new host does not corrupt settings |
| Bundle flags | Pack without `sac_displays` when `IncludeDisplayBundle=false`; game runs without missing-bundle hard crash |

---

## UI

| Feature | How to test |
|---------|-------------|
| Filter menus open/close | Station filter + display type menus usable |
| Assets load | No missing pink UI; wood panels visible |
