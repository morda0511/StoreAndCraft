# StoreAndCraft â€“ Current Behavior

How the mod **actually works** in the current codebase. Not a wishlist.

Where behavior is gated or soft-disabled, that is stated explicitly.

---

## Global

- Mod GUID: `Mordi.StoreAndCraft`.
- Master switch: `ModEnabled` (and feature toggles under Craft / Store / Stations / etc. in `ModConfig`).
- Multiplayer: host config synced to clients (`ProtocolVersion` 15). Clients should match mod version (`VersionGate`).
- Ranges: separate config for store vs craft (and station-related distances where bound).

---

## Default hotkeys (CONFIRMED from `ModConfig` binds)

Defaults are `KeyboardShortcut` values in `Configuration/ModConfig.cs`. Players may remap via BepInEx config. Handled primarily by `Input/Hotkeys.cs` (+ `BuildGrab`, rename helpers).

| Config key | Default | When / action |
|------------|---------|----------------|
| `DumpKey` | `.` (Period) | Dump allowed inventory stacks into nearby matching chests (`InventoryDump`) |
| `HoverStoreKey` | Mouse2 (MMB) | Inventory open: store hovered bag item (ignored if Ctrl held â€” that is TakeStack) |
| `TakeStackKey` | Ctrl+Mouse2 | Inventory open: fill hovered stack from chests (`TakeStack`) |
| `SearchKey` | `Y` | Inventory open: ping nearest chest containing hovered item |
| `FavoriteKey` | `F` | Inventory open: favorite/unfavorite hovered item |
| `SortKey` | `R` | Inventory open: sort open chest or bag |
| `RenameKey` | Alt+`E` | Settings: chest / small display / station pull-filter + link |
| `AutoFillKey` | `B` | Inventory closed: toggle station auto-fill on hovered supported station |
| `AutoDropKey` | `N` | Inventory closed: toggle auto-store on cook / beehive / kiln-smelter (output → nearby chest; independent of B) |
| `ActivityLogKey` | `F11` | Toggle on-screen activity log (starts off; max 10 lines under TopLeft status text) |
| `DisplayRangeKey` | Alt+`R` | Look at Storage Display: open per-board range menu |
| `BuildGrabKey` | `C` (held) | Hammer place confirm: grab piece mats from chests instead of placing |

Hotkeys skipped while console/chat/text input or filter/display menus are open.

---

## Storage discovery

- Nearby player-built chests are scanned on a tick (`NearbyIndex.Tick`) when a local player exists.
- Operations that need accurate counts call ensure/load paths so empty client views after travel do not silently â€œsucceedâ€ remote deposits.
- Non-player / invalid containers are excluded by `ContainerFilter`.

---

## Ignored containers â€” `[I]`

- Chests whose name includes the ignore tag are skipped by store/craft/feed paths that call `ChestNames.IsIgnored`.
- `[H]` hides from dump/store/craft but can still show on Storage Displays (per RenameKey config text).

---

## LeaveOneItem

- Config: `LeaveOneItem` (Craft section), default true; synced.
- When counting/consuming craft mats from chests, one unit is reserved per chest for **stackable** items (`ItemIds.ShouldLeaveOne` â†’ `MaxStackSize > 1`).
- **Exceptions (verified):**
  - Upgrade forge (`CraftingStation.m_upgrader`): LeaveOne **off** (`RequirementBridge.LeaveOneInChests`).
  - Epic Loot enchanting path: consume **without** LeaveOne.
- Non-stackables (max stack 1) never reserve LeaveOne.

---

## Auto-store (push)

- Dump / hover-store / intake move items into nearby eligible chests within store range.
- Uses `TransferService.StoreItem`: prefers local ownership when inventory view is non-empty and safe; avoids claim+save wipe after boss travel; ignored chests skipped.

---

## Crafting from storage

- Requirements UI and `HaveRequirements`-style checks can see chest counts via Harmony count patches + `RequirementBridge`.
- On craft/build consume, `StagingPull.ConsumeRequirements` pays the **deficit** from chests directly (does not dump full stacks into free inventory slots first).

---

## Station systems (CONFIRMED matrix)

