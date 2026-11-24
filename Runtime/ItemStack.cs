namespace InventorySystem
{
    /// <summary>
    /// Some number of one item, in one inventory bag: what a slot in a bag shows. Equipment points at stacks, so
    /// the weapon in a hand is a particular one (a particular upgrade, later) and not just "a longsword".
    /// Created and changed by Inventory only.
    /// </summary>
    public sealed class ItemStack
    {
        public ItemDefinition Item { get; }

        /// <summary>How many. 0 once the stack is gone from the inventory.</summary>
        public int Count { get; internal set; }

        /// <summary>The bag it is in. Null once gone from the inventory.</summary>
        public InventoryBag Bag { get; internal set; }

        /// <summary>Stays the same for the stack's life and across saves, so equipment can point at it.</summary>
        public string InstanceId { get; }

        /// <summary>How many more fit in this stack.</summary>
        public int Room => Item.StackLimit == int.MaxValue ? int.MaxValue : Item.StackLimit - Count;

        internal ItemStack(ItemDefinition item, int count, string instanceId)
        {
            Item = item;
            Count = count;
            InstanceId = string.IsNullOrEmpty(instanceId) ? System.Guid.NewGuid().ToString("N") : instanceId;
        }

        public override string ToString() => Count == 1 ? Item.DisplayName : $"{Item.DisplayName} x{Count}";
    }
}
