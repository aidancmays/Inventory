using System;
using System.Collections.Generic;

namespace InventorySystem
{
    /// <summary>What an Inventory needs saved: every stack, by bag id and item id. Plain data, ready for JsonUtility.</summary>
    [Serializable]
    public class InventorySnapshot
    {
        [Serializable]
        public struct Stack
        {
            public string bag;
            public string item;
            public int count;
            public string instance;
        }

        public List<Stack> stacks = new List<Stack>();
    }

    /// <summary>
    /// What Equipment needs saved: which stack (by instance id) is in which slot, and what is selected.
    /// Restore the inventory first. Modifiers and abilities are not saved: equipping applies them again.
    /// </summary>
    [Serializable]
    public class EquipmentSnapshot
    {
        [Serializable]
        public struct Slot
        {
            public EquipSlotType type;
            public int index;
            public string instance;
        }

        [Serializable]
        public struct Selection
        {
            public EquipSlotType type;
            public int index;
        }

        public List<Slot> slots = new List<Slot>();
        public List<Selection> selected = new List<Selection>();
    }
}
