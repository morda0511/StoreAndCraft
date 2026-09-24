# StoreAndCraft – Current Behavior

How the mod **actually works** in the current codebase. Not a wishlist.

Where behavior is gated or soft-disabled, that is stated explicitly.

---

## Global

- Mod GUID: `Mordi.StoreAndCraft`.
- Master switch: `ModEnabled` (and feature toggles under Craft / Store / Stations / etc. in `ModConfig`).
- Multiplayer: host config synced to clients (`ProtocolVersion` 13). Clients should match mod version (`VersionGate`).
- Ranges: separate config for store vs craft (and station-related distances where bound).

---

## Default hotkeys (CONFIRMED from `ModConfig` binds)

Defaults are `KeyboardShortcut` values in `Configuration/ModConfig.cs`. Players may remap via BepInEx config. Handled primarily by `Input/Hotkeys.cs` (+ `BuildGrab`, rename helpers).

| Config key | Default | When / action |
|------------|---------|----------------|
| `DumpKey` | `.` (Period) | Dump allowed inventory stacks into nearby matching chests (`InventoryDump`) |
| `HoverStoreKey` | Mouse2 (MMB) | Inventory open: store hovered bag item (ignored if Ctrl held — that is TakeStack) |
| `TakeStackKey` | Ctrl+Mouse2 | Inventory open: fill hovered stack from chests (`TakeStack`) |
| `SearchKey` | `Y` | Inventory open: ping nearest chest containing hovered item |
| `FavoriteKey` | `F` | Inventory open: favorite/unfavorite hovered item |
| `SortKey` | `R` | Inventory open: sort open chest or bag |
| `RenameKey` | Alt+`E` | Small display options **or** station pull-filter **or** chest rename |
| `AutoFillKey` | `B` | Inventory closed: toggle station auto-fill on hovered supported station |
| `AutoDropKey` | `N` | Inventory closed: toggle auto-drop on cook station / beehive |
| `DisplayRangeKey` | Alt+`R` | Look at Storage Display: open per-board range menu |
| `BuildGrabKey` | `C` (held) | Hammer place confirm: grab piece mats from chests instead of placing |

Hotkeys skipped while console/chat/text input or filter/display menus are open.

---

## Storage discovery

- Nearby player-built chests are scanned on a tick (`NearbyIndex.Tick`) when a local player exists.
- Operations that need accurate counts call ensure/load paths so empty client views after travel do not silently “succeed” remote deposits.
- Non-player / invalid containers are excluded by `ContainerFilter`.

---

## Ignored containers — `[I]`

- Chests whose name includes the ignore tag are skipped by store/craft/feed paths that call `ChestNames.IsIgnored`.
- `[H]` hides from dump/store/craft but can still show on Storage Displays (per RenameKey config text).

---

## LeaveOneItem

- Config: `LeaveOneItem` (Craft section), default true; synced.
- When counting/consuming craft mats from chests, one unit is reserved per chest for **stackable** items (`ItemIds.ShouldLeaveOne` → `MaxStackSize > 1`).
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

**Important split:** player autofill is `StationAutoFill` (toggled with B). `StationFeed` is the shared pull/stamp helper. `CookingAutoDrop` is separate (N). `RemoteAutomation` output→chest is coded but **inactive** (see Remote).

### Supported auto-fill types (`StationAutoFill` Register + Tick)

| Vanilla / mod type | Component | Input behavior | Output behavior (active product) | Link `[lN]` | Pull filter | Automation |
|--------------------|-----------|----------------|----------------------------------|---------------|-------------|------------|
| Kiln, smelter, blast furnace, eitr refinery, windmill, Frost Foundry, etc. | `Smelter` | Fuel + ore/queue via `OnAddFuel` / `OnAddOre` reflection when auto-fill on and empty/need | No direct chest deposit | Yes (`StationLink`) | Yes (`StationPullFilter`) | Local B toggle ZDO `SAC_autoFill`; pulse from player |
| Torches / campfires that refill | `Fireplace` (`m_canRefill`) | Fuel to max | None by mod | Yes | Via hover/filter paths where wired | Same autofill ZDO |
| Cooking spit, stone oven, iron cooking station, … | `CookingStation` | Fuel + food; food may use RPC_AddItem; `EnsureCookDropPrefab` for `IsItemAllowed` | **Auto-drop (N):** finished food → ground via `RPC_RemoveDoneItem` (not chest) | Yes | Multi-input filter | Autofill B + optional AutoDrop N |
| Fermenter | `Fermenter` | Add mead base when empty | None by mod | Yes | Where applicable | Autofill B |
| Mistlands ballista | `Turret` | Ammo via `UseItem` | None by mod | Yes | — | Autofill B |
| Food serving tray | `ItemStand` (food tray only) | Attach food via `UseItem` | None by mod | Yes | — | Autofill B |
| Beehive | `Beehive` | **Not** auto-filled | **Auto-drop (N):** honey extract → ground | — | — | AutoDrop only |

**Not registered for autofill:** `SpinningWheel` and other non-listed components — no `Register` / Awake patch in `StationAutoFill`.

Comment in `StationAutoFill` lists windmill under Smelter coverage (Valheim windmill uses `Smelter`).

### Linked storage

- Station ZDO `SAC_stationLink` 0–9; chests tagged `[lN]` / legacy `[linkN]` in name (`StationLink`).
- Link ≥ 1 → only matching chests; link 0 → only untagged chests (linked chests exclusive).
- Active pull context: `StationLink.PushStation` / `ChestAllowedForActive` used by `StationFeed` / `NearbyIndex` counts when in station pull.

### Filters

