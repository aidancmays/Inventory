# Changelog

## [0.1.0] - 2026-11-23

- First version, taken from Marcescence.
- Equipment no longer grants abilities or drives a weapon loadout itself: equipment items have an Effects list
  (`EquipEffect`) and Equipment raises `HeldChanged` for weapon code. `WeaponItem` holds a Prefab (GameObject).
- `ConsumableItem.Use` takes the user's GameObject. `Inventory.Database` can be set from code.
- Standalone Inventory Debug Panel (F6). Sample: Equipment.
