# Changelog

## [0.4.0] - 2026-11-24

- `Inventory.Reorder(stack, index)`: move a stack to another place in its own bag (index clamped, so past the end puts
  it last). Raises `StackChanged` with the count unchanged. Also `IInventoryRequests.Reorder` (local, and an owner
  request on `NetworkInventory`).
- `Inventory.Mirror` follows order changes and raises `StackChanged` for stacks that only moved place, so a copy's UI
  redraws.
- Fix: an equipped item marked Only One Equipped can move to another slot (it used to count itself as already equipped).

## [0.3.0] - 2026-11-24

- `Equipment.StowHands(source)` / `UnstowHands(source)`, `HandsStowed`, `HandsStowedChanged`: put both hands' weapons
  away (nothing held or in effect, HeldChanged reports empty hands) without touching slots or selections, for as long
  as any source wants. Local to each copy: on a networked character, drive it from synced state.

## [0.2.0] - 2026-11-24

- `IInventoryRequests` / `InventoryRequests.For(inventory)`: one way to ask for equip, unequip, select, cycle, use,
  remove, move and give that works offline and networked. The Inventory Debug Panel uses it.
- `Inventory.Mirror(snapshot)` and `Equipment.Mirror(snapshot)`: match a snapshot keeping the stacks that are still
  there, so only real changes raise events and change what is in effect.
- `Equipment.UseStartingEquipment` (set false on copies whose equipment comes from elsewhere).
- `InventorySystem.Netcode` (compiled only with Netcode for GameObjects 2.7+): `NetworkInventory`, the server's
  inventory and equipment mirrored on every machine, owner requests checked and made by the server, consumables used
  on the owner or the server, late joiners synced on spawn, `Dropped` event on the server.

## [0.1.0] - 2026-11-23

- First version, taken from Marcescence.
- Equipment no longer grants abilities or drives a weapon loadout itself: equipment items have an Effects list
  (`EquipEffect`) and Equipment raises `HeldChanged` for weapon code. `WeaponItem` holds a Prefab (GameObject).
- `ConsumableItem.Use` takes the user's GameObject. `Inventory.Database` can be set from code.
- Standalone Inventory Debug Panel (F6). Sample: Equipment.
