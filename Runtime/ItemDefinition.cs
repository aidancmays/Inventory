using GameplayTags;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// One kind of item: what it is, not how many anyone has (that is an ItemStack in an Inventory).
    /// Shared asset. Plain items (keys, materials) use this class as it is; items that do something when equipped
    /// or used are subclasses (WeaponItem, ArmorItem, RingItem, SpellItem, ConsumableItem).
    ///
    /// Category is a gameplay tag and decides which inventory bag the item goes in: a bag takes the categories it
    /// lists and their descendants, so with Sword (parent Weapon) a Weapons bag takes swords.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Used in save data. Set it once and never change it. Empty = the asset's name (so then never rename the asset).")]
        [SerializeField] string id;

        [SerializeField] string displayName;

        [TextArea]
        [SerializeField] string description;

        [SerializeField] Sprite icon;

        [Tooltip("What kind of item this is (Weapon, Consumable, Key Item...). Decides which inventory bag it goes in.")]
        [SerializeField] GameplayTag category;

        [Tooltip("How many fit in one stack. 1 = each one is separate (weapons, armour). 0 = no limit.")]
        [Min(0)]
        [SerializeField] protected int maxStack = 1;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameplayTag Category => category;

        /// <summary>More than one fits in a stack.</summary>
        public bool Stacks => maxStack != 1;

        /// <summary>How many fit in one stack (int.MaxValue for no limit).</summary>
        public int StackLimit => maxStack == 0 ? int.MaxValue : maxStack;

        /// <summary>Is the item's category that tag or a descendant of it? False with no category.</summary>
        public bool IsA(GameplayTag tag) => category != null && category.Is(tag);

        /// <summary>Can it be put in an equipment slot of that type? Plain items go nowhere.</summary>
        public virtual bool CanGoIn(EquipSlotType slot) => false;
    }
}