**Important split:** player autofill is `StationAutoFill` (toggled with B). `StationFeed` is the shared pull/stamp helper. `CookingAutoDrop` is separate (N).

### Supported auto-fill types (`StationAutoFill` Register + Tick)

| Vanilla / mod type | Component | Input behavior | Output behavior (active product) | Link `[lN]` | Pull filter | Automation |
|--------------------|-----------|----------------|----------------------------------|---------------|-------------|------------|
| Kiln, smelter, blast furnace, eitr refinery, windmill, Frost Foundry, etc. | `Smelter` | Fuel + ore/queue via `OnAddFuel` / `OnAddOre` reflection when auto-fill on and empty/need | **Auto-drop (N):** finished bars → nearby chest (vanilla floor if off) | Yes (`StationLink`) | Yes (`StationPullFilter`) | Autofill B + optional AutoDrop N |
| Torches / campfires that refill | `Fireplace` (`m_canRefill`) | Fuel to max | None by mod | Yes | Via hover/filter paths where wired | Same autofill ZDO |
| Cooking spit, stone oven, iron cooking station, â€¦ | `CookingStation` | Fuel + food; food may use RPC_AddItem; `EnsureCookDropPrefab` for `IsItemAllowed` | **Auto-drop (N):** finished food â†’ nearby chest (ground fallback) | Yes | Multi-input filter | Autofill B + optional AutoDrop N |
| Beehive | `Beehive` | **Not** auto-filled | **Auto-drop (N):** honey â†’ nearby chest (ground fallback) | â€” | â€” | AutoDrop only |
| Fermenter | `Fermenter` | Add mead base when empty | None by mod | Yes | Where applicable | Autofill B |
| Mistlands ballista | `Turret` | Ammo via `UseItem` | None by mod | Yes | â€” | Autofill B |
| Food serving tray | `ItemStand` (food tray only) | Attach food via `UseItem` | None by mod | Yes | â€” | Autofill B |

**Not registered for autofill:** `SpinningWheel` and other non-listed components â€” no `Register` / Awake patch in `StationAutoFill`.

Comment in `StationAutoFill` lists windmill under Smelter coverage (Valheim windmill uses `Smelter`).

### Linked storage

- Station ZDO `SAC_stationLink` 0â€“9; chests tagged `[lN]` / legacy `[linkN]` in name (`StationLink`).
- Link â‰¥ 1 â†’ only matching chests; link 0 â†’ only untagged chests (linked chests exclusive). Pulls pass the station link id explicitly so a leaked ActiveId=-1 cannot open every chest.
- Active pull context: `StationLink.PushStation` / `ChestAllowedForActive` used by `StationFeed` / `NearbyIndex` counts when in station pull.

### Filters

- `StationPullFilter` + `StationFilterMenu` (opened via RenameKey / Alt+E path when looking at multi-input stations).
- Denied types are not chest-pulled **and** cannot be manually inserted from the bag on that station.

### Config related

- `AutoFillRange` (chests only â€” never bag; link rules + `[I]`/`[H]` apply).
- Alt+E pull filter also blocks manual bag insert of denied types on that station.

---

## Output → chest

| Path | Status | Behavior |
|------|--------|----------|
| `CookingAutoDrop` | **Active** | Station → chest: **matching `[lN]` first** (MustHave), then **untagged** (MustHave); never another link; else ground + same intake prio |
| Direct station→chest | **Active** | Auto-store (N) via `StationOutput` two-pass (link → untagged); fill still uses stricter `ChestAllowed` |
| Activity log (F11) | **Active** | Starts off; up to 10 lines under MessageHud TopLeft (“Dir ist kalt”), same font; e.g. `Chest 10x Tin → Smelter` |

---

## Modded cooking / Volture (CONFIRMED approach)

There is **no** hardcoded Volture item ID allow-list.

Compatibility is dynamic:

