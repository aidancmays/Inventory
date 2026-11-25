using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// One of a character's inventories: Consumables, Weapons, Armor, Key Items, a storage chest... Set up in the
    /// list on Inventory. Each takes the item categories it lists and is either unlimited or has a number of spaces;
    /// a stack takes one space however many it holds.
    ///
    /// What does not fit can go on to another bag (Overflow To), e.g. into a Storage bag like the bottomless box.
    /// Items in a Storage bag can't be equipped or used, and it only gets items that overflow or are moved into it.
    ///
    /// An Equipped bag holds what Equipment has equipped when its Equipped Items Leave The Bag is on (Equipment adds
    /// one if there is none). Items only get there by being equipped, and UI should not show it as a bag.
    /// </summary>
    [Serializable]
    public class InventoryBag
    {
        [Tooltip("Used in save data and by Overflow To. Set it once and never change it.")]
        [SerializeField] string id = "Bag";

        [SerializeField] string displayName;

        [Tooltip("Item categories this bag takes, with their descendants. Empty = every item. " +
                 "An item goes in the first bag in the list that takes it, so put specific bags above catch-alls.")]
        [SerializeField] List<GameplayTag> accepts = new List<GameplayTag>();

        [Tooltip("Off: the bag holds any number of stacks. On: it has Spaces spaces, one per stack.")]
        [SerializeField] bool limited;

        [Min(1)]
        [SerializeField] int spaces = 20;

        [Tooltip("A storage bag (chest, bottomless box): its items can't be equipped or used, and it only gets items that " +
                 "overflow into it or are moved into it.")]
        [SerializeField] bool storage;

        [Tooltip("Id of the bag that gets what does not fit here. Empty = what does not fit is refused.")]
        [SerializeField] string overflowTo;

        [Tooltip("Holds what Equipment has equipped, when its Equipped Items Leave The Bag is on. Items only get here by " +
                 "being equipped; pickups and moves never put anything here. Leave it unlimited.")]
        [SerializeField] bool equipped;

        [NonSerialized] List<ItemStack> stacks;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public bool Limited => limited;

        /// <summary>How many stacks fit (int.MaxValue when unlimited).</summary>
        public int Spaces => limited ? Mathf.Max(1, spaces) : int.MaxValue;

        public bool IsStorage => storage;
        public string OverflowTo => overflowTo;

        /// <summary>The bag equipped items are kept in (see Equipment.EquippedItemsLeaveTheBag). Not a bag to show.</summary>
        public bool HoldsEquipped
        {
            get => equipped;
            internal set => equipped = value;
        }

        public IReadOnlyList<ItemStack> Stacks => List;
        public int UsedSpaces => List.Count;
        public int FreeSpaces => limited ? Mathf.Max(0, Spaces - List.Count) : int.MaxValue;
        public bool IsFull => FreeSpaces == 0;

        internal List<ItemStack> List => stacks ??= new List<ItemStack>();

        public InventoryBag() { }

        public InventoryBag(string id, bool limited = false, int spaces = 20, params GameplayTag[] accepts)
        {
            this.id = id;
            this.limited = limited;
            this.spaces = spaces;
            this.accepts = new List<GameplayTag>(accepts);
        }

        /// <summary>Is this the kind of item this bag holds? (Says nothing about room.)</summary>
        public bool Accepts(ItemDefinition item)
        {
            if (item == null) return false;
            if (accepts.Count == 0) return true;

            foreach (GameplayTag tag in accepts)
            {
                if (item.IsA(tag)) return true;
            }

            return false;
        }

        /// <summary>
        /// How many more of the item fit, in its stacks here and in free spaces (clamped to int.MaxValue).
        /// 0 for an item the bag does not take, except in storage, which takes anything sent to it.
        /// </summary>
        public int RoomFor(ItemDefinition item)
        {
            if (item == null || (!storage && !Accepts(item))) return 0;

            long room = 0;
            if (item.Stacks)
            {
                foreach (ItemStack stack in List)
                {
                    if (stack.Item == item) room += stack.Room;
                }
            }

            room += (long)Math.Min(FreeSpaces, int.MaxValue) * item.StackLimit;
            return (int)Math.Min(room, int.MaxValue);
        }

        /// <summary>How many of the item are in this bag.</summary>
        public int Count(ItemDefinition item)
        {
            int count = 0;
            foreach (ItemStack stack in List)
            {
                if (stack.Item == item) count += stack.Count;
            }
            return count;
        }
    }
}
