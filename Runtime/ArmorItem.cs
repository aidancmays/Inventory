using UnityEngine;

namespace InventorySystem
{
    /// <summary>A piece of armour: goes in the slot for its body part and is in effect while worn.</summary>
    [CreateAssetMenu(menuName = "Inventory/Armor", fileName = "Armor Item")]
    public class ArmorItem : EquipmentItem
    {
        public enum Piece
        {
            Head,
            Chest,
            Hands,
            Legs,
        }

        [Header("Armor")]
        [SerializeField] Piece piece = Piece.Chest;

        public Piece BodyPart => piece;

        public override bool CanGoIn(EquipSlotType slot)
        {
            switch (piece)
            {
                case Piece.Head: return slot == EquipSlotType.Head;
                case Piece.Chest: return slot == EquipSlotType.Chest;
                case Piece.Hands: return slot == EquipSlotType.Hands;
                default: return slot == EquipSlotType.Legs;
            }
        }
    }
}
