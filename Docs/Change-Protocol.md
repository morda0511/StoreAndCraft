# StoreAndCraft – Change Protocol

Purpose: prevent impulsive edits on an interconnected mod.

```text
REQUEST
   ↓
UNDERSTAND
   ↓
SEARCH EXISTING IMPLEMENTATION
   ↓
TRACE DEPENDENCIES
   ↓
CHECK GIT HISTORY
   ↓
IDENTIFY ROOT CAUSE
   ↓
IDENTIFY AFFECTED SYSTEMS
   ↓
PLAN MINIMAL CHANGE
   ↓
IMPLEMENT
   ↓
BUILD
   ↓
TEST REQUESTED FEATURE
   ↓
TEST RELATED FEATURES
   ↓
REVIEW GIT DIFF
   ↓
REPORT
```

---

## Step details

### REQUEST

Clarify what the user wants. Do not expand scope.

### UNDERSTAND

Read the owning class and call sites. Consult `Docs/Architecture.md`, `Docs/Behavior.md`, `Docs/Risks.md`.

### SEARCH EXISTING IMPLEMENTATION

Grep for similar features. Prefer extending `NearbyIndex`, `TransferService`, `ItemIds`, `StationFeed`, etc. See `Docs/Patterns.md`.

### TRACE DEPENDENCIES

Ask “what else uses this?” List callers, shared state, Harmony, RPCs, config sync.

### CHECK GIT HISTORY

If it used to work: `git log` / `git blame` / diff old implementation. Do not blindly redesign. Note: remote dump was removed in 1.3.1 for cause.

### IDENTIFY ROOT CAUSE

Separate symptom (UI text, missing fill) from cause (ID map, ownership, LeaveOne, patch target).

### IDENTIFY AFFECTED SYSTEMS

Map to regression rows in `Docs/Regression.md`.

### PLAN MINIMAL CHANGE

Write the Rule 9 plan (Problem → Test plan) before editing. If conflict with protected behavior: STOP and ask.

### IMPLEMENT

Smallest diff. No drive-by refactors. No unrequested bugfixes (report them).

### BUILD

`dotnet build -c Release` (Deploy copies DLL). Fix errors from this change. Do not pack zips unless asked.

### TEST REQUESTED FEATURE

In-game in the test profile with the new DLL only.

### TEST RELATED FEATURES

Use `Docs/Regression.md` for shared hubs you touched.

### REVIEW GIT DIFF

Remove unrelated edits. Confirm no accidental protocol/behavior changes.

### REPORT

What changed, what was verified, what remains unknown, any bugs found but not fixed.

---

## Hard stops

- Uncertain Valheim/ZDO/RPC behavior → mark `UNKNOWN – VERIFY BEFORE USE` and stop guessing.
- Change would silently alter existing LeaveOne / ignore / ownership semantics → explain conflict first.
- Re-enabling remote dump / remote UI / shipping display bundles → requires explicit user request.
