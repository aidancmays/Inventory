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
  `Move`, `Reorder` (a stack to another place in its bag), `Count`, `Has`, `FindAll`, `RoomFor`, a `StackChanged` event, and `Capture()` / `Restore(snapshot, database)`.
- **Equipment** (next to Inventory, and a StatSheet if the character has stats): slot counts per type in *Layout*,
  starting equipment, optional attunement and equip-weight stats. `Equip`, `EquipAnywhere`, `Unequip`, `Select`,
  `Cycle(type, direction)`, `UseQuickItem()`, `InHand(hand)`, `SlotChanged`, `SelectionChanged` and `HeldChanged`
  events, `Capture()` / `Restore(snapshot)` (restore the inventory first). With *Equipped Items Leave The Bag* on, an
  equipped item is taken out of its bag (into a hidden Equipped bag) so it no longer takes a bag space; unequipping
  needs room (`UnequipInto` puts it at a given space, swapping when the bag is full). In effect: armour and rings while equipped,
  the selected weapon of each hand, the selected spell. Nothing reads input: your input code calls Cycle and UseQuickItem.
  `StowHands(source)` / `UnstowHands(source)` put both hands' weapons away (while carrying something, say) without
  changing slots; it isn't synced, so on a networked character call it on every copy from shared state.

## Asking for changes

Input code and UI should ask through `InventoryRequests.For(inventory)` (an `IInventoryRequests`: `Equip`,
`EquipAnywhere`, `Unequip`, `Select`, `Cycle`, `UseQuickItem`, `Use`, `Remove`, `Move`, `Reorder`, `Give`) rather than call
Inventory and Equipment directly. Offline it changes them directly; on a networked character it sends the request to
the server. Reading (bags, stacks, slots, events) is the same either way.

## Multiplayer (Netcode for GameObjects)

With Netcode for GameObjects 2.7+ in the project, the `InventorySystem.Netcode` assembly adds **Network Inventory**. Put
it next to Inventory and Equipment on a networked character (with a NetworkObject), and give the Inventory an Item
Database (clients find items by id):

- The server's contents are the real ones. Change them on the server as usual (`Inventory.Add` for a pickup); every
  change goes to everyone else, whose copies mirror it (`Inventory.Mirror`, `Equipment.Mirror`), so equipment is in
  effect on every copy (modifiers, tags, EquipEffects, `HeldChanged`). Late joiners get the contents on spawn.
- The owner asks through `InventoryRequests.For(inventory)` (which finds the Network Inventory); the server checks and
  makes the change. Don't change a client's copy directly: the next update from the server undoes it.
- *Consumables Used On*: Owner (when the owner's StatSheet is the real one) uses the item there at once and the server
  then takes it; Server uses it on the server.
- `Dropped` (server): items the owner threw away, to put a pickup in the world.
- *Owner Can Give*: lets the owner add items from the database (the debug panel's Give); development builds always can.

## Debug panel

Add **Inventory Debug Panel** to the character (editor and development builds; F6 toggles): every slot, every bag,
click to equip, unequip, use, drop or move, and give any item from the database. `InventoryDebugPanel.DrawControls(inventory)`
draws it inside your own overlay.

## Sample

*Equipment*: add **Inventory Sample Controller** to an empty GameObject and press Play. It gives one of every sample
item; equip and cycle them in the F6 panel and watch max health change in the F5 panel. The Ring of Vigour also lights
a glow through the sample `GlowWhileWorn` effect.
