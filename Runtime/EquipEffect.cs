using System;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Something extra an equipment item does to its wearer while it is in effect, on top of the stat modifiers and
    /// tags every EquipmentItem has: grant abilities, start a particle effect, unlock a dialogue option.
    /// Subclass it (a plain [Serializable] class) and it shows up in the item's Effects list (added with the + button).
    ///
    /// Apply and Remove are called in pairs with the same context. For a held weapon the context's Hand says which
    /// hand, so one weapon held in both hands is applied twice. Keep any per-wearer state off the effect itself:
    /// one item asset (and so one effect object) is shared by everyone who has it; key state by context.Stack.
    /// </summary>
    [Serializable]
    public abstract class EquipEffect
    {
        public abstract void Apply(in EquipContext context);
        public abstract void Remove(in EquipContext context);
    }

    /// <summary>Who an EquipEffect is applied to and for what.</summary>
    public readonly struct EquipContext
    {
        /// <summary>The Equipment component of the wearer.</summary>
        public readonly Equipment Equipment;

        /// <summary>The stack in effect. Use it as the source of anything added, so two copies apply twice.</summary>
        public readonly ItemStack Stack;

        /// <summary>RightHand or LeftHand for a held weapon; null for everything else.</summary>
        public readonly EquipSlotType? Hand;

        public GameObject Wearer => Equipment.gameObject;

        public EquipContext(Equipment equipment, ItemStack stack, EquipSlotType? hand)
        {
            Equipment = equipment;
            Stack = stack;
            Hand = hand;
        }
    }
}
