# StoreAndCraft – Initial Analysis Report

Analysis-only pass at **1.3.39**. **Update:** Remote Automation was fully removed in **1.3.43** — sections below that describe a gated `RemoteAutomation` system are historical.

---

## Project overview

StoreAndCraft (GUID `Mordi.StoreAndCraft`) is a BepInEx + Harmony Valheim mod for:

- Auto-storing items into nearby player chests
- Crafting / building using materials still in chests (count + spend without backpack staging dumps)
- Feeding processing stations from storage (links/filters)
- Optional Storage Display boards
- Feed trough support
- Soft Epic Loot compatibility
- Host-synced config and version gating

Version at analysis: **1.3.39** (csproj/manifest). Config **ProtocolVersion 13**.

It does **not** use Jotunn.

---

## Architecture

Central hub: **`NearbyIndex`** (discover/cache chests) + **`TransferService`** (ownership-safe moves/RPCs).

Feature layers:

- **Push:** AutoStore
- **Pull/spend:** StagingPull + RequirementBridge + craft/UI Harmony
- **Stations:** StationLink / PullFilter / StationFeed / AutoFill + StationPatches
- **Display:** DisplayPrefab + StorageDisplayBoard (+ soft-gated carved bundle)
- **Net/config:** ConfigSync, VersionGate
- **Guards:** ChestLoadGuard Harmony on Inventory.Load / Container.Save

`Plugin.Update` (client/listen with local player): ConfigWatch → TransferService → NearbyIndex → PendingChestDebit → AutoIntake → AutoStack/SearchPing → display/filter menus → **StationAutoFill** → RemoteAutomation (gated). Dedicated: TransferService + AutoIntake + RemoteAutomation only. Hotkeys in LateUpdate. Feed trough is component/patch driven.

See `Docs/Architecture.md`.

---

## Critical dependencies

| Hub | Why connected |
|-----|----------------|
| NearbyIndex | All counts and many ensures |
| TransferService | All item movement across store/craft/feed |
| ContainerFilter / ChestNames | `[I]` and usable-storage policy everywhere |
| ItemIds + LeaveOne | Craft counts, spend, autofill maps, EL exceptions |
| StagingPull.Active | Gates craft patches and related UI |
| ModConfig wire format | MP behavior of every synced toggle |
| ChestLoadGuard | Protects ensure/display/transfer races |

Changing a hub without tracing dependents is the primary regression vector.

---

## High-risk areas

1. `TransferService` ownership / claim / empty-view deposit
2. `NearbyIndex.EnsureInventory` + wipe guard interaction
3. LeaveOne exceptions (forge upgrader, Epic Loot, unique items)
4. `StagingPull` + `InventoryCountPatches.Skip` discipline
5. `ModConfig` Read/Write order / ProtocolVersion
6. Station hover patch targets (multi-switch pieces)
7. Display Destroy/rebuild flicker and ensure traffic
8. Re-enabling RemoteAutomation / remote dump–like behavior

Details: `Docs/Risks.md`.

---

## Existing patterns to reuse

- One nearby index, not new scanners
- One transfer service, not new Claim+Save helpers
- LeaveOne via ItemIds/RequirementBridge, not ad-hoc `-1` in features
- Soft reflection for optional mods
- Soft-disable flags instead of deleting large features
- Harmony skip/allow counters around intentional operations
- Item ID maps for station inputs

Details: `Docs/Patterns.md`.

---

## Git history findings

Inspected via `git log` (message grep and recent releases). Selected facts:

| Commit / era | Finding |
|--------------|---------|
| **1.3.1** (`1775023`) | **Removed remote dump**; display/auto-fill perf; AOE spam fix. Do not resurrect remote dump casually. |
| **1.3.0** (`8f59eee`) | Displays, Shift+grab, Pull Stack, Epic Loot, experimental remote dump introduced. |
| **1.3.30–1.3.31** | Chest wipe guard; Select Types UI; Compact layout; feed trough eat-at-trough. |
| **1.2.5** | Large band layout; Epic Loot soft compat; Shift+build grab; hotbar-safe sort. |
| **1.1.10** | Auto-store crash + LeaveOneItem spend fix. |

**Note:** Last committed release messages in history may lag the working tree (1.3.39 WIP: remote/carved soft-hide, Volture/cook/hover, display flicker, etc.). Prefer code + current csproj over last tag when behavior conflicts.

Do not revert history as part of normal fixes; use it to recover known-good approaches.

---

## Potential technical debt (observed, not prescriptions)

- Large `TransferService` and `RemoteAutomation` files (complexity / review cost).
- Soft-disabled features still compile into the DLL (surface area for accidental calls).
- Dual authoring paths for displays (Unity project + runtime `DisplayPrefab` / bundle flags).
- Manual config serialization (easy to break MP when adding fields).
- Working tree often has many content/UI assets and WIP C# together — easy to mix docs-only vs code tasks.

No “rewrite” recommended; debt listed only so agents do not casually enlarge it.

---

## Unknown areas (after follow-up investigation)

Statuses: **CONFIRMED** / **PARTIALLY CONFIRMED** / **NOT IMPLEMENTED** / **UNKNOWN**.

| # | Topic | Status | Summary |
|---|--------|--------|---------|
| 1 | Default hotkey chords | **CONFIRMED** | All defaults in `ModConfig` + `Hotkeys` — see `Docs/Behavior.md` |
| 2 | StationFeed / autofill station matrix | **CONFIRMED** | Matrix in `Docs/Behavior.md`; autofill = `StationAutoFill` (not a StationFeed.Tick) |
| 3 | Output → chest | **PARTIALLY CONFIRMED** | Active path: AutoDrop → ground → intake. Direct station→chest only in inactive RemoteAutomation |
| 4 | Dedicated ownership | **PARTIALLY CONFIRMED** | RPC/owner rules confirmed; sector handoff when empty still UNKNOWN |
| 5 | Carved display physics | **PARTIALLY CONFIRMED** | Runtime SoftenColliders / FitCollider / LiftSnapPoints confirmed; binary prefab internals not fully dumped; carved not exposed |
| 6 | Volture / modded cook IDs | **CONFIRMED** | Dynamic conversion + dropPrefab stamping; no fixed ID list |
| 7 | RemoteAutomation state | **CONFIRMED** | `RemoteUiExposed => false`; ticks/UI/output inactive despite config default true |

Evidence paths are listed in the follow-up report and in `Docs/Behavior.md` / `Docs/Architecture.md`.

### Residual UNKNOWN only

- Dedicated ZDO ownership when the last player leaves a sector (with Remote keep-alive off).
- Exact authored mesh/collider trees inside each `sac_displays` Unity prefab (manifest names only).
- Exhaustive multi-client EnsureInventory race catalogue (guards exist; not formally proven).

---

## Documentation delivered

| Path | Role |
|------|------|
| `AGENTS.md` | Agent rules + philosophy |
| `Docs/Architecture.md` | Systems + dependencies |
| `Docs/Behavior.md` | Current behavior (+ resolved matrices) |
| `Docs/Valheim-Modding.md` | APIs as used here |
| `Docs/Patterns.md` | Reuse patterns |
| `Docs/Risks.md` | High-risk hubs |
| `Docs/Regression.md` | Manual tests |
| `Docs/Change-Protocol.md` | Workflow |
| `Docs/Initial-Analysis.md` | This report |

Future agents: **understand first, modify second**; follow `Docs/Change-Protocol.md`.
