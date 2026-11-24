namespace InventorySystem
{
    /// <summary>One equipment slot: a type, its number among slots of that type, and the stack in it (or nothing).</summary>
    public sealed class EquipSlot
    {
        public EquipSlotType Type { get; }
        public int Index { get; }

        /// <summary>The stack equipped here. Null when empty.</summary>
        public ItemStack Item { get; internal set; }

        public bool IsEmpty => Item == null;

        internal EquipSlot(EquipSlotType type, int index)
        {
            Type = type;
            Index = index;
        }

        public override string ToString() => $"{Type} {Index + 1}: {(Item != null ? Item.Item.DisplayName : "empty")}";
    }
}
