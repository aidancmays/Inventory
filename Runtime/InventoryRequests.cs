using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Everything a player (input code, UI, debug panels) asks of an inventory and its equipment. Ask through this
    /// rather than calling Inventory and Equipment directly, and the same code works offline and in multiplayer:
    /// <see cref="InventoryRequests.For"/> returns a component on the character that implements it (a networked
    /// inventory, which sends the request to whoever owns the real contents) or, if there is none, a plain local one.
    ///
    /// The bools say whether the request looks like it worked. A networked one only knows what its local copy says;
    /// the result arrives a moment later.
    /// </summary>
    public interface IInventoryRequests
    {
        bool Equip(ItemStack stack, EquipSlotType type, int index);
        bool EquipAnywhere(ItemStack stack);
        void Unequip(EquipSlotType type, int index);

        /// <summary>Unequips into a bag at a space, swapping when that bag is full (see Equipment.UnequipInto).</summary>
        bool UnequipInto(EquipSlotType type, int index, InventoryBag bag, int bagIndex);
        void Select(EquipSlotType type, int index);
        bool Cycle(EquipSlotType type, int direction = 1);

        /// <summary>Uses the selected quick item.</summary>
        bool UseQuickItem();

        /// <summary>Uses a consumable stack, in a quick item slot or not (not from storage).</summary>
        bool Use(ItemStack stack);

        /// <summary>Takes up to count from a stack (throwing it away, dropping it).</summary>
        int Remove(ItemStack stack, int count = 1);

        int Move(ItemStack stack, InventoryBag to, int count = int.MaxValue);

        /// <summary>Moves a stack to another place in its own bag (see Inventory.Reorder).</summary>
        bool Reorder(ItemStack stack, int index);

        /// <summary>Adds items out of nowhere (debug panels, cheats).</summary>
        int Give(ItemDefinition item, int count = 1);
    }

    public static class InventoryRequests
    {
        static readonly Dictionary<Inventory, Local> locals = new Dictionary<Inventory, Local>();

        /// <summary>
        /// Where to send requests for this inventory: an <see cref="IInventoryRequests"/> component on its GameObject
        /// if there is one, else one that changes the inventory directly. Null for a null inventory.
        /// </summary>
        public static IInventoryRequests For(Inventory inventory)
        {
            if (inventory == null) return null;
            if (inventory.TryGetComponent(out IInventoryRequests requests)) return requests;

            if (!locals.TryGetValue(inventory, out Local local))
            {
                if (locals.Count > 64) Prune();
                local = new Local(inventory);
                locals[inventory] = local;
            }
            return local;
        }

        /// <summary>
        /// Uses a consumable stack right here: does what it does to the inventory's GameObject, then takes one if it
        /// is used up. For code that holds the real contents (a local inventory, or a server).
        /// </summary>
        public static bool UseHere(Inventory inventory, ItemStack stack)
        {
            if (!CanUse(inventory, stack, out ConsumableItem consumable)) return false;
            if (!consumable.Use(inventory.gameObject)) return false;

            if (consumable.ConsumedOnUse) inventory.Remove(stack, 1);
            return true;
        }

        /// <summary>Is the stack a consumable this inventory carries, outside storage?</summary>
        public static bool CanUse(Inventory inventory, ItemStack stack, out ConsumableItem consumable)
        {
            consumable = stack?.Item as ConsumableItem;
            return consumable != null && inventory != null && inventory.Contains(stack) && !stack.Bag.IsStorage;
        }

        static void Prune()
        {
            var dead = new List<Inventory>();
            foreach (Inventory key in locals.Keys)
            {
                if (key == null) dead.Add(key);
            }
            foreach (Inventory key in dead) locals.Remove(key);
        }

        /// <summary>Changes the inventory and equipment directly.</summary>
        sealed class Local : IInventoryRequests
        {
            readonly Inventory inventory;
            Equipment equipment;

            public Local(Inventory inventory) => this.inventory = inventory;

            Equipment Equipment => equipment != null ? equipment : equipment = inventory.GetComponent<Equipment>();

            public bool Equip(ItemStack stack, EquipSlotType type, int index) => Equipment != null && Equipment.Equip(stack, type, index);
            public bool EquipAnywhere(ItemStack stack) => Equipment != null && Equipment.EquipAnywhere(stack);
            public void Unequip(EquipSlotType type, int index) => Equipment?.Unequip(type, index);
            public bool UnequipInto(EquipSlotType type, int index, InventoryBag bag, int bagIndex) =>
                Equipment != null && Equipment.UnequipInto(type, index, bag, bagIndex);
            public void Select(EquipSlotType type, int index) => Equipment?.Select(type, index);
            public bool Cycle(EquipSlotType type, int direction = 1) => Equipment != null && Equipment.Cycle(type, direction);
            public bool UseQuickItem() => Equipment != null && Equipment.UseQuickItem();
            public bool Use(ItemStack stack) => UseHere(inventory, stack);
            public int Remove(ItemStack stack, int count = 1) => inventory.Remove(stack, count);
            public int Move(ItemStack stack, InventoryBag to, int count = int.MaxValue) => inventory.Move(stack, to, count);
            public bool Reorder(ItemStack stack, int index) => inventory.Reorder(stack, index);
            public int Give(ItemDefinition item, int count = 1) => inventory.Add(item, count);
        }
    }
}
