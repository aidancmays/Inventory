# Inventory

Items, an inventory of stacks, and souls-like equipment for RPG characters.

## Requirements

- Unity 6.3 (6000.3) or newer
- **Gameplay Tags** and **Character Stats** (`com.aidancmays.gameplay-tags`, `com.aidancmays.character-stats`). Add them
  from disk first, in that order.
- Input System is optional (only for the F6 debug panel's hotkey).

## Items (Create > Inventory)

| Asset | Notes |
|---|---|
| **Item** | Plain items (keys, materials). Id (for saves), name, description, icon, a **category** gameplay tag (decides its bag), max stack (1 = no stacking, 0 = no limit). |
| **Weapon** | Held in a hand: either, right only or left only. An optional Prefab for your weapon code to put in the hand (see `Equipment.HeldChanged`). |
| **Armor** | Head, chest, hands or legs. |
| **Ring** | Any ring slot; the same ring can't be worn twice. |
| **Spell** | Attuned in spell slots; takes Attunement Cost slots of the character's attunement stat. |
| **Consumable** | Quick item slots; restores pools on use. Subclass and override `Use(user)` for anything else. |
| **Item Database** | Every item, so saves can find items by id. Its context menu has *Collect Every Item In Project*. |

Weapons, armour, rings and spells are **equipment items**: while in effect they add their stat modifiers and gameplay
tags to the wearer, and run their **Effects**, a list of your own `EquipEffect` subclasses (grant abilities, spawn a
particle effect...). Effects are `[Serializable]` classes chosen with the list's + button.

## Components

- **Inventory**: bags in priority order, each taking some categories (and their descendants; none = everything),
  limited to some spaces or unlimited, optionally storage (never equipped from), with an overflow bag. `Add`, `Remove`,
  `Move`, `Count`, `Has`, `FindAll`, `RoomFor`, a `StackChanged` event, and `Capture()` / `Restore(snapshot, database)`.
- **Equipment** (next to Inventory, and a StatSheet if the character has stats): slot counts per type in *Layout*,
  starting equipment, optional attunement and equip-weight stats. `Equip`, `EquipAnywhere`, `Unequip`, `Select`,
  `Cycle(type, direction)`, `UseQuickItem()`, `InHand(hand)`, `SlotChanged`, `SelectionChanged` and `HeldChanged`
  events, `Capture()` / `Restore(snapshot)` (restore the inventory first). In effect: armour and rings while equipped,
  the selected weapon of each hand, the selected spell. Nothing reads input: your input code calls Cycle and UseQuickItem.

## Debug panel

Add **Inventory Debug Panel** to the character (editor and development builds; F6 toggles): every slot, every bag,
click to equip, unequip, use, drop or move, and give any item from the database. `InventoryDebugPanel.DrawControls(inventory)`
draws it inside your own overlay.

## Sample

*Equipment*: add **Inventory Sample Controller** to an empty GameObject and press Play. It gives one of every sample
item; equip and cycle them in the F6 panel and watch max health change in the F5 panel. The Ring of Vigour also lights
a glow through the sample `GlowWhileWorn` effect.