- `StationPullFilter` + `StationFilterMenu` (opened via RenameKey / Alt+E path when looking at multi-input stations).
- Denied types are not chest-pulled for that station (manual inventory use still possible).

### Config related

- `AutoFillRange`, `StationFillSkipInventory` (prefer inventory before chests unless remote `ForceChestOnly`).
- Remote path forces chest-only via `StationFeed.BeginStationPull(..., chestOnly: true)` — only when Remote enabled.

---

## Output → chest

| Path | Status | Behavior |
|------|--------|----------|
| `CookingAutoDrop` | **Active** | Station/hive → **ground** drop → player `AutoIntake` can store into chests |
| `RemoteAutomation.TryHandleOutput` | **Implemented, inactive** | Would deposit smelter/oven output into linked chests; gated by `RemoteUiExposed => false` so always `PassThrough` |
| Other direct station→chest pull | **Not implemented** outside Remote | No separate active “output withdraw to chest” system found |

---

## Modded cooking / Volture (CONFIRMED approach)

There is **no** hardcoded Volture item ID allow-list.

Compatibility is dynamic:

1. Station `m_conversion` list drives allowed cookables (`StationFeed.FindCookConversion`, `CookPrefabName`).
2. `EnsureCookDropPrefab` / Harmony on `CookItem`, `IsItemAllowed`, use-item paths stamp `m_dropPrefab` so name matches conversion `m_from` (avoids “can't use …” when prefab null or `(Clone)`).
3. `StationFeed.EnsureForUse` refuses to wipe a valid hotbar/manual stack (Volture Meat + Iron Cooking Station case in comments).
4. `ItemIds.PrefabFromToken` resolves prefab **names** like `VoltureMeat` as well as `$item_` tokens; null cache not poisoned when ObjectDB was not ready.

Any cookable present on that station’s conversion table can work; items with no conversion on that station are not force-mapped.

---

## Storage Display

### Vanilla boards (active)

- Prefabs: `sac_storage_display_small` / `_medium` / `_large` cloned from wood sign.
- Components inherited/added: `ZNetView` (persistent), `Piece`, `Sign`, `StorageDisplayBoard`; WearNTear from sign clone.
- Colliders: Small unchanged; Medium/Large BoxColliders thinned on shallow axis (`SoftenColliders`); **not** triggers (placement would break).
- Interact: `Sign.Interact` → type menu (medium/large); small: hotbar assign; Shift+hover blocks attack for scale/layout cycle.
- **No Rigidbody** added by this mod.

### Carved boards (code present, not exposed)

- `DisplayPrefab.CarvedDisplaysExposed => false` — carved variants not registered.
- `IncludeDisplayBundle` defaults **false** in csproj; bundle file may exist at `Content/Displays/sac_displays` but is not copied unless pack flag true.
- When/if enabled: `DisplayVisual.Ensure` instantiates child `SacModel`, fits root `BoxCollider` to mesh bounds (min Z 0.12), disables extra boxes, lifts nested snap points to piece root as `SacSnap` with tag `snappoint`, aligns canvas to `ItemGrid`.
- Exact mesh/collider authoring **inside** each Unity prefab binary: not fully expanded in docs (manifest lists six prefab names only).

---

## Multiplayer / dedicated ownership (CONFIRMED + residual UNKNOWN)

### Confirmed

- Chest mutations that change shared inventory require **ZNetView ownership** of that container (`IsChestOwner` / RPC handlers check `nv.IsOwner()`).
- Non-owner client: `Withdraw` / `Consume` / deposit invoke chest RPCs; owner runs `OnRemove` / `OnConsume` / … then may `SendGrant` via `ZRoutedRpc` (`KAC_Grant`).
- RPC validated: usable container, private-area access, sender within range + 6 m slack (`ValidateRpc` / `SenderInRange`).
- Open chest: avoid claiming ownership (kick / wipe risk); grab uses async grant if `IsInUse()`.
- Dedicated process Update: `TransferService.Tick` + `AutoIntake.TickDedicated` + `RemoteAutomation.TickDedicated` only (remote no-ops while gated).
- Config: `AdminUtil.IsServer()`; dedicated cannot use local settings UI (`CanEditSettings` false on dedicated).

### Still UNKNOWN (not fully traced)

- Exact vanilla ZDO ownership handoff when the last player leaves a sector on a dedicated host (beyond Remote keep-alive code, which is inactive).
- Every edge case of simultaneous multi-client EnsureInventory races (mitigated by wipe guards, not exhaustively proven).

---

## Remote Automation (CONFIRMED current state)

| Aspect | Finding |
|--------|---------|
| Hard gate | `RemoteAutomation.RemoteUiExposed => false` |
| `Enabled()` | Returns false immediately when gate false (ignores config true) |
| `Tick` / `TickDedicated` | Early return |
| UI | `UiExposed` false — no remote toggle in filter |
| Output→chest | Code in `TryHandleOutput` + smelter Harmony prefixes; inactive |
| Config | `RemoteAutomationEnabled` default true still synced — dormant |
| Git | Experimental **remote dump** removed in **1.3.1**; current Remote Automation is a separate later system, still soft-hidden |

Do not document as a player feature until `RemoteUiExposed` is true and tested.

---

## Configuration persistence

- Local BepInEx config file.
- Synced subset via `WriteTo` / `ReadFrom` on `ModConfig` — changing field order breaks clients; bump `ProtocolVersion`.

---

## Soft dependencies

- **Epic Loot:** optional; enchanting mats from chests when bridge detects types.
- No Jotunn requirement.
- Remote Automation also refuses to enable if LazyVikings GUID is loaded (only relevant if Remote gate opened).
