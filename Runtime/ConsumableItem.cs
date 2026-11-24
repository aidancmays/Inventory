using System;
using System.Collections.Generic;
using CharacterStats;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// An item that is used up (or not, with Consumed On Use off): goes in quick item slots, and Equipment.UseQuickItem
    /// uses the selected one. Out of the box it restores pools (health, stamina...); subclass and override Use for
    /// anything else (throwables, buffs).
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Consumable", fileName = "Consumable Item")]
    public class ConsumableItem : ItemDefinition
    {
        [Serializable]
        public struct PoolRestore
        {
            public PoolDefinition pool;
            public float amount;
        }

        [Header("Use")]
        [Tooltip("Each use takes one from the stack. Off: it can be used forever (refill it some other way).")]
        [SerializeField] bool consumedOnUse = true;

        [Tooltip("Added to these pools of the user when used.")]
        [SerializeField] List<PoolRestore> restores = new List<PoolRestore>();

        public bool ConsumedOnUse => consumedOnUse;
        public IReadOnlyList<PoolRestore> Restores => restores;

        void Reset()
        {
            maxStack = 99;
        }

        public override bool CanGoIn(EquipSlotType slot) => slot == EquipSlotType.QuickItem;

        /// <summary>
        /// Does what the item does to its user (the object with the Equipment, or whoever you pass). Returns false if
        /// it could not be used (then nothing is used up).
        /// </summary>
        public virtual bool Use(GameObject user)
        {
            if (user == null) return false;

            if (user.TryGetComponent(out StatSheet stats))
            {
                foreach (PoolRestore restore in restores)
                {
                    if (restore.pool != null && stats.TryGetPool(restore.pool, out ResourcePool pool)) pool.Restore(restore.amount, this);
                }
            }

            return true;
        }
    }
}