1. Station `m_conversion` list drives allowed cookables (`StationFeed.FindCookConversion`, `CookPrefabName`).
2. `EnsureCookDropPrefab` / Harmony on `CookItem`, `IsItemAllowed`, use-item paths stamp `m_dropPrefab` so name matches conversion `m_from` (avoids â€œcan't use â€¦â€ when prefab null or `(Clone)`).
3. `StationFeed.EnsureForUse` refuses to wipe a valid hotbar/manual stack (Volture Meat + Iron Cooking Station case in comments).
4. `ItemIds.PrefabFromToken` resolves prefab **names** like `VoltureMeat` as well as `$item_` tokens; null cache not poisoned when ObjectDB was not ready.

Any cookable present on that stationâ€™s conversion table can work; items with no conversion on that station are not force-mapped.

---

## Storage Display

### Vanilla boards (active)

- Prefabs: `sac_storage_display_small` / `_medium` / `_large` cloned from wood sign.
- Components inherited/added: `ZNetView` (persistent), `Piece`, `Sign`, `StorageDisplayBoard`; WearNTear from sign clone.
- Colliders: Small unchanged; Medium/Large BoxColliders thinned on shallow axis (`SoftenColliders`); **not** triggers (placement would break).
- Interact: `Sign.Interact` â†’ type menu (medium/large); small: hotbar assign; Shift+hover blocks attack for scale/layout cycle.
- Medium/Large: Display Scale (âˆ’2â€¦+4) and Classic/Compact Layout rebuild the grid; amount + category fonts follow cell size (Large uses the same font factors as Medium so Scale/Layout stay readable). Large left category rail is wide enough for full words (WEAPONS, INGREDIENTS) with autosize â€” not ellipsis. Column floor uses measured board rect even when width â‰ˆ 1 (vanilla sign text area).
- **No Rigidbody** added by this mod.

### Carved boards (code present, not exposed)

- `DisplayPrefab.CarvedDisplaysExposed => false` â€” carved variants not registered.
- `IncludeDisplayBundle` defaults **false** in csproj; bundle file may exist at `Content/Displays/sac_displays` but is not copied unless pack flag true.
- When/if enabled: `DisplayVisual.Ensure` instantiates child `SacModel`, fits root `BoxCollider` to mesh bounds (min Z 0.12), disables extra boxes, lifts nested snap points to piece root as `SacSnap` with tag `snappoint`, aligns canvas to `ItemGrid`.
- Exact mesh/collider authoring **inside** each Unity prefab binary: not fully expanded in docs (manifest lists six prefab names only).

---

## Multiplayer / dedicated ownership (CONFIRMED + residual UNKNOWN)

### Confirmed

- Chest mutations that change shared inventory require **ZNetView ownership** of that container (`IsChestOwner` / RPC handlers check `nv.IsOwner()`).
- Non-owner client: `Withdraw` / `Consume` / deposit invoke chest RPCs; owner runs `OnRemove` / `OnConsume` / â€¦ then may `SendGrant` via `ZRoutedRpc` (`KAC_Grant`).
- RPC validated: usable container, private-area access, sender within range + 6 m slack (`ValidateRpc` / `SenderInRange`).
- Open chest: avoid claiming ownership (kick / wipe risk); grab uses async grant if `IsInUse()`.
- Dedicated process Update: `TransferService.Tick` + `AutoIntake.TickDedicated` only.
- Config: `AdminUtil.IsServer()`; dedicated cannot use local settings UI (`CanEditSettings` false on dedicated).

### Still UNKNOWN (not fully traced)

- Exact vanilla ZDO ownership handoff when the last player leaves a sector on a dedicated host (beyond Remote keep-alive code, which is inactive).
- Every edge case of simultaneous multi-client EnsureInventory races (mitigated by wipe guards, not exhaustively proven).

---

## Remote Automation

Removed in **1.3.43** (`Craft/RemoteAutomation.cs`, config keys, keep-alive / zone Harmony patches). Earlier experimental **remote dump** was removed in **1.3.1**.

---

## Configuration persistence

- Local BepInEx config file.
- Synced subset via `WriteTo` / `ReadFrom` on `ModConfig` â€” changing field order breaks clients; bump `ProtocolVersion`.

---

## Soft dependencies

- **Epic Loot:** optional; enchanting mats from chests when bridge detects types.
- No Jotunn requirement.