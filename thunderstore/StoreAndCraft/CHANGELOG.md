# Changelog

## 1.3.32

### FIX
- Select Types menu opens without a long freeze (no full item-list rebuild for every category)

## 1.3.31

### NEW FEATURES
- Station auto-fill: new config `StationFillSkipInventory` — when on, kilns, smelters, ovens, and fires take from chests only, never from your bag
- Feed Trough: hungry tames walk up to the trough and eat there, like food on the ground
- Auto-drop (**N**) also works on beehives — honey drops on the ground for auto-store

### UI IMPROVEMENTS
- Feed Trough shows a small food sparkle when it has food inside (hidden when empty)
- Storage Display scale: Shift+LMB steps **25%** (−2…+4); icon and count stay one pair with room for 4-digit totals; category labels stay fixed
- Storage Display layout: Shift+RMB toggles **Classic** ↔ **Compact** on Medium/Large (saved per board). Compact starts each category on its own row (label + items)
- Storage Display type menu: categories on top with on/off toggles, items for the selected category below (icon + one toggle each); faster scroll

### FIX
- Storage Displays no longer wipe nearby chests when counting items (iron chests included)
- Dump / sort / auto-fill leave equipment and quick-slot mod overflow alone (e.g. EquipmentAndQuickSlotsPlus Z V B); Wider / Deeper Pockets bag rows work like normal inventory
- Coming back to base after a raid no longer saves empty reinforced chests (lag spike / unload race)
- Feed Trough hover tells you to put food inside
- Storage Display: switching Compact → Classic no longer flickers / blanks the board (Medium header strip restored)
- Storage Display only reads chests — no re-Load over a full bag and no empty Save if a count briefly shows 0
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
