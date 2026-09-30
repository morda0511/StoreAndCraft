# Changelog

## 1.3.45 (Beta)

### NEW FEATURES
- Carved storage displays in the hammer (Storage tab): new wooden board models for small, medium and large displays
- Large carved display shows each selected category in its own area with the category name above its items. **Shift+RMB** switches between layouts (Categories / Big / All), then Classic / Compact as before
- Small carved display (upright) can show categories too: **E** picks categories, **Shift+RMB** switches layouts, hotbar **1-8** still adds a single item
- Search box in the Storage Display filter menu (**E**): type a name to find items from every category
- Optional catch-up while away (`CatchUpWhileAway`, off by default): smelters, kilns etc. with auto-fill (B) and auto-store (N) finish the time their area was unloaded once you come back (max `CatchUpMaxHours`)
- Torches can start with auto-fill on (`TorchAutoFillDefault`, off by default, checkbox in F10 or `/torchautofill on|off`). **B** on a torch still turns it off
- Manual-fill chests: type `[M]` at the start of a chest name. Dump, ground pickup and auto-store no longer put items into it, stations and crafting can still take from it. Works with links, e.g. `[M] [l3] Wood`
- **Sort** and **Stack** buttons on the inventory and chest panels (Stack merges partial stacks of the same item)

### UI IMPROVEMENTS
- F10 settings panel rebuilt in the Valheim look (wood panel, vanilla buttons and checkboxes). Checked boxes show a clear yellow tick, and scrolling moves much further per wheel step
- Optional Valheim look for the Storage Display filter menu: set `DisplayMenuStyle = Vanilla` in the config (vanilla panels and buttons, click the whole item tile to select it)
- Sorting moved from the **R** key to the new **Sort** button (R is free again)
- Activity log on/off is remembered

### FIX
- Much less multiplayer lag around many stations: auto-fill only wakes up when a station in range actually needs something, and fills fuel / ore in one go instead of one item at a time
- Storage displays without chests in range no longer rescan several times per second
- Epic Loot items are no longer sent into another player's chest (their magic data could be lost)
- Carved display items sat at the wooden foot, were mirrored and counts were invisible — they now sit on the board, readable, with counts
- Removed an error in the log (NullReferenceException in WearNTear.UpdateBiome) caused by storage displays
- The character no longer walks around (and hotkeys no longer fire) while the Storage Display filter menu is open

**Beta:** needs the same version on server and all clients (network protocol changed).

## 1.3.44

### NEW FEATURES
- F10 settings panel: sliders and number fields for all ranges, activity log checkbox, Save button, short info on what each range does. On a server only admins (adminlist) can save; in a solo world everyone can
- New `AutoFillChestRange`: stations take items from chests around the station, not around the player (0 = same as AutoFillRange)
- Putting items into a chest wakes nearby stations right away (about 1 s instead of up to 25 s)
- Changing a station filter or link with Alt+E makes the station react at once

### UI IMPROVEMENTS
- Activity log / settings key moved from F11 to F10 (F11 is the Valheim screenshot key). Old F11 configs switch over automatically

### FIX
- Auto-fill stopped for kilns, smelters and windmills when many torches or fires were in range
- All stations now get a fair turn, so a large base no longer starves stations at the end of the list
- Two players near the same station no longer fill it twice at the same time
- A cooking station with no allowed food no longer blocks auto-fill for other stations

## 1.3.43

### NEW FEATURES
- Auto-store (**N**) on the fermenter: finished mead goes into a nearby chest (ground only if no matching chest)
- Fire and torch hover shows the remaining burn time in minutes, in green next to the name
- F11 activity log shows every mod action: dump, store, take stack, auto-store from the ground, craft and build pulls, build grab, station [E], feed trough, sort, auto-stack, B / N toggles, station filter and link, chest rename, config changes

### UI IMPROVEMENTS
- Fires and torches no longer show Alt+E Settings (they only take wood or resin)

### FIX
- Shift+E on fires fills to max again instead of adding only 1 wood
- Range settings are limited to 0-1000 m in the config file and in chat commands
- Fires and torches ignore station links and take fuel from unlinked chests

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
