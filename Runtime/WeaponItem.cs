using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// A weapon, shield or catalyst as an inventory item, held in a hand (the selected one of that hand's slots).
    /// The package doesn't spawn or animate anything: listen to Equipment.HeldChanged and put the Prefab in the
    /// character's hand with your own weapon code.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Weapon", fileName = "Weapon Item")]
    public class WeaponItem : EquipmentItem
    {
        public enum HandRestriction
        {
            EitherHand,
            RightHandOnly,
            LeftHandOnly,
        }

        [Header("Weapon")]
        [Tooltip("Optional. What your weapon code puts in the hand while this is held (see Equipment.HeldChanged).")]
        [SerializeField] GameObject prefab;

        [Tooltip("Which hands' slots it can go in (e.g. Left Hand Only for most shields).")]
        [SerializeField] HandRestriction hands = HandRestriction.EitherHand;

        public GameObject Prefab => prefab;
        public HandRestriction Hands => hands;

        public override bool CanGoIn(EquipSlotType slot)
        {
            switch (slot)
            {
                case EquipSlotType.RightHand:
                    return hands != HandRestriction.LeftHandOnly;
                case EquipSlotType.LeftHand:
                    return hands != HandRestriction.RightHandOnly;
                default:
                    return false;
            }
        }
    }
}
