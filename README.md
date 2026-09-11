# StoreAndCraft

**Your chests work with you.** Ground loot stores itself, one key dumps your inventory, crafting and building pull from nearby chests, smelters refill with **[E]**, and Storage Displays show what’s in stock — by type (Wood, Ore, Food, …).

**Required on the dedicated / hosted server and every PC client** (same version). Valheim 1.0 · BepInExPack 5.4.2350+. Console players via crossplay cannot load the mod.

**Bug reports:** https://github.com/morda0511/StoreAndCraft/issues/1

---

## Keys

| Key | What it does |
|---|---|
| **.** | Dump inventory into matching nearby chests |
| **Middle mouse** | Store the hovered inventory/chest item into nearby chests (only while inventory is open) |
| **Ctrl + Middle mouse** | Fill the hovered stack from nearby chests (only while inventory is open) |
| **Alt + E** | Rename the looked-at chest |

### Ignore Items

Prefix a chest name with **`[I]`** to ignore it (no auto-store / no pulling from it).

Ignored chests show **red hover text**, so you can spot them quickly.

---

## Craft, build & stations

Crafting and building use items from nearby chests automatically when you don’t have enough on you.

On smelters, charcoal kilns, ovens, torches and fires, **[E]** can also pull fuel / ore / food from nearby chests.

Config option **LeaveOneItem**: when pulling from a chest, one item of that stack stays behind (applies to craft, build, station fill, and Ctrl+middle-click fill).

---

## Storage Display (Hammer)

Build **Storage Display** (looks like a wood sign). It shows how much of each item is in nearby chests.

Chests marked with **`[I]`** (Ignore) are **not counted** on the display.

1. Place the display.
2. Press **[E]** — a Valheim-style list opens (same window style as Skills).
3. Click a type (e.g. Wood, Ore, Food).
4. The board only shows items of that type.

Until you pick a type, the board shows **Select type**.

### Types

**Resources**

- **Wood** — all wood kinds (Wood, Fine wood, Core wood, …)
- **Ore** — ores and metal scrap (Copper ore, Iron scrap, …)
- **Metals** — smelted bars and nails (Copper, Iron, Bronze nails, …)
- **Stone** — Stone, Flint, Obsidian, Crystal, …
- **Fuel** — Coal, Resin, Tar
- **Hides** — hides, leather scraps, pelts, scales
- **Parts** — bones, entrails, feathers, glands, …
- **Crops & Seeds** — plants, seeds, cones, herbs
- **Raw Food** — raw meat/fish, dough, mead bases
- **Gems & Coins** — coins, amber, rubies, …
- **Boss / Rare** — Surtling core, Dragon tear, Ancient seed, …

**Other**

- **Food** — finished food and meads (not raw)
- **Fish** · **Trophy** · **Ammo** · **Tools** · **Weapons** · **Armor**
- **Utility** — Megingjord, Wishbone, …
- **Misc** — swamp key, dragon egg, totems, saddles, …

### Several displays

Place more Storage Displays of the **same type** next to each other. They share one list and show as pages `(1/2)`, `(2/2)`, … — first placed = page 1.

---

## Config

- `BepInEx/config/StoreAndCraft.cfg` — range, keys, LeaveOneItem, …
- `StoreAndCraft.rules.yml` — optional allow/deny lists per chest name
