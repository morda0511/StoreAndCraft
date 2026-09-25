# Changelog

## 1.3.42

### NEW FEATURES
- F11 toggles an activity log (up to 10 lines under the TopLeft status text, same font as "You are cold"; starts off). Lines like `Chest 10x Tin -> Smelter` / `Kiln 5x Coal -> Chest`
- Auto-store (**N**): finished kiln / smelter / blast-furnace bars, cooking food, and beehive honey go into nearby chests (independent from Auto-fill **B**)
- Station Alt+E filter also blocks manually inserting denied wood / ore / food from your bag (not only chest pull)

### UI IMPROVEMENTS
- Station output toggle (**N**) labeled Auto-store / Auto-Lagern
- Alt+E labeled **Settings** everywhere (chest, station, hover)
- Station Settings: Display-style item cells (sprite + name + toggle, 2 per row); link grid (l1-l9) in Valheim bronze style, sized and placed in the parchment margin
- Chest rename link grid sits a few pixels higher (no panel-edge clip)
- Large Storage Display category labels show the full name again (Scale / Layout); Medium/Large keep readable amounts

### FIX
- Auto-store (**N**) deposit order: matching linked chest first (item already inside); if full, unlinked chest that already holds the item; never a different link - otherwise ground drop
- Auto-fill (**B**) never takes from your bag - chests only (matching link, or both unlinked). Removed StationFillSkipInventory config
- Auto-fill uses chests around the station (not only around the player) while you stay in range; reacts faster and retries once on a stale chest view
- Station link l1-l9: kiln/smelter/oven/fire no longer pull from the wrong channel
- Removed Remote Automation completely (including background keep-alive / zone scans)
- Opening rename chest no longer spams LiberationSans font missing warnings

## 1.3.41

### FIX
- Station auto-fill / Shift+[E] fill-to-max no longer take from your bag by default (`StationFillSkipInventory` defaults to on). Set it to false in config if you want bag-first

## 1.3.40

### FIX
- Storage Displays no longer flicker when changing Scale or Layout
- Kiln / smelter chest pull no longer takes wood or ore from unlinked chests
- Food Preparation Table: Raw Fish (and other only-one-ingredient recipes) craft from nearby chests and remove the fish from the chest

## 1.3.39

### UI IMPROVEMENTS
- Station hover prompts use one order everywhere: vanilla actions, then two-key shortcuts, then single keys

### FIX
- Cooking: raw meat from the hotbar or chests (including Volture meat) works on cooking stations again
- Cooking auto-fill places food on the spit/oven instead of treating it as not allowed
- Stone oven no longer shows a third empty hover on the body (food door and wood only)

## 1.3.38

### FIX
- Cooking from chests: raw meat (including Vulture meat) cooks on the spit/oven again instead of only appearing in your bag
- Remote Automation temporarily disabled (unstable when far from the station)
- Shift+[E] Fill to max works on fireplaces and torches (and cooking-station fuel)

## 1.3.36

### NEW FEATURES
- Ballista (Mistlands turret) auto-fill: press [B] to load ammo from bag and nearby chests

### UI IMPROVEMENTS
- Kiln / smelter hover: no duplicate Auto-fill / Fill to max; order is Add ? Fill to max ? Chest pull filter ? Auto-fill
- Favorites: star badge only ? no gold tint on the item icon

### FIX
- Station auto-fill finds items in nearby chests again when chest inventories looked empty after the wipe-guard (Frost Foundry / smelters / kilns)
- Frost Foundry casts (and other max-stack-1 items) can be pulled from chests again ? LeaveOne no longer blocks the only copy

## 1.3.35

### FIX
- Cooking auto-fill (campfire sticks / spit / oven) pulls food from chests again like pressing [E]; stone ovens still fall back to network add when the under-fire is out

## 1.3.34

### NEW FEATURES
- Hold Shift and press [E] on a kiln, smelter, or blast furnace to fill fuel or ore to max from your bag and nearby chests

### FIX
- Storage Displays no longer clear nearby chest contents when counting items (stronger empty-load guard)
- Feed Trough keeps food visible and usable after you put carrots or other animal food inside
- Config sync no longer loops forever when the server handshake works but the config package fails; the client keeps local settings and warns once

## 1.3.33

### FIX
- Forge of Potential: chest idols now count and can be spent. LeaveOne does not apply there (one idol in a chest is enough)

## 1.3.32

### FIX
- Select Types menu opens without a long freeze (no full item-list rebuild for every category)

## 1.3.31

### NEW FEATURES
- Station auto-fill: new config `StationFillSkipInventory` ? when on, kilns, smelters, ovens, and fires take from chests only, never from your bag
- Feed Trough: hungry tames walk up to the trough and eat there, like food on the ground
- Auto-drop (**N**) also works on beehives ? honey drops on the ground for auto-store

### UI IMPROVEMENTS
- Feed Trough shows a small food sparkle when it has food inside (hidden when empty)
- Storage Display scale: Shift+LMB steps **25%** (?2?+4); icon and count stay one pair with room for 4-digit totals; category labels stay fixed
- Storage Display layout: Shift+RMB toggles **Classic** ? **Compact** on Medium/Large (saved per board). Compact starts each category on its own row (label + items)
- Storage Display type menu: categories on top with on/off toggles, items for the selected category below (icon + one toggle each); faster scroll

### FIX
- Storage Displays no longer wipe nearby chests when counting items (iron chests included)
- Dump / sort / auto-fill leave equipment and quick-slot mod overflow alone (e.g. EquipmentAndQuickSlotsPlus Z V B); Wider / Deeper Pockets bag rows work like normal inventory
- Coming back to base after a raid no longer saves empty reinforced chests (lag spike / unload race)
- Feed Trough hover tells you to put food inside
- Storage Display: switching Compact ? Classic no longer flickers / blanks the board (Medium header strip restored)
- Storage Display only reads chests ? no re-Load over a full bag and no empty Save if a count briefly shows 0
- Large Classic scale grows icon and count together (taller rows); categories pack without overlapping numbers
- Select Types menu uses Valheim fonts (no LiberationSans missing-font spam)

## 1.3.30

### NEW FEATURES
- Storage Display scale: Shift + left click on Medium / Large cycles -10..+10 (icons and counts); Small cycles Name + Amount / No name / Icon only
- Feed Trough: hammer piece (Storage tab) - put animal food inside; nearby hungry tames eat from it

### UI IMPROVEMENTS
- Chest rename: Ignore and Show on Display use the new on/off toggle buttons (plus l1-l9 link grid)

### FIX
- Safer chest saves so items no longer vanish when a chest briefly looks empty on load
- Empty chests keep their custom rename (no longer lose the name / show broken empty titles)
