# StoreAndCraft – Existing Patterns

Reuse these. Do not invent a parallel system for the same problem.

---

## 1. Nearby chest scan + cache

**Where:** `World/NearbyIndex.cs`

**How:** Periodic `Tick` builds `Current` list; helpers count items with LeaveOne; `EnsureInventory` hydrates views.

**Used by:** AutoStore, StagingPull, RequirementBridge, StationFeed/AutoFill, BuildGrab, displays, EpicLootBridge, RemoteAutomation (if enabled).

**Why:** One scan avoids N× FindObjects; consistent ignore/range rules.

---

## 2. Ignore and name tags

**Where:** `World/ChestNames.cs`, `ContainerFilter.cs`

**How:** Parse chest hover/name for `[I]` (and related tags); `IsUsable` / `IsPlayerBuiltStorage` gates.

**Used by:** Every transfer and count path that must skip ignored storage.

**Why:** Player-controlled exclusion without per-feature config lists.

---

## 3. LeaveOne for stackable mats

**Where:** `ItemIds.ShouldLeaveOne`, `RequirementBridge.LeaveOneInChests`, applied in `NearbyIndex` counts and `StagingPull` consume.

**How:** Config flag + per-item exceptions + forge upgrader exception.

**Used by:** Craft UI counts, craft spend, autofill spendable maps, build grab.

**Why:** Keeps auto-store routing targets; exceptions prevent unique items from being stuck.

---

## 4. Ownership-safe transfer

**Where:** `Network/TransferService.cs`

**How:** Queue + budget; local execute when owner; RPC otherwise; **no ownership steal** on empty/stale views; comment-documented wipe/kick failure modes.

**Used by:** Store, craft consume, station feed fills, drop store RPCs.

**Why:** MP safety and chest integrity.

**Do not:** Add a second “just ClaimOwnership and Save” helper elsewhere.

---

## 5. Staging pull (craft without backpack dump)

**Where:** `Craft/StagingPull.cs` + `PlayerCraftPatches` / `InventoryCountPatches`

**How:** Count chests for UI; on consume, only pull deficit from chests; `InventoryCountPatches.Skip` during consume to avoid recursion.

**Used by:** Vanilla craft/build when CraftEnabled.

**Why:** Fixes classic full-inventory craft fail after mat teleport into bag.

---

## 6. Requirement bridge for “have enough”

**Where:** `Craft/RequirementBridge.cs`

**How:** Single API for nearby counts used by patches and recipe checks; central LeaveOne policy.

**Used by:** UI requirement setup, have-requirements patches, plant/build where wired.

---

## 7. Station feed tick

**Where:** `Craft/StationFeed.cs`, `StationAutoFill.cs`

**How:** Plugin Update → Tick → resolve station type → linked/nearby chests → transfer valid inputs.

**Used by:** All supported processing stations.

**Why:** One scheduler; filters/links apply consistently.

---

## 8. Soft reflection for optional mods

**Where:** `Craft/EpicLootBridge.cs`

**How:** Detect types/methods at runtime; no hard reference required to compile against Epic Loot APIs beyond what the project already does.

**Why:** Soft compat without forcing dependency.

---

## 9. Chest wipe guard

**Where:** `Patches/ChestLoadGuardPatch.cs`

**How:** Block empty Load over non-empty inventory; block empty Save when ZDO still has items / guard set; `AllowEmptySave` counter for intentional empties.

**Used by:** Global — any EnsureInventory / display / transfer race.

**Why:** Storage Display and travel unload races wiped bases historically.

---

## 10. Config wire protocol

**Where:** `Configuration/ModConfig.cs` + `Network/ConfigSync*.cs`

**How:** Explicit Write/Read order; `ProtocolVersion` constant; retry tick.

**Why:** MP hosts control shared gameplay toggles.

**When extending:** append fields carefully or bump protocol and document.

---

## 11. Feature soft-disable flags

**Where:** Plugin / config / csproj (`RemoteUiExposed`, `CarvedDisplaysExposed`, `IncludeDisplayBundle`)

**How:** Keep code in tree; hide UI and omit bundles from packs.

**Why:** Ship stable subset without deleting large features.

---

## 12. UI asset loading

**Where:** `UI/UiAssets.cs`, `UI/UiFonts.cs`, `Content/UI/*.png`

**How:** Load sprites by name; shared wood panel language.

**Used by:** Filter menus, display board, buttons.

---

## 13. Display UI rebuild without flicker

**Where:** `Display/StorageDisplayBoard.cs`, `Display/DisplayVisual.cs`

**How:** Rebuild/paint in place (`RebuildUiNow`); avoid Destroy-all then throttled Paint; early-return in Ensure when already valid.

**Why:** Destroy+delay caused visible flicker.

---

## 14. Harmony skip counters

**Where:** e.g. `InventoryCountPatches.Skip`, `IncludeChests`, `ContainerFilter.AllowEmptySave`

**How:** Increment around intentional operations so patches do not re-enter or block intentional saves.

**Why:** Prevents infinite recursion and false wipe blocks.

---

## 15. Item ID maps for stations

**Where:** `World/ItemIds.cs`

**How:** Central lists/sets for fuels, ores, cookables, modded station items.

**Why:** StationFeed stays declarative; new cook items extend the map instead of forking feed logic.
