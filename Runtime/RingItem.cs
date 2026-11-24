using UnityEngine;

namespace InventorySystem
{
    /// <summary>A ring: goes in any ring slot and is in effect while worn. The same ring can't be worn twice.</summary>
    [CreateAssetMenu(menuName = "Inventory/Ring", fileName = "Ring Item")]
    public class RingItem : EquipmentItem
    {
        public override bool OnlyOneEquipped => true;

        public override bool CanGoIn(EquipSlotType slot) => slot == EquipSlotType.Ring;
    }
}
