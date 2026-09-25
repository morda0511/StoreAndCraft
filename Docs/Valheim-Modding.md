# Valheim modding knowledge relevant to StoreAndCraft

Priority: **how this project uses the APIs**, not generic internet tutorials.

If something is not verified in this repo or referenced assemblies, it is marked:

`UNKNOWN – VERIFY BEFORE USE`

---

## BepInEx

- Plugin inherits `BaseUnityPlugin`.
- `[BepInPlugin(GUID, Name, Version)]` on `Plugin`.
- Config via `Config.Bind` → wrapped in `ModConfig`.
- Soft dependency attributes may exist for Epic Loot (non-hard).
- Logging: `Plugin.Log` / BepInEx logger patterns used in project.

---

## Harmony

- `Harmony.CreateAndPatchAll` (or equivalent PatchAll) in Awake.
- Prefix/Postfix/Transpiler as used in `Patches/*`.
- Critical wipe guards: prefix on `Inventory.Load` and `Container.Save` (`ChestLoadGuardPatch`).
- Craft path: patches on player consume / have-requirements / inventory count / UI setup requirement.
- Station path: hover / interact / cooking related patches in `StationPatches`.
- **Do not** add overlapping patches on the same method without checking existing prefixes for early `return false` skips.

---

## Jotunn

- **Not used** by StoreAndCraft.
- Do not assume PieceManager / ItemManager from Jotunn.
- Prefabs: custom registration via project `DisplayPrefab` / piece table helpers and AssetBundles.

---

## Unity (as used here)

| Concept | Usage in project |
|---------|------------------|
| `MonoBehaviour` | `Plugin` is a Unity plugin behaviour; display boards / feeders are components on pieces. |
| `GameObject` / `Transform` | Piece instances, UI hierarchy under canvas. |
| `Component` | `Container`, `Inventory`, `ZNetView`, `CraftingStation`, custom display scripts. |
| Prefab | Vanilla stations/chests; mod display prefabs from bundle or code. |
| AssetBundle | `Content/Displays/sac_displays` when included; UI mostly loose PNGs under `Content/UI`. |
| Lifecycle | `Awake` init; `Update` ticks; destroy/rebuild UI carefully (display flicker). |

Collider / physics: only as required by vanilla piece placement — `UNKNOWN – VERIFY BEFORE USE` for custom collider setup on carved displays until reading `DisplayPrefab` / Unity project.

---

## Valheim inventory and items

| Type | Role |
|------|------|
| `ItemDrop` / `ItemDrop.ItemData` | Ground/item definition + stack instance. |
| `Inventory` | Player bag, chest, station slots. `CountItems`, `RemoveItem`, `AddItem`, `NrOfItems`, `Load`/`Save` via ZPackage. |
| `Container` | Chest piece wrapper; `GetInventory`, `IsInUse`, Load/Save tied to ZDO. |
| Shared name | `m_shared.m_name` localization token used as identity key in much of the mod. |
| Drop prefab | Used when shared name is insufficient (cooking IDs, uniqueness). |

**Wipe race (project-proven):** empty `Inventory.Load` package applied over a full local inventory, then `Container.Save`, persists empty chest. Guarded by Harmony + `ChestLoadGuard` + avoid claim on empty local view.

---

## Player and crafting

| Type | Role |
|------|------|
| `Player` | Local owner checks, inventory, current crafting station, consume resources. |
| `Recipe` / `Piece.Requirement` | Craft/build costs; quality and upgrader resource flags. |
| `CraftingStation` | Workbench/forge/etc.; `m_upgrader` special-cased for LeaveOne. |

StagingPull mirrors vanilla skip rules for upgrader resources when consuming from chests.

---

## Networking

| Type | Role |
|------|------|
| `ZNet` / peers | Server vs client; `IsDedicated()`, `IsServer()`, peer `m_refPos` for range checks |
| `ZDO` | Persistent object state including inventory blobs and mod keys (`SAC_*`) |
| `ZNetView` | View over ZDO; `IsOwner`, `ClaimOwnership`, `IsValid`, `InvokeRPC` |
| `ZRoutedRpc` | Named RPCs with `ZPackage` payloads (`KAC_Grant`) |
| Ownership | Chest inventory RPCs only apply when the handling peer owns the view. Non-owners request via `InvokeRPC`. Claiming while another player uses a chest or while local inventory view is empty is treated as **dangerous**. |

### Dedicated vs listen

- Dedicated Update does **not** run `NearbyIndex` / `StationAutoFill` / `Hotkeys` (no local player loop).
- Dedicated still runs `TransferService.Tick` and `AutoIntake.TickDedicated`.
- Dedicated host ticks: `TransferService` + `AutoIntake.TickDedicated` (Remote Automation removed in 1.3.43).
- Config edit UI: `AdminUtil.CanEditSettings()` is false on dedicated.

`UNKNOWN – VERIFY BEFORE USE`: full sector ownership migration when all players leave (outside inactive Remote keep-alive).

---

## Crafting stations and processing

- Smelters, charcoal kilns, ovens, cooking stations, spinning wheels, etc. are fed by `StationFeed` using station-specific slot rules.
- Hover text switches (`m_addFuelSwitch`, `m_addOreSwitch`, `m_addFoodSwitch`, …) may receive postfix hover strings; multiple switches on one piece (stone oven) means **order and target method** matter.
- Modded stations (e.g. Volture iron cooking): item IDs must be in `ItemIds` maps or feed will ignore them.

---

## Configuration

- BepInEx `ConfigEntry<T>` per setting.
- Networked subset serialized manually in `ModConfig` — **order is ABI**.
- Changing defaults does not bump protocol; changing wire layout must bump `ProtocolVersion`.

---

## Input

- Valheim/BepInEx key binds via config.
- UI clicks for filter menus / display type menus.
- Do not assume Unity `Input.GetKey` patterns without reading `Hotkeys` / Plugin handlers.

---

## Prefab registration

- Feeder and display pieces registered in Awake paths.
- Unity project under `Unity/StoreAndCraftDisplays/` is an authoring/export aid; runtime loads exported bundle/content as configured by csproj (`IncludeDisplayBundle`).

---

## World / scan

- `NearbyIndex` is the mod’s spatial index — prefer it over ad-hoc `FindObjectsOfType` loops in new features.
- `EnsureInventory` is part of anti-wipe / anti-stale-view strategy; calling it incorrectly (force + claim + save) is high risk.
