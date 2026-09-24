# StoreAndCraft – Risks

Sensitive areas. Changing these without a dependency trace is likely to cause regressions.

---

## TransferService (all store/craft/feed moves)

| | |
|--|--|
| **Classes** | `Network/TransferService.cs` |
| **Dependents** | AutoStore, StagingPull, StationFeed/AutoFill, BuildGrab, drop-store, RemoteAutomation |
| **Shared state** | Move queue, `FillsQueued`, grant RPC registration, ownership decisions |
| **Why sensitive** | Wrong claim/save wipes chests; wrong RPC orphans items; stealing ownership kicks players |
| **Before change** | Trace every caller of `StoreItem` / consume / deposit; re-read ownership comments; MP test open chest + travel unload |
| **Regression** | Auto-store, craft spend, smelter fill, “stored but still in bag”, empty chests |

---

## NearbyIndex + EnsureInventory

| | |
|--|--|
| **Classes** | `World/NearbyIndex.cs` |
| **Dependents** | Almost all gameplay systems |
| **Shared state** | `Current` list, caches, ensure timing |
| **Why sensitive** | Stale/empty views drive false counts and wipe races with displays |
| **Before change** | List callers of `Tick`, `CountItem`, `EnsureInventory`; check interaction with ChestLoadGuard |
| **Regression** | Craft UI counts, autofill, displays, LeaveOne counts |

---

## Chest wipe guard Harmony

| | |
|--|--|
| **Classes** | `Patches/ChestLoadGuardPatch.cs`, `ChestLoadGuard` |
| **Dependents** | Global inventory load/save for all containers |
| **Shared state** | `BlockEmptySave` set, `AllowEmptySave` |
| **Why sensitive** | Too aggressive → chests never empty when they should; too weak → wipes return |
| **Before change** | Understand intentional empty saves; never remove prefixes “for simplicity” |
| **Regression** | Emptying chests by hand, displays, EnsureInventory after boss run |

---

## LeaveOne / ItemIds.ShouldLeaveOne / RequirementBridge

| | |
|--|--|
| **Classes** | `ItemIds`, `RequirementBridge`, `StagingPull`, `NearbyIndex` count helpers |
| **Dependents** | Craft UI, craft spend, autofill maps, build grab, Epic Loot exceptions |
| **Why sensitive** | Breaks auto-store routing or blocks unique idol/runestone crafts |
| **Before change** | Check forge upgrader path + EpicLootBridge no-LeaveOne |
| **Regression** | Last-mat crafts, Potential forge, enchanting, auto-store targets |

---

## StagingPull + inventory count patches

| | |
|--|--|
| **Classes** | `StagingPull`, `PlayerCraftPatches`, `InventoryCountPatches`, `UiPatches` |
| **Shared state** | `Skip`, `IncludeChests`, `StagingPull.Active` |
| **Why sensitive** | Recursion into CountItems; craft fizzle if staging returns; UI lies about mats |
| **Before change** | Keep consume and count Skip discipline; test arrows (stack) vs new item craft (empty slot) |
| **Regression** | Workbench crafts, building pieces, plantables if patched |

---

## StationFeed / ItemIds cook maps / StationPatches hover

| | |
|--|--|
| **Classes** | `StationFeed`, `StationAutoFill`, `StationPullFilter`, `StationPatches`, `ItemIds` |
| **Dependents** | All processing stations + hover UX |
| **Why sensitive** | Wrong hover target = missing prompts on multi-switch pieces; wrong IDs = no fill (Volture etc.) |
| **Before change** | Identify which Switch/Hover method is patched; confirm item IDs in-game |
| **Regression** | Smelter/kiln/oven/cook station fill; hover hotkey order |

---

## ModConfig ProtocolVersion wire format

| | |
|--|--|
| **Classes** | `ModConfig`, `ConfigSync`, `ConfigSyncRetry` |
| **Dependents** | All MP sessions |
| **Why sensitive** | Reordered Read/Write desyncs clients silently or breaks parse |
| **Before change** | If layout changes, bump `ProtocolVersion` and document |
| **Regression** | Clients ignoring host toggles, exceptions on join |

---

## Storage Display UI rebuild

| | |
|--|--|
| **Classes** | `StorageDisplayBoard`, `DisplayVisual`, `DisplayPrefab` |
| **Dependents** | Display pieces; EnsureInventory traffic |
| **Why sensitive** | Flicker; wipe races if ensure+load mis-ordered; bundle flags affect shipping |
| **Before change** | Do not reintroduce Destroy-all + throttled paint; respect soft-disable/bundle flags |
| **Regression** | Display flicker, empty boards, chest wipes near displays |

---

## Soft-disabled RemoteAutomation

| | |
|--|--|
| **Classes** | `Craft/RemoteAutomation.cs` (`RemoteUiExposed => false`), Plugin dedicated/client ticks |
| **Why sensitive** | Large surface (keep-alive, output deposit, ForceChestOnly). Distinct from removed remote dump (1.3.1). |
| **Current state** | Hard-gated off; config flag alone cannot enable |
| **Before change** | Do not flip `RemoteUiExposed` without explicit user request + dedicated + MP tests |
| **Regression** | Unexpected remote pulls, zone load cost, item loss, conflict with LazyVikings |

---

## Hotkeys vs hover text

| | |
|--|--|
| **Classes** | `Hotkeys`, station/chest hover patches, ModConfig binds |
| **Why sensitive** | UI promises a key that code does not handle (or vice versa) |
| **Before change** | Update both binding and hover string sources together |
| **Regression** | Player-facing “key does nothing” reports |

---

## Dependency sketch (shared hubs)

```text
ItemIds / LeaveOne
    ↑
NearbyIndex ← ContainerFilter / ChestNames
    ↑
TransferService ←── AutoStore, StagingPull, StationFeed, BuildGrab
    ↑
Harmony guards (Load/Save) protect all of the above from wipe races

RequirementBridge / StagingPull ←── PlayerCraft + UI count patches

StationLink / PullFilter ←── StationFeed

ModConfig Protocol ←── ConfigSync (all clients)
```
