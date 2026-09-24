# StoreAndCraft – Agent Rules

StoreAndCraft is a mature interconnected Valheim mod.

The goal is NOT to produce the most sophisticated or abstract implementation.

The goal is to maintain a stable mod while adding functionality safely.

Existing working code is valuable.

A simple existing solution is preferable to a new complex solution if it already solves the problem.

Never sacrifice working functionality just to make the code look cleaner.

Read `Docs/` before changing shared systems. Follow `Docs/Change-Protocol.md` for non-trivial work.

---

## RULE 1 – UNDERSTAND FIRST, MODIFY SECOND

Never immediately modify code just because a line appears related to a bug.

Before modifying existing code:

1. Identify the relevant method.
2. Identify all known callers.
3. Identify what the method calls.
4. Identify shared state.
5. Identify events.
6. Identify Harmony patches.
7. Identify networking implications.
8. Identify related systems.
9. Identify existing patterns.
10. Check Git history when relevant.

Only after this analysis may code be changed.

---

## RULE 2 – ALWAYS SEARCH FOR EXISTING IMPLEMENTATIONS

Before creating:

* a new class
* a new manager
* a new helper
* a new system
* a new cache
* a new event
* a new API wrapper
* a new abstraction

search the entire project first.

If an existing implementation can be extended, use it.

Do not create duplicate systems.

---

## RULE 3 – MINIMAL CHANGES

Only modify what is necessary for the requested task.

Do NOT:

* refactor unrelated code
* optimize unrelated code
* rename unrelated code
* reorganize unrelated code
* rewrite working systems
* replace existing implementations without a clear reason
* "clean up" unrelated code
* introduce unnecessary abstractions

---

## RULE 4 – NO UNREQUESTED BUG FIXES

If another bug is discovered while implementing a requested task:

DO NOT automatically fix it.

Report it separately.

Only fix it when explicitly requested.

Do not expand the scope of the current task.

---

## RULE 5 – EXISTING BEHAVIOR IS PROTECTED

Existing behavior must be considered intentional unless there is evidence otherwise.

A new feature must not silently change existing functionality.

If a requested change conflicts with existing behavior:

STOP and explain the conflict before implementing.

---

## RULE 6 – DEPENDENCY CHECK BEFORE EVERY IMPORTANT CHANGE

Before changing shared code ask:

> "What else uses this?"

Trace the dependency chain.

Do not make a local change without understanding its wider effect.

Shared hubs (see `Docs/Risks.md`): `NearbyIndex`, `TransferService`, `ContainerFilter` / `ChestNames`, `ItemIds`, `StationFeed`, `RequirementBridge` / `StagingPull`, `ModConfig` ProtocolVersion, Harmony patches that touch inventory load/save.

---

## RULE 7 – NEVER GUESS VALHEIM APIs

Do not invent:

* Valheim APIs
* Unity APIs
* Jotunn APIs
* BepInEx APIs
* networking behavior
* prefab behavior
* ZDO behavior
* RPC behavior

This project uses **BepInEx + Harmony**. It does **not** use Jotunn (soft Epic Loot reflection only).

If uncertain:

1. Search the existing project.
2. Inspect referenced assemblies/source where available.
3. Inspect existing working implementations.
4. Verify documentation/source when appropriate.
5. Mark uncertainty as `UNKNOWN – VERIFY BEFORE USE`.

Never present an assumption as verified fact.

---

## RULE 8 – GIT HISTORY IS A SOURCE OF INFORMATION

If a feature previously worked and is now broken:

Before inventing a new implementation:

1. inspect Git history
2. find the previous implementation
3. determine what changed
4. understand why it changed
5. restore/reuse the previous approach when appropriate

Do not automatically redesign the system.

Notable history: remote dump removed in 1.3.1; chest wipe guard in 1.3.30–1.3.31; LeaveOne / auto-store fixes in 1.1.x–1.2.x. See `Docs/Initial-Analysis.md`.

---

## RULE 9 – PLAN BEFORE IMPLEMENTATION

For every non-trivial change, first produce:

### Problem

What is actually wrong?

### Root cause

What causes it?

### Existing implementation

How does the current system work?

### Dependencies

What else uses this code?

### Affected files

Which files need to change?

### Proposed solution

What is the smallest safe change?

### Regression risks

What could break?

### Test plan

How will the change be verified?

Only then implement.

---

## RULE 10 – BUILD AFTER CHANGES

After modifying code:

1. Build the project (`dotnet build -c Release` unless the user specifies otherwise).
2. Inspect compiler errors.
3. Fix errors caused by the current change.
4. Do not ignore errors.
5. Do not hide warnings/errors.

Deploy copies the DLL for the normal Release build. Do **not** create Thunderstore/Hexium/Nexus zip packs (`-p:Pack=true`) unless the user explicitly asks. After a code fix: put only the new DLL in the Thunderstore **test** profile and stop for in-game testing.

---

## RULE 11 – REGRESSION CHECK

After implementing a change:

Check the requested functionality AND related functionality listed in:

`Docs/Regression.md`

Do not assume that a successful build means the feature is safe.

---

## RULE 12 – REVIEW THE DIFF

Before considering a task complete:

Review the Git diff.

Ask:

* Did I change only what was necessary?
* Did I modify unrelated files?
* Did I accidentally change existing behavior?
* Did I introduce duplicate logic?
* Did I modify shared code unnecessarily?
* Did I change something that another system depends on?

If unrelated changes were introduced, remove them.

---

## RULE 13 – DO NOT IMPROVE THINGS WITHOUT PERMISSION

Do not turn:

> "Fix this bug"

into:

> "Redesign the entire system."

Do not turn:

> "Add this feature"

into:

> "Rewrite the existing implementation."

StoreAndCraft is an existing project.

Stability is more important than architectural elegance.

---

## RULE 14 – STOP WHEN UNCERTAIN

If you cannot determine how a change affects the rest of the project with reasonable confidence:

STOP.

Explain:

* what you know
* what you do not know
* what you inspected
* what could be affected
* what information is needed

Do not guess.

---

## RULE 15 – NO PARALLEL IMPLEMENTATIONS

Never create a second implementation of an existing feature simply because the existing implementation is inconvenient.

First determine whether the existing implementation can be extended.

Only create a new implementation if there is a demonstrated technical reason.

Document that reason.

---

## RULE 16 – COMMITS, RELEASES, ZIPS

* Do **not** commit, push, or open GitHub releases unless the user explicitly asks.
* Do **not** pack Thunderstore zips unless the user explicitly asks.
* After the user confirms a build works in-game, only then do GitHub + zip packs **if they ask**.
* Changelogs use only `NEW FEATURES` / `UI IMPROVEMENTS` / `FIX` (player-facing). See workspace changelog rules.

---

## Documentation map

| File | Purpose |
|------|---------|
| `Docs/Architecture.md` | Systems and dependencies |
| `Docs/Behavior.md` | Current player-facing + code behavior |
| `Docs/Valheim-Modding.md` | APIs as used by this mod |
| `Docs/Patterns.md` | Reuse these patterns |
| `Docs/Risks.md` | High-risk shared code |
| `Docs/Regression.md` | Manual test checklist |
| `Docs/Change-Protocol.md` | Required workflow |
| `Docs/Initial-Analysis.md` | Snapshot + git findings + unknowns |
